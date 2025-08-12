using SteamNetworkLib.Models;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Threading.Tasks;

namespace SteamNetworkLib.Streaming
{
    /// <summary>
    /// Generic stream sender that handles encoding and sending real-time data streams
    /// with proper sequencing, timing, and metadata. Provides the foundation for streaming
    /// audio, video, or any continuous data over Steam P2P networks.
    /// </summary>
    /// <typeparam name="T">Type of data being sent (e.g., float[] for audio, byte[] for video)</typeparam>
    public abstract class StreamSender<T> : IDisposable where T : class
    {
        /// <summary>
        /// Gets the unique identifier for this stream.
        /// </summary>
        public string StreamId { get; }

        /// <summary>
        /// Gets or sets the type of stream being sent (e.g., "audio", "video", "data").
        /// </summary>
        public string StreamType { get; protected set; } = "data";

        /// <summary>
        /// Gets or sets the specific payload type within the stream type (e.g., "pcm_audio", "h264_video").
        /// </summary>
        public string PayloadType { get; protected set; } = "data";

        /// <summary>
        /// Gets or sets the duration of each frame in milliseconds.
        /// This affects timing and synchronization of the stream.
        /// </summary>
        public int FrameDurationMs { get; protected set; } = 20;

        /// <summary>
        /// Gets a value indicating whether the stream is currently active and sending data.
        /// </summary>
        public bool IsStreaming { get; private set; }

        /// <summary>
        /// The current sequence number for outgoing frames, incremented with each frame sent.
        /// </summary>
        protected uint _currentSequence = 0;
        
        /// <summary>
        /// Indicates whether this stream sender has been disposed and should no longer send data.
        /// </summary>
        protected bool _isDisposed = false;
        
        /// <summary>
        /// Reference to the network client used for sending stream data to connected peers.
        /// </summary>
        protected SteamNetworkClient? _networkClient;

        /// <summary>
        /// Occurs when the stream starts sending data.
        /// </summary>
        public event Action<StreamSender<T>>? OnStreamStarted;

        /// <summary>
        /// Occurs when the stream stops sending data.
        /// </summary>
        public event Action<StreamSender<T>>? OnStreamEnded;

        /// <summary>
        /// Occurs when a frame has been successfully sent over the network.
        /// </summary>
        public event Action<uint, int>? OnFrameSent;

        /// <summary>
        /// Gets the total number of frames sent since the stream started.
        /// </summary>
        public uint TotalFramesSent { get; private set; }

