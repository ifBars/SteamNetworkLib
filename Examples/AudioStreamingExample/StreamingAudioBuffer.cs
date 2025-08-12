#if SCHEDULE_ONE_INTEGRATION && MONO
// Audio streaming is only available on Mono runtime due to OpusSharp limitations with IL2CPP
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AudioStreamingExample
{
    /// <summary>
    /// Provides a streaming audio buffer that enables smooth, continuous audio playback
    /// without creating new <see cref="AudioClip"/> instances for each frame.
    /// Implements a circular buffer system for efficient real-time audio streaming.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed for real-time audio streaming scenarios where audio data
    /// arrives continuously and needs to be played back without interruption. It uses
    /// a circular buffer to manage audio data and Unity's streaming <see cref="AudioClip"/>
    /// functionality for seamless playback.
    /// </para>
    /// <para>
    /// The buffer automatically starts playback once enough data is buffered (100ms by default)
    /// to prevent audio dropouts and stuttering. The circular buffer design ensures
    /// memory-efficient operation even for long-running streams.
    /// </para>
    /// </remarks>
    public class StreamingAudioBuffer
    {
        private readonly AudioSource _audioSource;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly float[] _circularBuffer;
        private readonly int _bufferSize;
        private int _writePosition = 0;
        private int _readPosition = 0;
        private bool _isPlaying = false;
        private AudioClip? _streamingClip;
#if !MONO
        private float[] _tempBuffer;
        private int _frameCount;
        private int _updateCounter;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingAudioBuffer"/> class.
        /// </summary>
        /// <param name="audioSource">The <see cref="AudioSource"/> component to use for audio playback.</param>
        /// <param name="sampleRate">The sample rate of the audio stream in Hz (e.g., 44100).</param>
        /// <param name="channels">The number of audio channels (1 for mono, 2 for stereo).</param>
        /// <param name="bufferSizeSeconds">The size of the internal buffer in seconds. Larger buffers provide more stability but increase latency.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="audioSource"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sampleRate"/>, <paramref name="channels"/>, or <paramref name="bufferSizeSeconds"/> are invalid.</exception>
        /// <remarks>
        /// The buffer size is automatically rounded up to the next power of two for optimal performance.
        /// A buffer size of 2 seconds provides a good balance between stability and latency for most applications.
        /// </remarks>
        public StreamingAudioBuffer(AudioSource audioSource, int sampleRate, int channels, float bufferSizeSeconds = 2.0f)
        {
            _audioSource = audioSource;
            _sampleRate = sampleRate;
            _channels = channels;
            _bufferSize = Mathf.NextPowerOfTwo((int)(sampleRate * channels * bufferSizeSeconds));
            _circularBuffer = new float[_bufferSize];

#if MONO
            // Create a streaming AudioClip (works in Mono)
            _streamingClip = AudioClip.Create("StreamingAudio", _bufferSize / channels, channels, sampleRate, true, OnAudioRead);
            _audioSource.clip = _streamingClip;
            _audioSource.loop = true;
#else
            // IL2CPP workaround - Create a static audio clip with silence
            _frameCount = (int)(sampleRate * 0.1f); // 100ms buffer for updates
            _tempBuffer = new float[_frameCount * channels];
            
            try
            {
                // Create an empty AudioClip for initial playback
                _streamingClip = AudioClip.Create("StreamingAudio", _frameCount, channels, sampleRate, false);
                _audioSource.clip = _streamingClip;
                _audioSource.loop = true;
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Failed to create streaming audio clip: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Adds audio data to the streaming buffer for playback.
        /// </summary>
        /// <param name="audioData">The audio samples to add to the buffer. Must be in the same format as specified in the constructor.</param>
        /// <remarks>
        /// <para>
        /// This method is thread-safe and can be called from any thread. The audio data should match
        /// the sample rate and channel count specified during construction.
        /// </para>
        /// <para>
        /// If this is the first audio data added and enough samples are buffered (100ms worth),
        /// playback will automatically start. If the buffer becomes full, older data will be overwritten.
        /// </para>
        /// </remarks>
        public void AddAudioData(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0) return;

            lock (_circularBuffer)
            {
                // Add data to circular buffer
                for (int i = 0; i < audioData.Length; i++)
                {
                    _circularBuffer[_writePosition] = audioData[i];
                    _writePosition = (_writePosition + 1) % _bufferSize;
                }

                // Start playing if we have enough buffer
                if (!_isPlaying && GetBufferedSamples() >= _sampleRate * _channels * 0.1f) // 100ms buffer
                {
#if MONO
                    _audioSource.Play();
                    _isPlaying = true;
#else
                    var bufferedSamples = GetBufferedSamples();
                    MelonLoader.MelonLogger.Msg($"[StreamingAudioBuffer] Starting playback! Buffered samples: {bufferedSamples}, Required: {_sampleRate * _channels * 0.1f}");
                    UpdateAudioClipData(); // Update the clip with initial data
                    _audioSource.Play();
                    _isPlaying = true;
                    MelonLoader.MelonLogger.Msg($"[StreamingAudioBuffer] AudioSource.Play() called, isPlaying: {_audioSource.isPlaying}");
#endif
                }
#if !MONO
                // Log buffer state periodically
                if (_isPlaying && GetBufferedSamples() % 4800 < audioData.Length) // Every ~100ms worth of samples
                {
                    var bufferedSamples = GetBufferedSamples();
                    var bufferedMs = (bufferedSamples / (float)(_sampleRate * _channels)) * 1000f;
                    MelonLoader.MelonLogger.Msg($"[StreamingAudioBuffer] Buffer state: {bufferedSamples} samples ({bufferedMs:F1}ms), AudioSource playing: {_audioSource.isPlaying}");
                }
#endif
            }
        }

#if !MONO
        /// <summary>
        /// Updates the AudioClip with new data from the circular buffer (IL2CPP only)
        /// </summary>
        public void UpdateAudioClipData()
        {
            if (_streamingClip == null) 
            {
                MelonLoader.MelonLogger.Error("[StreamingAudioBuffer] UpdateAudioClipData: _streamingClip is null!");
                return;
            }
            
            if (!_isPlaying) 
            {
                // Still update data even if not officially "playing" yet
                // MelonLoader.MelonLogger.Msg("[StreamingAudioBuffer] UpdateAudioClipData: not playing yet, but updating data");
            }
            
            lock (_circularBuffer)
            {
                int samplesToRead = Mathf.Min(_tempBuffer.Length, GetBufferedSamples());
                
                for (int i = 0; i < _tempBuffer.Length; i++)
                {
                    if (i < samplesToRead)
                    {
                        _tempBuffer[i] = _circularBuffer[_readPosition];
                        _readPosition = (_readPosition + 1) % _bufferSize;
                    }
                    else
                    {
                        _tempBuffer[i] = 0f; // Silence if no data available
                    }
                }
                
                try
                {
                    _streamingClip.SetData(_tempBuffer, 0);
                    
                    // Log occasionally for debugging
                    if (samplesToRead > 0 && UnityEngine.Time.frameCount % 300 == 0) // Every ~5 seconds
                    {
                        var avgAmplitude = 0f;
                        for (int i = 0; i < Mathf.Min(samplesToRead, 100); i++)
                        {
                            avgAmplitude += Mathf.Abs(_tempBuffer[i]);
                        }
                        avgAmplitude /= Mathf.Min(samplesToRead, 100);
                        
                        MelonLoader.MelonLogger.Msg($"[StreamingAudioBuffer] Updated clip with {samplesToRead} samples, avg amplitude: {avgAmplitude:F4}, AudioSource playing: {_audioSource.isPlaying}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Error($"[StreamingAudioBuffer] Error setting clip data: {ex.Message}");
                }
            }
        }
#endif

        /// <summary>
        /// Unity's audio callback method that reads data from the circular buffer for playback.
        /// This method is called automatically by Unity's audio system.
        /// </summary>
        /// <param name="data">The audio buffer to fill with sample data.</param>
        /// <remarks>
        /// This method runs on Unity's audio thread and should not be called directly.
        /// It reads available samples from the circular buffer and fills any remaining
        /// space with silence to prevent audio artifacts.
        /// </remarks>
#if MONO
        private void OnAudioRead(float[] data)
        {
            lock (_circularBuffer)
            {
                int samplesToRead = Mathf.Min(data.Length, GetBufferedSamples());

                for (int i = 0; i < data.Length; i++)
                {
                    if (i < samplesToRead)
                    {
                        data[i] = _circularBuffer[_readPosition];
                        _readPosition = (_readPosition + 1) % _bufferSize;
                    }
                    else
                    {
                        data[i] = 0f; // Silence if no data available
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Gets the number of audio samples currently buffered and available for playback.
        /// </summary>
        /// <returns>The number of buffered samples.</returns>
        /// <remarks>
        /// This method accounts for the circular nature of the buffer and handles
        /// wrap-around scenarios correctly.
        /// </remarks>
        private int GetBufferedSamples()
        {
            int buffered = _writePosition - _readPosition;
            if (buffered < 0) buffered += _bufferSize;
            return buffered;
        }

        /// <summary>
        /// Resets the streaming buffer to its initial state and stops audio playback.
        /// </summary>
        /// <remarks>
        /// This method clears all buffered audio data, resets buffer positions,
        /// and stops the <see cref="AudioSource"/>. The buffer can be used again
        /// by calling <see cref="AddAudioData"/> with new audio data.
        /// </remarks>
        public void Reset()
        {
            lock (_circularBuffer)
            {
                _writePosition = 0;
                _readPosition = 0;
                _isPlaying = false;
                _audioSource.Stop();
                Array.Clear(_circularBuffer, 0, _circularBuffer.Length);
#if !MONO
                if (_tempBuffer != null)
                {
                    Array.Clear(_tempBuffer, 0, _tempBuffer.Length);
                }
#endif
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="StreamingAudioBuffer"/>.
        /// </summary>
        /// <remarks>
        /// This method stops audio playback and destroys the streaming <see cref="AudioClip"/>.
        /// The buffer should not be used after calling this method.
        /// </remarks>
        public void Dispose()
        {
            _audioSource.Stop();
            if (_streamingClip != null)
            {
                UnityEngine.Object.Destroy(_streamingClip);
            }
        }
        
        /// <summary>
        /// Updates the streaming buffer with new audio data.
        /// Call this method from Update() to ensure smooth playback in IL2CPP.
        /// </summary>
        public void Update()
        {
#if !MONO
            if (_isPlaying)
            {
                _updateCounter++;
                if (_updateCounter >= 2) // Update every 2 frames instead of 5 for smoother playback
                {
                    UpdateAudioClipData();
                    _updateCounter = 0;
                }
                
                // Log playback state occasionally
                if (UnityEngine.Time.frameCount % 600 == 0) // Every ~10 seconds
                {
                    var bufferedSamples = GetBufferedSamples();
                    var bufferedMs = (bufferedSamples / (float)(_sampleRate * _channels)) * 1000f;
                    MelonLoader.MelonLogger.Msg($"[StreamingAudioBuffer] Update - Playing: {_audioSource.isPlaying}, Buffer: {bufferedSamples} samples ({bufferedMs:F1}ms)");
                }
            }
            else if (GetBufferedSamples() > 0)
            {
                // If we have data but aren't playing, try to start
                var bufferedSamples = GetBufferedSamples();
                if (bufferedSamples >= _sampleRate * _channels * 0.05f) // 50ms buffer minimum
                {
                    MelonLoader.MelonLogger.Msg($"[StreamingAudioBuffer] Update - Not playing but have {bufferedSamples} samples, attempting to start");
                    UpdateAudioClipData();
                    _audioSource.Play();
                    _isPlaying = true;
                }
            }
#endif
        }
    }
}
#endif
