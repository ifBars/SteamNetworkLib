using SteamNetworkLib.Models;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Collections.Generic;
using System.Linq;

namespace SteamNetworkLib.Streaming
{
    /// <summary>
    /// Generic streaming channel that handles real-time data streaming with jitter buffering,
    /// packet reordering, and loss detection. Codec-agnostic - subclasses handle specific encoding/decoding.
    /// Provides robust streaming capabilities for audio, video, or any continuous data streams.
    /// </summary>
    /// <typeparam name="T">The type of decoded frame data (e.g., float[] for audio, byte[] for video)</typeparam>
    public abstract class StreamChannel<T> : IDisposable where T : class
    {
        /// <summary>
        /// Gets the unique identifier for this stream channel.
        /// </summary>
        public string StreamId { get; }

        /// <summary>
        /// Gets or sets the target buffer size in milliseconds for jitter compensation.
        /// Higher values provide better resistance to network jitter but increase latency.
        /// </summary>
        public int BufferMs { get; set; } = 200;

        /// <summary>
        /// Gets or sets the maximum buffer size in milliseconds before dropping old frames.
        /// Prevents memory buildup when the network is severely congested.
        /// </summary>
        public int MaxBufferMs { get; set; } = 500;

        /// <summary>
        /// Gets or sets a value indicating whether packet loss detection and recovery is enabled.
        /// When enabled, attempts to handle missing frames with packet loss concealment.
        /// </summary>
        public bool EnablePacketLossDetection { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether jitter buffering is enabled.
        /// When disabled, frames are played immediately upon arrival (lower latency but less stability).
        /// </summary>
        public bool EnableJitterBuffer { get; set; } = true;

        /// <summary>
        /// Jitter buffer that stores incoming frames with their sequence numbers for ordered playback.
        /// </summary>
        protected readonly SortedDictionary<uint, StreamFrame<T>> _jitterBuffer = new();
        
        /// <summary>
        /// The next expected sequence number for ordered frame delivery.
        /// </summary>
        protected uint _expectedSequence = 0;
        
        /// <summary>
        /// The highest sequence number received so far, used for tracking stream progress.
        /// </summary>
        protected uint _highestSequenceReceived = 0;
        
        /// <summary>
        /// Indicates whether the stream is currently active and receiving data.
        /// </summary>
        protected bool _isStreaming = false;
        
        /// <summary>
        /// Indicates whether this stream channel has been disposed and should no longer process messages.
        /// </summary>
        protected bool _isDisposed = false;
        
        /// <summary>
        /// Timestamp of the last frame received, used for calculating stream timing and detecting gaps.
        /// </summary>
        protected DateTime _lastFrameTime = DateTime.UtcNow;
        
        /// <summary>
        /// Lock object for thread-safe access to the jitter buffer and related state.
        /// </summary>
        protected readonly object _bufferLock = new object();

        /// <summary>
        /// Occurs when a decoded frame is ready for playback or processing.
        /// </summary>
        public event Action<T>? OnFrameReady;

        /// <summary>
        /// Occurs when a frame is dropped due to buffer overflow or being too old.
        /// </summary>
        public event Action<uint>? OnFrameDropped;

        /// <summary>
        /// Occurs when the stream starts receiving data.
        /// </summary>
        public event Action<StreamChannel<T>>? OnStreamStarted;

        /// <summary>
        /// Occurs when the stream ends or stops receiving data.
        /// </summary>
        public event Action<StreamChannel<T>>? OnStreamEnded;

        /// <summary>
        /// Occurs when a frame arrives later than expected, indicating timing issues.
        /// </summary>
        public event Action<uint, TimeSpan>? OnFrameLate;

        /// <summary>
        /// Gets the current number of frames in the jitter buffer.
        /// </summary>
        public int BufferedFrameCount => _jitterBuffer.Count;

        /// <summary>
        /// Gets the total number of frames received since the stream started.
        /// </summary>
        public uint TotalFramesReceived { get; private set; }

        /// <summary>
        /// Gets the total number of frames dropped due to various reasons (buffer overflow, late arrival, etc.).
        /// </summary>
        public uint TotalFramesDropped { get; private set; }

        /// <summary>
        /// Gets the total number of frames that arrived later than expected.
        /// </summary>
        public uint TotalFramesLate { get; private set; }

        /// <summary>
        /// Gets the average jitter measurement for received frames.
        /// </summary>
        public double AverageJitter { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamChannel{T}"/> class.
        /// </summary>
        /// <param name="streamId">The unique identifier for this stream channel.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="streamId"/> is null.</exception>
        protected StreamChannel(string streamId)
        {
            StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
        }

        /// <summary>
        /// Processes a received stream message by adding it to the jitter buffer if enabled,
        /// or playing it immediately if buffering is disabled.
        /// </summary>
        /// <param name="message">The stream message to process.</param>
        /// <param name="senderId">The Steam ID of the player who sent this message.</param>
        public virtual void ProcessStreamMessage(StreamMessage message, CSteamID senderId)
        {
            if (_isDisposed || message.StreamId != StreamId) return;

            var receiveTime = DateTime.UtcNow;

            try
            {
                // Handle stream lifecycle
                if (message.IsStreamStart && !_isStreaming)
                {
                    Console.WriteLine($"[StreamChannel] Stream {StreamId} started from {senderId}");
                    StartStream();
                }
                else if (message.IsStreamEnd && _isStreaming)
                {
                    Console.WriteLine($"[StreamChannel] Stream {StreamId} ended from {senderId}");
                    EndStream();
                    return;
                }

                // Update statistics
                TotalFramesReceived++;
                _highestSequenceReceived = Math.Max(_highestSequenceReceived, message.SequenceNumber);

                // Initialize expected sequence from first received frame
                if (_expectedSequence == 0 && message.SequenceNumber > 0)
                {
                    _expectedSequence = message.SequenceNumber;
                    Console.WriteLine($"[StreamChannel] Setting initial expected sequence to {_expectedSequence}");
                }

                // Check for sequence number issues
                if (message.SequenceNumber < _expectedSequence)
                {
                    Console.WriteLine($"[StreamChannel] Ignoring old/duplicate frame {message.SequenceNumber} (expected: {_expectedSequence})");
                    return;
                }

                // Check for sequence gaps
                if (message.SequenceNumber > _expectedSequence && TotalFramesReceived > 10)
                {
                    var gap = message.SequenceNumber - _expectedSequence;
                    Console.WriteLine($"[StreamChannel] Sequence gap detected: got {message.SequenceNumber}, expected {_expectedSequence} (gap: {gap})");
                }

                // Calculate end-to-end latency
                var endToEndLatency = (receiveTime.Ticks - message.CaptureTimestamp) / TimeSpan.TicksPerMillisecond;

#if !MONO
                // IL2CPP specific check for marshalling issues
                if (message.StreamData == null || message.StreamData.Length == 0)
                {
                    Console.WriteLine($"[StreamChannel] Warning: Empty StreamData received in frame {message.SequenceNumber}");
                }
                else if (message.StreamData.Length > 4 && message.StreamData[0] == 0 && message.StreamData[1] == 0 && 
                         message.StreamData[2] == 0 && message.StreamData[3] == 0)
                {
                    Console.WriteLine($"[StreamChannel] IL2CPP marshalling issue detected! StreamData contains all zeros in frame {message.SequenceNumber}");
                    
                    // Try to log the first few bytes for debugging
                    var debugBytes = "";
                    for (int i = 0; i < Math.Min(message.StreamData.Length, 8); i++)
                    {
                        debugBytes += message.StreamData[i].ToString("X2") + " ";
                    }
                    Console.WriteLine($"[StreamChannel] First bytes: {debugBytes}");
                    
                    // Skip this frame as it's corrupted
                    TotalFramesDropped++;
                    return;
                }
#endif

                // Deserialize frame data
                var deserializeStart = DateTime.UtcNow;
                var frameData = DeserializeFrame(message.StreamData, message);
                var deserializeTime = (DateTime.UtcNow - deserializeStart).TotalMilliseconds;

                if (frameData == null)
                {
                    Console.WriteLine($"[StreamChannel] Failed to deserialize frame {message.SequenceNumber}");
                    return;
                }

                var frame = new StreamFrame<T>
                {
                    SequenceNumber = message.SequenceNumber,
                    Data = frameData,
                    CaptureTimestamp = message.CaptureTimestamp,
                    ReceivedTimestamp = receiveTime.Ticks,
                    FrameDurationMs = message.FrameDurationMs,
                    IsRetransmit = message.IsRetransmit
                };

                // Log network performance every 50 frames
                if (TotalFramesReceived % 50 == 0)
                {
                    Console.WriteLine($"[StreamChannel] Frame {message.SequenceNumber}: End-to-end latency: {endToEndLatency}ms, Deserialize time: {deserializeTime:F2}ms, Buffer level: {_jitterBuffer.Count}");
                }

                if (EnableJitterBuffer)
                {
                    AddToJitterBuffer(frame);
                }
                else
                {
                    // Direct playback without buffering
                    OnFrameReady?.Invoke(frame.Data);
                    _expectedSequence = message.SequenceNumber + 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StreamChannel] Error processing stream message: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the stream channel by processing buffered frames and performing maintenance tasks.
        /// Must be called regularly (e.g., in Update loop) to process buffered frames and maintain stream health.
        /// </summary>
        public virtual void Update()
        {
            if (_isDisposed || !EnableJitterBuffer) return;

            lock (_bufferLock)
            {
                ProcessJitterBuffer();
                CleanupOldFrames();
                DetectLostFrames();
            }
        }

        /// <summary>
        /// Deserializes codec-specific data into frame data.
        /// Must be implemented by derived classes to handle their specific data format.
        /// </summary>
        /// <param name="data">The raw data bytes to deserialize.</param>
        /// <param name="message">The complete stream message for context.</param>
        /// <returns>The deserialized frame data, or null if deserialization failed.</returns>
        protected abstract T? DeserializeFrame(byte[] data, StreamMessage message);

        /// <summary>
        /// Handles missing frames by generating replacement data or applying packet loss concealment.
        /// Can be overridden by subclasses to implement specific loss concealment strategies.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number of the missing frame.</param>
        /// <returns>Generated replacement frame data, or null to skip the frame.</returns>
        protected virtual T? HandleMissingFrame(uint sequenceNumber)
        {
            return null;
        }

        private void AddToJitterBuffer(StreamFrame<T> frame)
        {
            lock (_bufferLock)
            {
                if (frame.SequenceNumber < _expectedSequence) return;

                _jitterBuffer[frame.SequenceNumber] = frame;

                var maxFrames = Math.Max(5, MaxBufferMs / frame.FrameDurationMs);
                while (_jitterBuffer.Count > maxFrames)
                {
                    var oldestFrame = _jitterBuffer.First();
                    _jitterBuffer.Remove(oldestFrame.Key);
                    TotalFramesDropped++;
                    OnFrameDropped?.Invoke(oldestFrame.Key);
                    Console.WriteLine($"[StreamBuffer] Dropped old frame {oldestFrame.Key} - buffer too large ({_jitterBuffer.Count}/{maxFrames})");
                }
            }
        }

        private void ProcessJitterBuffer()
        {
            var processStart = DateTime.UtcNow;

            var frameDurationMs = _jitterBuffer.Count > 0 ? _jitterBuffer.Values.First().FrameDurationMs : 40;
            var minimumBufferFrames = Math.Max(2, BufferMs / frameDurationMs);

            if (_jitterBuffer.Count < minimumBufferFrames && _expectedSequence < 10)
            {
                if (_expectedSequence == 0 && _jitterBuffer.Count > 0)
                {
                    Console.WriteLine($"[StreamBuffer] Building initial buffer: {_jitterBuffer.Count}/{minimumBufferFrames} frames ({frameDurationMs}ms frames)");
                }
                return;
            }

            if (_jitterBuffer.Count == 0 && _isStreaming)
            {
                Console.WriteLine($"[StreamBuffer] Buffer underrun! Waiting for frames to rebuild buffer...");
                return;
            }

            var framesProcessed = 0;
            while (_jitterBuffer.ContainsKey(_expectedSequence) && framesProcessed < 3)
            {
                var frame = _jitterBuffer[_expectedSequence];
                _jitterBuffer.Remove(_expectedSequence);

                var expectedTime = _lastFrameTime.AddMilliseconds(frame.FrameDurationMs);
                var currentTime = DateTime.UtcNow;
                if (currentTime > expectedTime.AddMilliseconds(BufferMs))
                {
                    TotalFramesLate++;
                    var lateness = currentTime - expectedTime;
                    OnFrameLate?.Invoke(_expectedSequence, lateness);

                    if (lateness.TotalMilliseconds > 60)
                    {
                        Console.WriteLine($"[StreamBuffer] Frame {_expectedSequence} is {lateness.TotalMilliseconds:F1}ms late!");
                    }
                }

                if (_expectedSequence % 100 == 0)
                {
                    var bufferLevel = _jitterBuffer.Count;
                    var processingTime = (DateTime.UtcNow - processStart).TotalMilliseconds;
                    Console.WriteLine($"[StreamBuffer] Processed frame {_expectedSequence}, buffer level: {bufferLevel}, processing time: {processingTime:F2}ms");
                }

                OnFrameReady?.Invoke(frame.Data);
                _lastFrameTime = currentTime;
                _expectedSequence++;
                framesProcessed++;
            }

            if (EnablePacketLossDetection && !_jitterBuffer.ContainsKey(_expectedSequence))
            {
                var nextAvailableFrame = _jitterBuffer.Keys.FirstOrDefault(seq => seq > _expectedSequence);
                if (nextAvailableFrame > 0)
                {
                    var gap = nextAvailableFrame - _expectedSequence;

                    if (gap <= 3)
                    {
                        var missingFrameData = HandleMissingFrame(_expectedSequence);
                        if (missingFrameData != null)
                        {
                            OnFrameReady?.Invoke(missingFrameData);
                            Console.WriteLine($"[StreamBuffer] Used PLC for missing frame {_expectedSequence}");
                        }
                        else
                        {
                            TotalFramesDropped++;
                            OnFrameDropped?.Invoke(_expectedSequence);
                        }
                        _expectedSequence++;
                    }
                    else
                    {
                        Console.WriteLine($"[StreamBuffer] Large gap detected ({gap} frames), skipping to frame {nextAvailableFrame}");
                        for (uint seq = _expectedSequence; seq < nextAvailableFrame; seq++)
                        {
                            TotalFramesDropped++;
                            OnFrameDropped?.Invoke(seq);
                        }
                        _expectedSequence = nextAvailableFrame;
                    }
                }
            }
        }

        private void CleanupOldFrames()
        {
            var cutoffTime = DateTime.UtcNow.AddMilliseconds(-MaxBufferMs);
            var cutoffTicks = cutoffTime.Ticks;

            var framesToRemove = _jitterBuffer
                .Where(kvp => new DateTime(kvp.Value.ReceivedTimestamp) < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var frameSeq in framesToRemove)
            {
                _jitterBuffer.Remove(frameSeq);
                TotalFramesDropped++;
                OnFrameDropped?.Invoke(frameSeq);
            }
        }

        private void DetectLostFrames()
        {
            if (_jitterBuffer.Count > 0)
            {
                var oldestBufferedSeq = _jitterBuffer.Keys.Min();
                var gapSize = oldestBufferedSeq - _expectedSequence;

                var maxGapFrames = Math.Max(5, BufferMs / 40);
                if (gapSize > maxGapFrames)
                {
                    Console.WriteLine($"[StreamBuffer] Large gap detected: {gapSize} frames (max: {maxGapFrames}), jumping to frame {oldestBufferedSeq}");
                    for (uint seq = _expectedSequence; seq < oldestBufferedSeq; seq++)
                    {
                        TotalFramesDropped++;
                        OnFrameDropped?.Invoke(seq);
                    }
                    _expectedSequence = oldestBufferedSeq;
                }
            }
        }

        private void StartStream()
        {
            _isStreaming = true;
            _expectedSequence = 0;
            _highestSequenceReceived = 0;
            _lastFrameTime = DateTime.UtcNow;
            OnStreamStarted?.Invoke(this);
        }

        private void EndStream()
        {
            _isStreaming = false;
            OnStreamEnded?.Invoke(this);
        }

        /// <summary>
        /// Resets the stream channel to its initial state, clearing all buffers and statistics.
        /// </summary>
        public virtual void Reset()
        {
            lock (_bufferLock)
            {
                _jitterBuffer.Clear();
                _expectedSequence = 0;
                _highestSequenceReceived = 0;
                TotalFramesReceived = 0;
                TotalFramesDropped = 0;
                TotalFramesLate = 0;
                _isStreaming = false;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="StreamChannel{T}"/>.
        /// </summary>
        public virtual void Dispose()
        {
            if (_isDisposed) return;

            lock (_bufferLock)
            {
                _jitterBuffer.Clear();
            }

            _isDisposed = true;
        }
    }

    /// <summary>
    /// Represents a buffered stream frame with metadata for timing and sequencing.
    /// Contains the decoded frame data along with timing information for proper playback.
    /// </summary>
    /// <typeparam name="T">Type of the frame data (e.g., float[] for audio, byte[] for video)</typeparam>
    public class StreamFrame<T> where T : class
    {
        /// <summary>
        /// Gets or sets the sequence number of this frame in the stream.
        /// Used for ordering and detecting missing frames.
        /// </summary>
        public uint SequenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the decoded frame data ready for playback or processing.
        /// </summary>
        public T Data { get; set; } = default!;

        /// <summary>
        /// Gets or sets the timestamp when this frame was originally captured.
        /// Used for synchronization and latency measurements.
        /// </summary>
        public long CaptureTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this frame was received over the network.
        /// Used for jitter calculations and buffer management.
        /// </summary>
        public long ReceivedTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the expected duration of this frame in milliseconds.
        /// Used for timing calculations and buffer sizing.
        /// </summary>
        public int FrameDurationMs { get; set; } = 20;

        /// <summary>
        /// Gets or sets a value indicating whether this frame is a retransmission.
        /// Helps with quality metrics and debugging network issues.
        /// </summary>
        public bool IsRetransmit { get; set; }
    }
}