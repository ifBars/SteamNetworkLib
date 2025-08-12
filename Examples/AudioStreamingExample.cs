#if SCHEDULE_ONE_INTEGRATION
using AudioStreamingExample;
using MelonLoader;
using MelonLoader.Utils;
using SteamNetworkLib;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

[assembly: MelonInfo(typeof(ScheduleOneAudioStreaming.ScheduleOneAudioStreaming), "ScheduleOne-AudioStreaming", "2.0.0", "SteamNetworkLib")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleOneAudioStreaming
{
    /// <summary>
    /// Advanced example showing real-time audio streaming between players using SteamNetworkLib with Opus compression.
    /// Now uses the new object-oriented streaming architecture for improved quality, reliability, and extensibility.
    /// Automatically loads song.mp3 from mods directory and streams it to lobby members.
    /// </summary>
    public class ScheduleOneAudioStreaming : MelonMod
    {
        private SteamNetworkClient? _networkClient;
        private AudioStreamingManager? _audioStreamingManager;
        private AudioClip? _loadedSong;
        private bool _hasSong = false;

        private readonly string _modsDirectory = MelonEnvironment.ModsDirectory;
        private readonly string _songPath;

        public ScheduleOneAudioStreaming()
        {
            _songPath = Path.Combine(_modsDirectory, "song.mp3");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName != "Menu" || _networkClient != null) return;
            MelonLogger.Msg("Initializing Advanced Audio Streaming for ScheduleOne...");

            // Initialize SteamNetworkLib
            _networkClient = new SteamNetworkClient();
            if (_networkClient.Initialize())
            {
                // Enable P2P packet relay for NAT traversal
                SteamNetworking.AllowP2PPacketRelay(true);

                SetupNetworking();

                // Initialize audio streaming manager
                _audioStreamingManager = new AudioStreamingManager(_networkClient);
                _audioStreamingManager.OnStreamStarted += OnRemoteStreamStarted;
                _audioStreamingManager.OnStreamEnded += OnRemoteStreamEnded;

                MelonLogger.Msg("✓ Advanced audio streaming ready!");
            }

            // Load song if available
            MelonCoroutines.Start(LoadSongCoroutine());
        }

        private void SetupNetworking()
        {
            if (_networkClient == null) return;

            // Hook into SteamNetworkLib's lobby events
            _networkClient.OnMemberJoined += OnMemberJoined;
            _networkClient.OnMemberLeft += OnMemberLeft;
            _networkClient.OnLobbyJoined += OnLobbyJoined;
            
            // Register handlers for P2P messages
            _networkClient.RegisterMessageHandler<SteamNetworkLib.Models.TextMessage>(OnTextMessageReceived);
            _networkClient.RegisterMessageHandler<SteamNetworkLib.Models.StreamMessage>(OnStreamMessageReceived);
            
            MelonLogger.Msg("[DEBUG] Network message handlers registered");
        }

        private IEnumerator LoadSongCoroutine()
        {
            if (!File.Exists(_songPath))
            {
                MelonLogger.Msg($"No song.mp3 found at: {_songPath}");
                MelonLogger.Msg("Place a song.mp3 file in your mods directory to stream music!");
                yield break;
            }

            MelonLogger.Msg($"Loading song from: {_songPath}");

#if MONO
            // Load audio file using UnityWebRequest for Mono
            string fileUrl = "file://" + _songPath.Replace("\\", "/");
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    _loadedSong = DownloadHandlerAudioClip.GetContent(www);
                    _hasSong = true;
                    MelonLogger.Msg($"✓ Song loaded successfully! Duration: {_loadedSong.length:F1}s");
                    MelonLogger.Msg($"✓ Sample Rate: {_loadedSong.frequency}Hz, Channels: {_loadedSong.channels}");
                }
                else
                {
                    MelonLogger.Error($"Failed to load song: {www.error}");
                }
            }
#else
            // Use AudioImportLib for IL2CPP - much simpler and more reliable
            try
            {
                _loadedSong = AudioImportLib.API.LoadAudioClip(_songPath, true);
                if (_loadedSong != null)
                {
                    _hasSong = true;
                    MelonLogger.Msg($"✓ Song loaded successfully with AudioImportLib! Duration: {_loadedSong.length:F1}s");
                    MelonLogger.Msg($"✓ Sample Rate: {_loadedSong.frequency}Hz, Channels: {_loadedSong.channels}");
                }
                else
                {
                    MelonLogger.Error("Failed to load song with AudioImportLib - returned null");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Exception loading song with AudioImportLib: {ex.Message}");
            }
            
            // Small delay to ensure proper loading
            yield return new WaitForSeconds(0.1f);
#endif
        }

        private void OnLobbyJoined(object? sender, SteamNetworkLib.Events.LobbyJoinedEventArgs e)
        {
            if (_networkClient == null) return;

            MelonLogger.Msg($"Joined lobby with {e.Lobby.MemberCount} members");

            // Set our audio capability status
            _networkClient.SetMyData("has_song", _hasSong.ToString());
            _networkClient.SetMyData("stream_ready", "true");
            _networkClient.SetMyData("opus_support", "true");

            // Check streaming availability (no auto-start)
            CheckAndStartStreaming();
            
            // Show controls information
            if (_hasSong)
            {
                MelonLogger.Msg("🎵 Controls: Press 'M' to start/stop streaming, 'N' to stop only");
            }
        }

        private void OnMemberJoined(object? sender, SteamNetworkLib.Events.MemberJoinedEventArgs e)
        {
            if (_networkClient == null) return;

            var memberName = e.Member.DisplayName;
            MelonLogger.Msg($"🎵 {memberName} joined the lobby!");

            // Give Steam a moment to sync member data, then check streaming status
            MelonCoroutines.Start(DelayedStreamingCheck(1.0f));
        }

        private IEnumerator DelayedStreamingCheck(float delay)
        {
            yield return new WaitForSeconds(delay);
            CheckAndStartStreaming();
        }

        private void OnMemberLeft(object? sender, SteamNetworkLib.Events.MemberLeftEventArgs e)
        {
            if (_networkClient == null) return;

            var memberName = e.Member.DisplayName;
            MelonLogger.Msg($"🎵 {memberName} left the lobby");

            // Stop streaming if we're alone now
            if (_networkClient.GetLobbyMembers().Count <= 1 && _audioStreamingManager != null && _hasSong)
            {
                MelonLogger.Msg("🎵 Stopping stream - no other players in lobby");
                _audioStreamingManager.StopAllAudioStreams();
            }
        }

        private void CheckAndStartStreaming()
        {
            if (_networkClient == null || _audioStreamingManager == null) return;

            var lobbyMembers = _networkClient.GetLobbyMembers();

            // Check if we can stream, but don't auto-start
            if (_hasSong && lobbyMembers.Count > 1)
            {
                var playersWithSong = GetPlayersWithSong();

                if (playersWithSong.Count == 1) // Only we have it
                {
                    MelonLogger.Msg($"🎵 You have the song and there are {lobbyMembers.Count} players!");
                    MelonLogger.Msg($"🎵 Press 'M' to start streaming music!");
                }
            }
            else if (lobbyMembers.Count <= 1)
            {
                MelonLogger.Msg("🎵 Waiting for other players to join before starting music...");
            }
        }

        private List<CSteamID> GetPlayersWithSong()
        {
            var allPlayerData = _networkClient?.GetDataForAllPlayers("has_song") ?? new Dictionary<CSteamID, string>();

            var playersWithSong = allPlayerData
                .Where(kvp => kvp.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToList();

            return playersWithSong;
        }

        private IEnumerator StartStreamingWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            StartStreaming();
        }

        private void StartStreaming()
        {
            if (_loadedSong == null || _audioStreamingManager == null) return;

            MelonLogger.Msg("🎵 Starting high-quality audio stream...");

            // Start streaming using the new manager
            _audioStreamingManager.StartAudioStream(_loadedSong, "lobby_music_stream");
        }

        private void OnRemoteStreamStarted(CSteamID playerId, string streamId)
        {
            var playerName = _networkClient?.GetLobbyMembers()
                .Find(m => m.SteamId == playerId)?.DisplayName ?? "Unknown";
            MelonLogger.Msg($"🎧 {playerName} started streaming audio");
        }

        private void OnRemoteStreamEnded(CSteamID playerId, string streamId)
        {
            var playerName = _networkClient?.GetLobbyMembers()
                .Find(m => m.SteamId == playerId)?.DisplayName ?? "Unknown";
            MelonLogger.Msg($"🎧 {playerName} finished streaming audio");
        }

        public override void OnUpdate()
        {
#if IL2CPP
            // CRITICAL: Must call SteamAPI.RunCallbacks() in IL2CPP to process Steam callbacks
            // This is what actually triggers P2P packet reception callbacks
            try
            {
                SteamAPI.RunCallbacks();
                
                // Add periodic debug logging
                if (Time.frameCount % 300 == 0) // Every ~5 seconds at 60fps
                {
                    // Check if we're in a lobby
                    if (_networkClient != null && _networkClient.IsInLobby)
                    {
                        var lobbyMembers = _networkClient.GetLobbyMembers();
                        MelonLogger.Msg($"[DEBUG] In lobby with {lobbyMembers.Count} members");
                        
                        // Check P2P session state with each member
                        foreach (var member in lobbyMembers)
                        {
                            if (member.SteamId != _networkClient.LocalPlayerId)
                            {
                                try
                                {
                                    var sessionState = _networkClient.P2PManager.GetSessionState(member.SteamId);
                                    MelonLogger.Msg($"[DEBUG] P2P session with {member.DisplayName}: " +
                                        $"Connected={sessionState.m_bConnectionActive != 0}, " +
                                        $"Using Relay={sessionState.m_bUsingRelay != 0}, " +
                                        $"BytesQueued={sessionState.m_nBytesQueuedForSend}");
                                    
                                    // Try to ensure we have an active session
                                    if (sessionState.m_bConnectionActive == 0)
                                    {
                                        MelonLogger.Msg($"[DEBUG] Accepting P2P session with {member.DisplayName}");
                                        _networkClient.P2PManager.AcceptSession(member.SteamId);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Error($"[DEBUG] Error checking P2P session: {ex.Message}");
                                }
                            }
                        }
                        
                        // Check if audio streaming manager is initialized
                        if (_audioStreamingManager != null)
                        {
                            MelonLogger.Msg($"[DEBUG] AudioStreamingManager initialized: {_audioStreamingManager != null}");
                            var stats = _audioStreamingManager.GetAllStreamStats();
                            MelonLogger.Msg($"[DEBUG] Active streams: {stats.Count}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error running Steam callbacks: {ex.Message}");
            }
#endif
            // Process P2P messages for real-time streaming
            _networkClient?.ProcessIncomingMessages();

            // Update audio streaming manager (processes jitter buffers, etc.)
            _audioStreamingManager?.Update();

            // Handle manual streaming controls
            HandleStreamingControls();
        }

        private void HandleStreamingControls()
        {
            if (_networkClient == null || _audioStreamingManager == null) return;

            // Check for manual stream start (M key)
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (!IsStreamingActive() && CanStartStreaming())
                {
                    MelonLogger.Msg("🎵 Manual stream start requested!");
                    StartManualStream();
                }
                else if (IsStreamingActive())
                {
                    MelonLogger.Msg("🎵 Manual stream stop requested!");
                    StopStream();
                }
                else
                {
                    MelonLogger.Msg("🎵 Cannot start streaming - need song and other players in lobby!");
                }
            }

            // Check for stop stream (N key)
            if (Input.GetKeyDown(KeyCode.N))
            {
                if (IsStreamingActive())
                {
                    MelonLogger.Msg("🎵 Stopping stream...");
                    StopStream();
                }
            }
            
            // Check for test P2P packet (T key)
            if (Input.GetKeyDown(KeyCode.T))
            {
                SendTestP2PPacket();
            }
            
#if IL2CPP
            // In IL2CPP, periodically send test packets to verify P2P communication
            if (Time.frameCount % 600 == 300) // Every ~10 seconds, offset from the debug logs
            {
                if (_networkClient != null && _networkClient.IsInLobby)
                {
                    SendTestP2PPacket();
                }
            }
#endif
        }
        
        private void SendTestP2PPacket()
        {
            if (_networkClient == null || !_networkClient.IsInLobby) return;
            
            var lobbyMembers = _networkClient.GetLobbyMembers();
            foreach (var member in lobbyMembers)
            {
                if (member.SteamId != _networkClient.LocalPlayerId)
                {
                    try
                    {
                        // Send a test text message
                        var testMessage = new SteamNetworkLib.Models.TextMessage
                        {
                            Content = $"Test P2P message from {_networkClient.LocalPlayerId} at {DateTime.Now:HH:mm:ss}"
                        };
                        
                        MelonLogger.Msg($"[DEBUG] Sending test P2P message to {member.DisplayName}");
                        _networkClient.SendMessageToPlayerAsync(member.SteamId, testMessage).ContinueWith(task =>
                        {
                            if (task.Result)
                            {
                                MelonLogger.Msg($"[DEBUG] Test P2P message sent successfully to {member.DisplayName}");
                            }
                            else
                            {
                                MelonLogger.Error($"[DEBUG] Failed to send test P2P message to {member.DisplayName}");
                            }
                        });
                        
                        // Also try sending a test packet using the P2PManager directly
                        bool success = _networkClient.P2PManager.SendTestPacket(member.SteamId);
                        MelonLogger.Msg($"[DEBUG] Direct test packet sent: {success}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[DEBUG] Error sending test P2P packet: {ex.Message}");
                    }
                }
            }
        }

        private bool CanStartStreaming()
        {
            if (!_hasSong) return false;
            
            var lobbyMembers = _networkClient?.GetLobbyMembers();
            if (lobbyMembers == null || lobbyMembers.Count <= 1) return false;

            var playersWithSong = GetPlayersWithSong();
            return playersWithSong.Count == 1; // Only we have the song
        }

        private bool IsStreamingActive()
        {
            return _audioStreamingManager?.GetAllStreamStats().Count > 0;
        }

        public override void OnApplicationQuit()
        {
            // Dispose audio streaming manager (handles all cleanup)
            _audioStreamingManager?.Dispose();

            _networkClient?.Dispose();
        }

        // Public API for manual control
        public void StartManualStream()
        {
            if (_hasSong)
            {
                StartStreaming();
            }
            else
            {
                MelonLogger.Warning("No song loaded to stream!");
            }
        }

        public void StopStream()
        {
            _audioStreamingManager?.StopAllAudioStreams();
            MelonLogger.Msg("🎵 Audio streaming stopped");
        }

        private void OnTextMessageReceived(SteamNetworkLib.Models.TextMessage message, CSteamID senderId)
        {
            // This handler is just to verify P2P communication is working
            var senderName = _networkClient?.GetLobbyMembers()
                .Find(m => m.SteamId == senderId)?.DisplayName ?? "Unknown";
                
            MelonLogger.Msg($"[DEBUG] Received text message from {senderName}: {message.Content}");
        }
        
        private void OnStreamMessageReceived(SteamNetworkLib.Models.StreamMessage message, CSteamID senderId)
        {
            // This is just for debugging - the AudioStreamingManager already handles stream messages
            var senderName = _networkClient?.GetLobbyMembers()
                .Find(m => m.SteamId == senderId)?.DisplayName ?? "Unknown";
                
            MelonLogger.Msg($"[DEBUG] Received stream message from {senderName}: Seq={message.SequenceNumber}, Type={message.StreamType}, DataSize={message.StreamData?.Length ?? 0}");
        }
    }


}
#endif