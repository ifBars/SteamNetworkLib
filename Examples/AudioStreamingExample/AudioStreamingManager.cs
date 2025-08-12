#if SCHEDULE_ONE_INTEGRATION && MONO
// Audio streaming is only available on Mono runtime due to OpusSharp limitations with IL2CPP
using MelonLoader;
using SteamNetworkLib;
using SteamNetworkLib.Models;
using SteamNetworkLib.Streaming;
using SteamNetworkLib.Utilities;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AudioStreamingExample
{
    /// <summary>
    /// Main manager for audio streaming that coordinates sending and receiving,
    /// integrates with Unity's audio system, and manages multiple concurrent streams.
    /// </summary>
    public class AudioStreamingManager : IDisposable
    {
        // Audio configuration - optimized for reliability over quality
        public const int SAMPLE_RATE = 48000;
        public const int CHANNELS = 2;
        public const int FRAME_SIZE = 960; // 20ms at 48kHz (standard for music)
        public const int STREAM_FPS = 25; // 25 FPS instead of 50

        private readonly SteamNetworkClient _networkClient;
        private readonly Dictionary<CSteamID, OpusAudioStreamChannel> _incomingStreams = new();
        private readonly Dictionary<CSteamID, AudioSource> _playerAudioSources = new();
        private readonly Dictionary<CSteamID, StreamingAudioBuffer> _playerAudioBuffers = new();
        private readonly Dictionary<string, OpusAudioSender> _outgoingStreams = new();

        // Local audio components
        private AudioSource? _localAudioSource;
        private bool _isDisposed = false;
        
        // Debug tracking
        private uint TotalFramesReceived = 0;

        // Events
        public event Action<CSteamID, string>? OnStreamStarted;
        public event Action<CSteamID, string>? OnStreamEnded;
        public event Action<AudioStreamStats>? OnStreamStatsUpdated;

        public AudioStreamingManager(SteamNetworkClient networkClient)
        {
            // Verify that audio streaming is supported in this runtime environment
            AudioStreamingCompatibility.ThrowIfNotSupported();
            
            _networkClient = networkClient ?? throw new ArgumentNullException(nameof(networkClient));

            SetupNetworking();
            SetupLocalAudio();

            MelonLogger.Msg($"✓ AudioStreamingManager initialized on {AudioStreamingCompatibility.RuntimeType} runtime");
            
            // Log diagnostic information
            LogDiagnosticInfo();
        }
        
        private void LogDiagnosticInfo()
        {
            try
            {
                // Check if message handlers are registered
                MelonLogger.Msg($"[AudioManager] Diagnostic info:");
                
                // Check if we can access the P2P manager
                if (_networkClient != null && _networkClient.P2PManager != null)
                {
                    MelonLogger.Msg($"[AudioManager] P2PManager is active: {_networkClient.P2PManager.IsActive}");
                    
                    // Check if we're in a lobby
                    MelonLogger.Msg($"[AudioManager] In lobby: {_networkClient.IsInLobby}");
                    if (_networkClient.IsInLobby)
                    {
                        var members = _networkClient.GetLobbyMembers();
                        MelonLogger.Msg($"[AudioManager] Lobby members: {members.Count}");
                    }
                }
                else
                {
                    MelonLogger.Error("[AudioManager] P2PManager is null or not accessible");
                }
                
                // Send a test message to verify handlers are working
                MelonLogger.Msg("[AudioManager] Sending test message to self");
                var testMessage = new SteamNetworkLib.Models.TextMessage
                {
                    Content = $"AudioManager test message at {DateTime.UtcNow:HH:mm:ss}"
                };
                
                try
                {
                    // Try to broadcast the message
                    _networkClient.BroadcastMessage(testMessage);
                    MelonLogger.Msg("[AudioManager] Test message broadcast successfully");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AudioManager] Error broadcasting test message: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AudioManager] Error in diagnostic logging: {ex.Message}");
            }
        }

        private void SetupNetworking()
        {
            // Register message handlers
            _networkClient.RegisterMessageHandler<StreamMessage>(OnStreamMessage);
            _networkClient.RegisterMessageHandler<EventMessage>(OnEventMessage);

            // Hook into lobby events
            _networkClient.OnMemberJoined += OnMemberJoined;
            _networkClient.OnMemberLeft += OnMemberLeft;
            
            MelonLogger.Msg("[AudioManager] Network message handlers registered");
        }

        private void SetupLocalAudio()
        {
            var audioGameObject = new GameObject("AudioStreamingManager_LocalAudio");
            GameObject.DontDestroyOnLoad(audioGameObject);
            _localAudioSource = audioGameObject.AddComponent<AudioSource>();
            _localAudioSource.volume = 0.7f;
            _localAudioSource.spatialBlend = 0f; // 2D sound
        }

        /// <summary>
        /// Start streaming an AudioClip to all connected players
        /// </summary>
        public string StartAudioStream(AudioClip clip, string streamId = "default_audio")
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));

            // Stop existing stream with same ID
            if (_outgoingStreams.ContainsKey(streamId))
            {
                StopAudioStream(streamId);
            }

            try
            {
                // Create and configure sender for high-quality music
                var sender = new OpusAudioSender(streamId, SAMPLE_RATE, CHANNELS, FRAME_SIZE, _networkClient);
                sender.Quality = 90; // High quality setting
                sender.Bitrate = 160000; // 160kbps for good music quality

                // Hook up events
                sender.OnStreamStarted += (s) => MelonLogger.Msg($"🎵 Started streaming '{streamId}'");
                sender.OnStreamEnded += (s) => MelonLogger.Msg($"🎵 Finished streaming '{streamId}'");

                _outgoingStreams[streamId] = sender;

                // Start local playback
                if (_localAudioSource != null)
                {
                    _localAudioSource.clip = clip;
                    _localAudioSource.Play();
                }

                // Start network streaming
                sender.StartStreamFromClip(clip);

                MelonLogger.Msg($"🎵 Audio stream '{streamId}' started ({clip.length:F1}s, {clip.frequency}Hz)");
                return streamId;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to start audio stream: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stop an outgoing audio stream
        /// </summary>
        public void StopAudioStream(string streamId)
        {
            if (_outgoingStreams.TryGetValue(streamId, out var sender))
            {
                sender.StopStream();
                sender.Dispose();
                _outgoingStreams.Remove(streamId);

                // Stop local playback if it was playing this stream
                if (_localAudioSource != null && _localAudioSource.isPlaying)
                {
                    _localAudioSource.Stop();
                }

                MelonLogger.Msg($"🎵 Stopped audio stream '{streamId}'");
            }
        }

        /// <summary>
        /// Stop all outgoing streams
        /// </summary>
        public void StopAllAudioStreams()
        {
            var streamIds = _outgoingStreams.Keys.ToList();
            foreach (var streamId in streamIds)
            {
                StopAudioStream(streamId);
            }
        }

        private DateTime _lastStatsReport = DateTime.UtcNow;

        /// <summary>
        /// Must be called regularly (e.g., in Update) to process incoming audio
        /// </summary>
        public void Update()
        {
            if (_isDisposed) return;

            // Update all incoming streams (process jitter buffers)
            foreach (var stream in _incomingStreams.Values)
            {
                stream.Update();
            }
            
            // Update all audio buffers (especially important for IL2CPP)
            foreach (var buffer in _playerAudioBuffers.Values)
            {
                buffer.Update();
            }

            // Periodically report statistics (every 5 seconds)
            if ((DateTime.UtcNow - _lastStatsReport).TotalSeconds >= 5.0)
            {
                ReportStreamingStatistics();
                _lastStatsReport = DateTime.UtcNow;
            }
        }

        private void ReportStreamingStatistics()
        {
            if (_incomingStreams.Count == 0 && _outgoingStreams.Count == 0) return;

            MelonLogger.Msg("=== Audio Streaming Statistics ===");

            // Outgoing streams
            foreach (var kvp in _outgoingStreams)
            {
                var sender = kvp.Value;
                MelonLogger.Msg($"[OUT] {kvp.Key}: {sender.TotalFramesSent} frames sent, {sender.TotalBytesSent} bytes");

                // Check if we need to adjust quality due to receiver feedback
                AdaptQualityBasedOnNetworkConditions(sender);
            }

            // Incoming streams
            foreach (var kvp in _incomingStreams)
            {
                var stats = kvp.Value.GetStats();
                var playerId = kvp.Key;
                var playerName = _networkClient.GetLobbyMembers()
                    .Find(m => m.SteamId == playerId)?.DisplayName ?? "Unknown";

                var lossRate = stats.PacketLossRate * 100;
                var plcRate = stats.PlcUsageRate * 100;

                if (lossRate > 20.0) // High packet loss
                {
                    MelonLogger.Warning($"[IN] {playerName}: HIGH PACKET LOSS! {stats.TotalFramesReceived} frames, {stats.TotalFramesDropped} dropped ({lossRate:F1}% loss), {stats.TotalPlcFramesGenerated} PLC frames ({plcRate:F1}% PLC), buffer: {stats.BufferedFrameCount}");
                }
                else
                {
                    MelonLogger.Msg($"[IN] {playerName}: {stats.TotalFramesReceived} frames, {stats.TotalFramesDropped} dropped ({lossRate:F1}% loss), {stats.TotalPlcFramesGenerated} PLC frames ({plcRate:F1}% PLC), buffer: {stats.BufferedFrameCount}");
                }
            }

            MelonLogger.Msg("=================================");
        }

        private void AdaptQualityBasedOnNetworkConditions(OpusAudioSender sender)
        {
            // Calculate average packet loss from all receivers
            float avgLossRate = 0f;
            int receiverCount = 0;

            foreach (var stream in _incomingStreams.Values)
            {
                var stats = stream.GetStats();
                avgLossRate += stats.PacketLossRate;
                receiverCount++;
            }

            if (receiverCount > 0)
            {
                avgLossRate /= receiverCount;

                // Less aggressive quality adaptation based on loss rate
                if (avgLossRate > 0.25f && sender.Bitrate > 64000) // > 25% loss, reduce
                {
                    sender.Bitrate = Math.Max(64000, sender.Bitrate - 16000);
                    sender.Quality = Math.Max(60, sender.Quality - 10);
                    MelonLogger.Warning($"[QualityAdaptation] CRITICAL packet loss ({avgLossRate * 100:F1}%), reducing bitrate to {sender.Bitrate} and quality to {sender.Quality}");
                }
                else if (avgLossRate > 0.15f && sender.Bitrate > 96000) // > 15% loss, slightly reduce quality
                {
                    sender.Bitrate = Math.Max(96000, sender.Bitrate - 8000);
                    sender.Quality = Math.Max(70, sender.Quality - 5);
                    MelonLogger.Warning($"[QualityAdaptation] High packet loss ({avgLossRate * 100:F1}%), reducing bitrate to {sender.Bitrate} and quality to {sender.Quality}");
                }
                else if (avgLossRate < 0.02f && sender.Bitrate < 192000) // < 2% loss, slowly increase
                {
                    sender.Bitrate = Math.Min(192000, sender.Bitrate + 8000);
                    sender.Quality = Math.Min(100, sender.Quality + 2);
                    MelonLogger.Msg($"[QualityAdaptation] Very low packet loss ({avgLossRate * 100:F1}%), slightly increasing bitrate to {sender.Bitrate} and quality to {sender.Quality}");
                }
            }
        }

        /// <summary>
        /// Get statistics for a specific incoming stream
        /// </summary>
        public AudioStreamStats? GetStreamStats(CSteamID playerId)
        {
            if (_incomingStreams.TryGetValue(playerId, out var stream))
            {
                return stream.GetStats();
            }
            return null;
        }

        /// <summary>
        /// Get statistics for all active streams
        /// </summary>
        public Dictionary<CSteamID, AudioStreamStats> GetAllStreamStats()
        {
            var stats = new Dictionary<CSteamID, AudioStreamStats>();
            foreach (var kvp in _incomingStreams)
            {
                stats[kvp.Key] = kvp.Value.GetStats();
            }
            return stats;
        }

        private void OnStreamMessage(StreamMessage message, CSteamID senderId)
        {
            if (message.StreamType != "audio") return;

#if !MONO
            // IL2CPP-specific debugging
            if (message.StreamData == null || message.StreamData.Length == 0)
            {
                MelonLogger.Error($"[AudioManager] Received empty StreamData from {senderId} (seq: {message.SequenceNumber})");
                return;
            }
            
            // Check for IL2CPP marshalling issues
            bool potentialMarshallIssue = message.StreamData.Length > 4;
            for (int i = 0; i < Math.Min(4, message.StreamData.Length) && potentialMarshallIssue; i++)
            {
                if (message.StreamData[i] != 0) potentialMarshallIssue = false;
            }
            
            if (potentialMarshallIssue)
            {
                MelonLogger.Error($"[AudioManager] IL2CPP marshalling issue detected! StreamData contains all zeros for frame {message.SequenceNumber}");
                return;
            }
            
            // Log the first few bytes for debugging
            if (message.SequenceNumber % 100 == 0)
            {
                var debugBytes = "";
                for (int i = 0; i < Math.Min(message.StreamData.Length, 8); i++)
                {
                    debugBytes += message.StreamData[i].ToString("X2") + " ";
                }
                MelonLogger.Msg($"[AudioManager] Frame {message.SequenceNumber} data: {debugBytes} (length: {message.StreamData.Length})");
            }
#endif

            // Get or create stream channel for this sender
            if (!_incomingStreams.TryGetValue(senderId, out var streamChannel))
            {
                streamChannel = new OpusAudioStreamChannel(message.StreamId, SAMPLE_RATE, CHANNELS, FRAME_SIZE);

                // Configure for more stable buffering with less dropouts
                streamChannel.BufferMs = 120; // Increased buffer for more stability (6 frames at 20ms)
                streamChannel.MaxBufferMs = 300; // Higher maximum buffer during network issues
                streamChannel.EnablePacketLossConcealment = true;
                streamChannel.EnablePacketLossDetection = true;
                streamChannel.EnableJitterBuffer = true;

                // Hook up events
                streamChannel.OnFrameReady += (frameData) => PlayAudioFrame(senderId, frameData);
                streamChannel.OnStreamStarted += (channel) => OnStreamStarted?.Invoke(senderId, message.StreamId);
                streamChannel.OnStreamEnded += (channel) => OnStreamEnded?.Invoke(senderId, message.StreamId);
                streamChannel.OnFrameDropped += (seq) => MelonLogger.Warning($"[AudioManager] Frame {seq} dropped from {senderId}");
                streamChannel.OnFrameLate += (seq, lateness) =>
                {
                    if (lateness.TotalMilliseconds > 60) // Reduced threshold for precision timing
                        MelonLogger.Warning($"[AudioManager] Frame {seq} from {senderId} is {lateness.TotalMilliseconds:F1}ms late");
                };

                _incomingStreams[senderId] = streamChannel;

                // Create audio source for this player if needed
                EnsureAudioSourceForPlayer(senderId);

                MelonLogger.Msg($"[AudioManager] Created audio channel for {senderId} with optimized buffering for precision timing");
            }

            // Process the message
            streamChannel.ProcessStreamMessage(message, senderId);
        }

        private void OnEventMessage(EventMessage message, CSteamID senderId)
        {
            if (message.EventType != "audio_stream") return;

            var senderName = _networkClient.GetLobbyMembers()
                .Find(m => m.SteamId == senderId)?.DisplayName ?? "Unknown";

            switch (message.EventName)
            {
                case "stream_start":
                    MelonLogger.Msg($"🎧 {senderName} started audio streaming");
                    
                    // Create audio channel for this player when we receive the stream_start event
                    // This ensures the audio source and buffer are ready before streaming begins
                    EnsureAudioSourceForPlayer(senderId);
                    
                    // Parse stream configuration from EventData if available
                    try
                    {
                        if (!string.IsNullOrEmpty(message.EventData))
                        {
                            // For now, just log the configuration - we'll use default values
                            MelonLogger.Msg($"[AudioManager] Stream configuration received: {message.EventData}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[AudioManager] Error parsing stream configuration: {ex.Message}");
                    }
                    break;

                case "stream_end":
                    MelonLogger.Msg($"🎧 {senderName} finished audio streaming");
                    break;

                case "stream_stop":
                    MelonLogger.Msg($"🎧 {senderName} stopped audio streaming");
                    break;
            }
        }

        private void OnMemberJoined(object? sender, SteamNetworkLib.Events.MemberJoinedEventArgs e)
        {
            var memberName = e.Member.DisplayName;
            MelonLogger.Msg($"🎵 {memberName} joined - preparing audio channel");

            EnsureAudioSourceForPlayer(e.Member.SteamId);
        }

        private void OnMemberLeft(object? sender, SteamNetworkLib.Events.MemberLeftEventArgs e)
        {
            var memberName = e.Member.DisplayName;
            MelonLogger.Msg($"🎵 {memberName} left - cleaning up audio channel");

            // Clean up incoming stream
            if (_incomingStreams.TryGetValue(e.Member.SteamId, out var stream))
            {
                stream.Dispose();
                _incomingStreams.Remove(e.Member.SteamId);
            }

            // Clean up streaming audio buffer
            if (_playerAudioBuffers.TryGetValue(e.Member.SteamId, out var audioBuffer))
            {
                audioBuffer.Dispose();
                _playerAudioBuffers.Remove(e.Member.SteamId);
            }

            // Clean up audio source
            if (_playerAudioSources.TryGetValue(e.Member.SteamId, out var audioSource))
            {
                if (audioSource != null)
                {
                    GameObject.Destroy(audioSource.gameObject);
                }
                _playerAudioSources.Remove(e.Member.SteamId);
            }
        }

        private void EnsureAudioSourceForPlayer(CSteamID playerId)
        {
            if (_playerAudioSources.ContainsKey(playerId)) return;

            // Try to get player name, but don't hang if lobby data isn't ready yet
            string playerName = "Unknown";
            try
            {
                var lobbyMembers = _networkClient.GetLobbyMembers();
                var member = lobbyMembers.Find(m => m.SteamId == playerId);
                if (member != null)
                {
                    playerName = member.DisplayName ?? $"Player_{playerId}";
                }
                else
                {
                    // Member not found in lobby list yet, use Steam ID as fallback
                    playerName = $"Player_{playerId}";
                    MelonLogger.Msg($"[AudioManager] Member {playerId} not found in lobby members list yet, using fallback name");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AudioManager] Error getting player name for {playerId}: {ex.Message}");
                playerName = $"Player_{playerId}";
            }

            var audioGameObject = new GameObject($"AudioStream_{playerName}");
            GameObject.DontDestroyOnLoad(audioGameObject);
            var audioSource = audioGameObject.AddComponent<AudioSource>();
            audioSource.volume = 0.7f;
            audioSource.spatialBlend = 0f; // 2D sound

            _playerAudioSources[playerId] = audioSource;

            // Create streaming audio buffer for smooth playback
            var streamingBuffer = new StreamingAudioBuffer(audioSource, SAMPLE_RATE, CHANNELS, 1.5f); // 1.5 second buffer
            _playerAudioBuffers[playerId] = streamingBuffer;

            MelonLogger.Msg($"[AudioManager] Created audio channel for {playerName} ({playerId})");
        }

        private void PlayAudioFrame(CSteamID playerId, float[] frameData)
        {
            if (!_playerAudioBuffers.TryGetValue(playerId, out var audioBuffer))
            {
                Console.WriteLine($"[AudioManager] No audio buffer found for player {playerId}");
                return;
            }

            try
            {
                // Increment frame counter for debugging
                TotalFramesReceived++;
                
#if !MONO
                // Debug logging for IL2CPP
                if (frameData != null && frameData.Length > 0)
                {
                    // Calculate average amplitude for debugging
                    float avgAmplitude = 0f;
                    for (int i = 0; i < Math.Min(frameData.Length, 100); i++)
                    {
                        avgAmplitude += Mathf.Abs(frameData[i]);
                    }
                    avgAmplitude /= Math.Min(frameData.Length, 100);
                    
                    // Log periodically to avoid spam
                    if (TotalFramesReceived % 50 == 0)
                    {
                        MelonLogger.Msg($"[AudioManager] Adding frame to buffer: {frameData.Length} samples, avg amplitude: {avgAmplitude:F6}");
                    }
                    
                    // Check if this is the first frame being added
                    if (TotalFramesReceived <= 5)
                    {
                        MelonLogger.Msg($"[AudioManager] First few frames - Frame {TotalFramesReceived}: {frameData.Length} samples, avg amplitude: {avgAmplitude:F6}");
                    }
                }
                else
                {
                    MelonLogger.Error($"[AudioManager] Received null or empty frame data from {playerId}");
                    return;
                }
#endif
                
                // Simply add the frame data to the streaming buffer - much more efficient
                audioBuffer.AddAudioData(frameData);
                
#if !MONO
                // Also manually trigger an update for IL2CPP to ensure smooth playback
                audioBuffer.Update();
#endif
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Error adding audio frame from {playerId}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            // Stop all outgoing streams
            StopAllAudioStreams();

            // Clean up incoming streams
            foreach (var stream in _incomingStreams.Values)
            {
                stream.Dispose();
            }
            _incomingStreams.Clear();

            // Clean up streaming audio buffers
            foreach (var audioBuffer in _playerAudioBuffers.Values)
            {
                audioBuffer.Dispose();
            }
            _playerAudioBuffers.Clear();

            // Clean up audio sources
            foreach (var audioSource in _playerAudioSources.Values)
            {
                if (audioSource != null)
                {
                    GameObject.Destroy(audioSource.gameObject);
                }
            }
            _playerAudioSources.Clear();

            // Clean up local audio
            if (_localAudioSource != null)
            {
                GameObject.Destroy(_localAudioSource.gameObject);
            }

            _isDisposed = true;
        }
    }
}

#else
// Stub implementation for IL2CPP that provides clear error messages

using MelonLoader;
using SteamNetworkLib;
using SteamNetworkLib.Utilities;
using System;

namespace AudioStreamingExample
{
    /// <summary>
    /// Audio streaming example is not available on IL2CPP runtime due to OpusSharp limitations.
    /// This stub implementation provides clear error messages when attempting to use audio streaming on IL2CPP.
    /// 
    /// All other SteamNetworkLib features (lobbies, P2P messaging, file transfer) work perfectly on IL2CPP.
    /// </summary>
    public class AudioStreamingManager : IDisposable
    {
        public AudioStreamingManager(SteamNetworkClient networkClient)
        {
            MelonLogger.Error("Audio streaming is not supported on IL2CPP runtime due to OpusSharp limitations.");
            MelonLogger.Error("Please use a Mono-based game/environment for audio streaming features.");
            MelonLogger.Msg("All other SteamNetworkLib features (lobbies, P2P messaging, file transfer) work perfectly on IL2CPP.");
            
            AudioStreamingCompatibility.ThrowIfNotSupported();
        }

        public void Dispose()
        {
            // Nothing to dispose in the stub implementation
        }
    }
}
#endif