        /// <summary>
        /// Gets the total number of bytes sent since the stream started.
        /// </summary>
        public uint TotalBytesSent { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamSender{T}"/> class.
        /// </summary>
        /// <param name="streamId">The unique identifier for this stream.</param>
        /// <param name="networkClient">The network client used for sending stream data.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="streamId"/> is null.</exception>
        protected StreamSender(string streamId, SteamNetworkClient? networkClient)
        {
            StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
            _networkClient = networkClient;
        }

        /// <summary>
        /// Starts the stream by sending a stream start message to all connected peers.
        /// Initializes the sequence counter and notifies the network that streaming has begun.
        /// </summary>
        public virtual void StartStream()
        {
            if (IsStreaming || _isDisposed) return;

            IsStreaming = true;
            _currentSequence = 0;

            var startMessage = CreateStreamMessage(new byte[0], true, false);
            SendStreamMessage(startMessage);

            OnStreamStarted?.Invoke(this);
        }

        /// <summary>
        /// Stops the stream by sending a stream end message to all connected peers.
        /// Signals to receivers that no more data will be sent for this stream.
        /// </summary>
        public virtual void StopStream()
        {
            if (!IsStreaming || _isDisposed) return;

            var endMessage = CreateStreamMessage(new byte[0], false, true);
            SendStreamMessage(endMessage);

            IsStreaming = false;
            OnStreamEnded?.Invoke(this);
        }

        /// <summary>
        /// Sends a frame of data to all connected peers.
        /// Should be called by derived classes after encoding their specific data type.
        /// </summary>
        /// <param name="frameData">The frame data to encode and send.</param>
        protected virtual void SendFrame(T frameData)
        {
            if (!IsStreaming || _isDisposed || frameData == null) return;

            try
            {
                var encodedData = SerializeFrame(frameData);
                if (encodedData == null || encodedData.Length == 0) return;

                var message = CreateStreamMessage(encodedData, false, false);
                SendStreamMessage(message);

                TotalFramesSent++;
                TotalBytesSent += (uint)encodedData.Length;
                OnFrameSent?.Invoke(_currentSequence - 1, encodedData.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending frame: {ex.Message}");
            }
        }

        /// <summary>
        /// Serializes the frame data into bytes for network transmission.
        /// Must be implemented by derived classes to handle their specific data format.
        /// </summary>
        /// <param name="frameData">The frame data to serialize.</param>
        /// <returns>The serialized frame data as bytes, or null if serialization failed.</returns>
        protected abstract byte[]? SerializeFrame(T frameData);

        /// <summary>
        /// Creates a properly formatted <see cref="StreamMessage"/> with all required metadata.
        /// Automatically increments the sequence number and sets timing information.
        /// </summary>
        /// <param name="data">The encoded frame data to include in the message.</param>
        /// <param name="isStart">Whether this message marks the start of the stream.</param>
        /// <param name="isEnd">Whether this message marks the end of the stream.</param>
        /// <returns>A properly formatted stream message ready for transmission.</returns>
        protected virtual StreamMessage CreateStreamMessage(byte[] data, bool isStart, bool isEnd)
        {
            return new StreamMessage
            {
                StreamType = StreamType,
                StreamId = StreamId,
                SequenceNumber = _currentSequence++,
                CaptureTimestamp = DateTime.UtcNow.Ticks,
                IsStreamStart = isStart,
                IsStreamEnd = isEnd,
                StreamData = data,
                PayloadType = PayloadType,
                FrameDurationMs = FrameDurationMs,
                Priority = 128,
                RecommendedSendType = GetRecommendedSendType()
            };
        }

        /// <summary>
        /// Gets the recommended P2P send type for this stream type.
        /// Can be overridden by derived classes to specify different reliability levels.
        /// </summary>
        /// <returns>The recommended <see cref="EP2PSend"/> type for this stream.</returns>
        protected virtual EP2PSend GetRecommendedSendType()
        {
            return EP2PSend.k_EP2PSendUnreliableNoDelay;
        }

        /// <summary>
        /// Sends the stream message via the network client to all connected peers.
        /// Handles error logging and performance monitoring.
        /// </summary>
        /// <param name="message">The stream message to send.</param>
        protected virtual void SendStreamMessage(StreamMessage message)
        {
            if (_networkClient == null)
            {
                Console.WriteLine("[StreamSender] Warning: No network client available for sending stream message");
                return;
            }

            try
            {
                var sendStart = DateTime.UtcNow;
                _networkClient.BroadcastMessage(message);
                var sendTime = (DateTime.UtcNow - sendStart).TotalMilliseconds;

                if (sendTime > 10.0)
                {
                    Console.WriteLine($"[StreamSender] Network broadcast took {sendTime:F2}ms (slow!) for frame {message.SequenceNumber}");
                }
                else if (message.SequenceNumber % 100 == 0)
                {
                    Console.WriteLine($"[StreamSender] Frame {message.SequenceNumber}: Network send took {sendTime:F2}ms");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StreamSender] Error sending stream message: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a frame to a specific target player instead of broadcasting to all peers.
        /// Useful for targeted streaming or private communication.
        /// </summary>
        /// <param name="frameData">The frame data to encode and send.</param>
        /// <param name="targetId">The Steam ID of the target player.</param>
        /// <returns>A task that represents the asynchronous send operation. The task result indicates success.</returns>
        protected virtual async Task<bool> SendFrameToTarget(T frameData, CSteamID targetId)
        {
            if (!IsStreaming || _isDisposed || frameData == null || _networkClient == null)
                return false;

            try
            {
                var encodedData = SerializeFrame(frameData);
                if (encodedData == null || encodedData.Length == 0) return false;

                var message = CreateStreamMessage(encodedData, false, false);
                return await _networkClient.SendMessageToPlayerAsync(targetId, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending frame to target: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resets the stream sender to its initial state, clearing all statistics.
        /// Stops the stream if it's currently active.
        /// </summary>
        public virtual void Reset()
        {
            _currentSequence = 0;
            TotalFramesSent = 0;
            TotalBytesSent = 0;
            IsStreaming = false;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="StreamSender{T}"/>.
        /// Automatically stops the stream if it's currently active.
        /// </summary>
        public virtual void Dispose()
        {
            if (_isDisposed) return;

            if (IsStreaming)
            {
                StopStream();
            }

            _isDisposed = true;
        }
    }
}