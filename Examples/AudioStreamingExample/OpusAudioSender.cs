#if SCHEDULE_ONE_INTEGRATION && MONO
// Audio streaming is only available on Mono runtime due to OpusSharp limitations with IL2CPP
using MelonLoader;
using OpusSharp.Core;
using OpusSharp.Core.Extensions;
using SteamNetworkLib;
using SteamNetworkLib.Streaming;
using SteamNetworkLib.Utilities;
using System;
using System.Collections;
using UnityEngine;

namespace AudioStreamingExample
{
    /// <summary>
    /// Audio sender that encodes PCM audio data using Opus and sends it via the network.
    /// Supports both Unity AudioClip streaming and real-time audio input.
    /// </summary>
    public class OpusAudioSender : StreamSender<float[]>
    {
        public int SampleRate { get; }
        public int Channels { get; }
        public int FrameSize { get; }
        public int Quality { get; set; } = 50; // Low quality for maximum reliability
        public int Bitrate { get; set; } = 64000; // Very conservative 64 kbps for packet loss tolerance

        private OpusEncoder _encoder;
        private readonly object _encoderLock = new object();
        private bool _encoderInitialized = false;

        // Streaming state
        private Coroutine? _streamingCoroutine;
        private AudioClip? _sourceClip;
        private float[]? _sourceData;
        private int _currentSamplePosition = 0;

        public OpusAudioSender(string streamId, int sampleRate, int channels, int frameSize, SteamNetworkClient? networkClient)
            : base(streamId, networkClient)
        {
            // Verify that audio streaming is supported in this runtime environment
            AudioStreamingCompatibility.ThrowIfNotSupported();
            
            SampleRate = sampleRate;
            Channels = channels;
            FrameSize = frameSize;

            StreamType = "audio";
            PayloadType = "audio";
            FrameDurationMs = (int)((double)frameSize / sampleRate * 1000); // Calculate based on actual frame size

            InitializeEncoder();
        }

        private void InitializeEncoder()
        {
            try
            {
                lock (_encoderLock)
                {
                    _encoder = new OpusEncoder(SampleRate, Channels, OpusPredefinedValues.OPUS_APPLICATION_AUDIO);

                    // Configure for high music quality
                    _encoder.SetBitRate(Bitrate);
                    _encoder.SetComplexity(10); // Maximum complexity for best quality
                    _encoder.SetSignal(OpusPredefinedValues.OPUS_SIGNAL_MUSIC); // Music mode for better quality
                    _encoder.SetMaxBandwidth((int)OpusPredefinedValues.OPUS_BANDWIDTH_FULLBAND); // Full audio spectrum

                    // Enable inband FEC with moderate packet loss expectation
                    _encoder.SetInbandFec(1); // 1 = enabled, 0 = disabled
                    _encoder.SetPacketLostPercent(10); // Expect moderate packet loss (10%)

                    // Enable VBR for better quality
                    _encoder.SetVbr(true); // Variable bitrate
                    _encoder.SetVbrConstraint(true); // Constrained VBR for consistency

                    _encoderInitialized = true;
                    MelonLogger.Msg($"[OpusEncoder] Initialized: {Bitrate}bps, Quality:{Quality}, FEC enabled, Music mode, Full bandwidth");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[OpusEncoder] Failed to initialize Opus encoder: {ex.Message}");
                _encoderInitialized = false;
            }
        }

        protected override byte[]? SerializeFrame(float[] frameData)
        {
            if (!_encoderInitialized || _isDisposed || frameData == null) return null;

            var startTime = DateTime.UtcNow;

            try
            {
                lock (_encoderLock)
                {
                    // Convert float to 16-bit PCM for Opus encoder (removed dithering to eliminate static)
                    short[] pcmData = new short[frameData.Length];

                    for (int i = 0; i < frameData.Length; i++)
                    {
                        // Clean conversion without dithering - eliminates static noise
                        pcmData[i] = (short)(Mathf.Clamp(frameData[i], -1f, 1f) * 32767f);
                    }

                    var conversionTime = DateTime.UtcNow;

                    // Encode with Opus
                    int maxOpusBytes = 4000; // Larger buffer for higher quality encoding
                    byte[] encodedBuffer = new byte[maxOpusBytes];

                    int encodedBytes = _encoder.Encode(pcmData, FrameSize, encodedBuffer, encodedBuffer.Length);

                    if (encodedBytes <= 0)
                    {
                        MelonLogger.Warning($"[AudioSender] Opus encoding failed, returned {encodedBytes} bytes");
                        return null;
                    }

                    var encodingTime = DateTime.UtcNow;

                    // Return only the actual encoded data
                    byte[] result = new byte[encodedBytes];
                    Array.Copy(encodedBuffer, 0, result, 0, encodedBytes);

#if !MONO
                    // Verify the data is not all zeros (IL2CPP marshalling check)
                    bool allZeros = encodedBytes > 4;
                    for (int i = 0; i < Math.Min(4, encodedBytes) && allZeros; i++)
                    {
                        if (result[i] != 0) allZeros = false;
                    }
                    
                    if (allZeros)
                    {
                        MelonLogger.Error($"[AudioSender] IL2CPP marshalling issue detected! Encoded data contains all zeros");
                        return null;
                    }
                    
                    // Log the first few bytes for debugging
                    if (TotalFramesSent % 100 == 0)
                    {
                        var debugBytes = "";
                        for (int i = 0; i < Math.Min(result.Length, 8); i++)
                        {
                            debugBytes += result[i].ToString("X2") + " ";
                        }
                        MelonLogger.Msg($"[AudioSender] Frame {TotalFramesSent} encoded data: {debugBytes} (length: {encodedBytes})");
                    }
#endif

                    var totalTime = encodingTime - startTime;
                    var convTime = conversionTime - startTime;
                    var opusTime = encodingTime - conversionTime;

                    // Log performance metrics every 50 frames to avoid spam
                    if (TotalFramesSent % 50 == 0)
                    {
                        MelonLogger.Msg($"[AudioSender] Frame {TotalFramesSent}: Encoding took {totalTime.TotalMilliseconds:F2}ms (conv: {convTime.TotalMilliseconds:F2}ms, opus: {opusTime.TotalMilliseconds:F2}ms), output: {encodedBytes} bytes");
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AudioSender] Error encoding audio frame: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Start streaming an AudioClip
        /// </summary>
        public void StartStreamFromClip(AudioClip clip)
        {
            if (IsStreaming || clip == null) return;

            // Resample if necessary
            AudioClip sourceClip = clip;
            if (clip.frequency != SampleRate)
            {
                MelonLogger.Msg($"Resampling audio from {clip.frequency}Hz to {SampleRate}Hz for Opus...");
                sourceClip = ResampleAudioClip(clip, SampleRate);
            }

            // Extract PCM data
            _sourceClip = sourceClip;
            _sourceData = new float[sourceClip.samples * sourceClip.channels];
            sourceClip.GetData(_sourceData, 0);
            _currentSamplePosition = 0;

            // Send event message to notify receivers to prepare audio channels
            if (_networkClient != null)
            {
                var startEventMessage = new SteamNetworkLib.Models.EventMessage
                {
                    EventType = "audio_stream",
                    EventName = "stream_start",
                    EventData = $"{{\"streamId\":\"{StreamId}\",\"sampleRate\":{SampleRate},\"channels\":{Channels},\"frameSize\":{FrameSize}}}",
                    Priority = 255, // High priority
                    ShouldPersist = false
                };
                
                try
                {
                    _networkClient.BroadcastMessage(startEventMessage);
                    MelonLogger.Msg($"[AudioSender] Sent stream_start event for '{StreamId}'");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AudioSender] Error sending stream_start event: {ex.Message}");
                }
            }

            // Start the stream
            StartStream();

            // Start streaming coroutine with timing info
            double streamStartTime = AudioSettings.dspTime + 1.0; // Give time to prepare
            _streamingCoroutine = (Coroutine?)MelonCoroutines.Start(StreamAudioCoroutine(streamStartTime));
        }

        private IEnumerator StreamAudioCoroutine(double streamStartTime)
        {
            if (_sourceData == null)
            {
                MelonLogger.Error("[AudioSender] No source data to stream");
                yield break;
            }

            int totalSamples = _sourceData.Length;
            int samplesPerFrame = FrameSize * Channels;
            int totalFrames = totalSamples / samplesPerFrame;

            MelonLogger.Msg($"🎵 Starting Opus stream with {totalFrames} frames (duration: {totalFrames * FrameDurationMs}ms)...");

            // Send initial frames immediately without delay
            for (int i = 0; i < 3 && IsStreaming && _currentSamplePosition < totalSamples; i++)
            {
                var frameStart = DateTime.UtcNow;
                SendNextFrame();
                var frameEnd = DateTime.UtcNow;

                MelonLogger.Msg($"[AudioSender] Initial frame {i}: took {(frameEnd - frameStart).TotalMilliseconds:F2}ms");
                yield return null; // Just yield one frame, don't wait
            }

            MelonLogger.Msg($"[AudioSender] Starting main streaming loop at {FrameDurationMs}ms intervals...");

            // **IMPROVED HIGH-PRECISION TIMER APPROACH**
            var nextFrameTime = DateTime.UtcNow.AddMilliseconds(FrameDurationMs);
            int frameCount = 3; // We already sent 3 frames

            while (IsStreaming && _currentSamplePosition < totalSamples)
            {
                var currentTime = DateTime.UtcNow;
                var timeUntilFrame = nextFrameTime - currentTime;

                // Wait with better precision
                if (timeUntilFrame.TotalMilliseconds > 5.0) // If we have more than 5ms to wait
                {
                    // Use coroutine yielding for most of the wait
                    var waitTime = timeUntilFrame.TotalMilliseconds - 2.0; // Leave 2ms for precise timing
                    yield return new WaitForSeconds((float)(waitTime / 1000.0));
                }

                // Precise busy wait for the final timing
                while (DateTime.UtcNow < nextFrameTime)
                {
                    // Very short busy wait for sub-millisecond precision
                }

                // Send the frame
                var sendStart = DateTime.UtcNow;
                SendNextFrame();
                var sendEnd = DateTime.UtcNow;

                // Calculate next frame time (cumulative to avoid drift)
                nextFrameTime = nextFrameTime.AddMilliseconds(FrameDurationMs);

                // Calculate timing metrics
                var timingError = (sendStart - nextFrameTime.AddMilliseconds(-FrameDurationMs)).TotalMilliseconds;
                var sendTime = (sendEnd - sendStart).TotalMilliseconds;

                // Log timing every 100 frames (reduced frequency)
                if (frameCount % 100 == 0)
                {
                    MelonLogger.Msg($"[AudioSender] Frame {frameCount}: Timing error: {timingError:F2}ms, Send time: {sendTime:F2}ms");
                }

                // Log only significant timing issues
                if (Math.Abs(timingError) > 2.0 || sendTime > 5.0)
                {
                    MelonLogger.Warning($"[AudioSender] Frame {frameCount}: High timing error: {timingError:F2}ms, Send time: {sendTime:F2}ms");
                }

                frameCount++;
            }

            // Stream completed
            MelonLogger.Msg($"🎵 Opus stream completed! Sent {frameCount} frames total.");
            StopStream();
        }

        private void SendNextFrame()
        {
            if (_sourceData == null || _currentSamplePosition >= _sourceData.Length) return;

            int samplesPerFrame = FrameSize * Channels;
            int samplesToSend = Math.Min(samplesPerFrame, _sourceData.Length - _currentSamplePosition);

            // Extract frame
            float[] frame = new float[samplesPerFrame];
            Array.Copy(_sourceData, _currentSamplePosition, frame, 0, samplesToSend);

            // Pad with silence if needed
            if (samplesToSend < samplesPerFrame)
            {
                for (int i = samplesToSend; i < samplesPerFrame; i++)
                {
                    frame[i] = 0f;
                }
            }

            // Send the frame
            SendFrame(frame);
            _currentSamplePosition += samplesToSend;
        }

        /// <summary>
        /// Send a single audio frame manually (for real-time input)
        /// </summary>
        public void SendAudioFrame(float[] audioData)
        {
            if (audioData == null || audioData.Length != FrameSize * Channels)
            {
                throw new ArgumentException($"Audio data must be exactly {FrameSize * Channels} samples");
            }

            SendFrame(audioData);
        }

        public override void StopStream()
        {
            // Send event message to notify receivers that stream is ending
            if (_networkClient != null && IsStreaming)
            {
                var endEventMessage = new SteamNetworkLib.Models.EventMessage
                {
                    EventType = "audio_stream",
                    EventName = "stream_end",
                    EventData = $"{{\"streamId\":\"{StreamId}\"}}",
                    Priority = 255, // High priority
                    ShouldPersist = false
                };
                
                try
                {
                    _networkClient.BroadcastMessage(endEventMessage);
                    MelonLogger.Msg($"[AudioSender] Sent stream_end event for '{StreamId}'");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AudioSender] Error sending stream_end event: {ex.Message}");
                }
            }
            
            if (_streamingCoroutine != null)
            {
                MelonCoroutines.Stop(_streamingCoroutine);
                _streamingCoroutine = null;
            }

            base.StopStream();
        }

        private AudioClip ResampleAudioClip(AudioClip originalClip, int targetSampleRate)
        {
            // Get original audio data
            float[] originalData = new float[originalClip.samples * originalClip.channels];
            originalClip.GetData(originalData, 0);

            // Calculate resampling ratio
            float ratio = (float)targetSampleRate / originalClip.frequency;
            int newSampleCount = Mathf.RoundToInt(originalClip.samples * ratio);

            // Simple linear interpolation resampling
            float[] resampledData = new float[newSampleCount * originalClip.channels];
            for (int i = 0; i < newSampleCount; i++)
            {
                float sourceIndex = i / ratio;
                int index1 = Mathf.FloorToInt(sourceIndex);
                int index2 = Mathf.Min(index1 + 1, originalClip.samples - 1);
                float t = sourceIndex - index1;

                for (int channel = 0; channel < originalClip.channels; channel++)
                {
                    int srcIdx1 = index1 * originalClip.channels + channel;
                    int srcIdx2 = index2 * originalClip.channels + channel;
                    int dstIdx = i * originalClip.channels + channel;

                    if (srcIdx1 < originalData.Length && srcIdx2 < originalData.Length)
                    {
                        resampledData[dstIdx] = Mathf.Lerp(originalData[srcIdx1], originalData[srcIdx2], t);
                    }
                }
            }

            // Create new AudioClip with resampled data
            var resampledClip = AudioClip.Create(originalClip.name + "_48kHz", newSampleCount, originalClip.channels, targetSampleRate, false);
            resampledClip.SetData(resampledData, 0);
            return resampledClip;
        }

        public override void Reset()
        {
            if (_streamingCoroutine != null)
            {
                MelonCoroutines.Stop(_streamingCoroutine);
                _streamingCoroutine = null;
            }

            _sourceData = null;
            _sourceClip = null;
            _currentSamplePosition = 0;

            base.Reset();
        }

        public override void Dispose()
        {
            if (_isDisposed) return;

            if (_streamingCoroutine != null)
            {
                MelonCoroutines.Stop(_streamingCoroutine);
                _streamingCoroutine = null;
            }

            lock (_encoderLock)
            {
                _encoder?.Dispose();
                _encoderInitialized = false;
            }

            base.Dispose();
        }
    }
}
#endif