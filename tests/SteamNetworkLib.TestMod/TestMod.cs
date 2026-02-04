using System;
using System.Collections;
using System.IO;
using MelonLoader;
using UnityEngine;
using SteamNetworkLib;
using SteamNetworkLib.Models;

#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif

[assembly: MelonInfo(
    typeof(SteamNetworkLib.TestMod.TestMod),
    "SteamNetworkLib.TestMod",
    "1.0.0",
    "Bars"
)]
[assembly: MelonColor(255, 128, 0, 255)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace SteamNetworkLib.TestMod
{
    /// <summary>
    /// Custom transaction message for testing custom P2P message types.
    /// Uses the built-in JSON serialization helpers from P2PMessage.
    /// </summary>
    public class TransactionMessage : P2PMessage
    {
        public override string MessageType => "TRANSACTION";

        public string TransactionId { get; set; } = string.Empty;
        public string FromPlayer { get; set; } = string.Empty;
        public string ToPlayer { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string Description { get; set; } = string.Empty;

        public override byte[] Serialize()
        {
            // Escape quotes in string properties to prevent JSON parsing issues
            var escapedTransactionId = TransactionId.Replace("\"", "\\\"");
            var escapedFromPlayer = FromPlayer.Replace("\"", "\\\"");
            var escapedToPlayer = ToPlayer.Replace("\"", "\\\"");
            var escapedCurrency = Currency.Replace("\"", "\\\"");
            var escapedDescription = Description.Replace("\"", "\\\"");
            
            var json = $"{{{CreateJsonBase($"\"TransactionId\":\"{escapedTransactionId}\",\"FromPlayer\":\"{escapedFromPlayer}\",\"ToPlayer\":\"{escapedToPlayer}\",\"Amount\":{Amount},\"Currency\":\"{escapedCurrency}\",\"Description\":\"{escapedDescription}\"")}}}";
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public override void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            ParseJsonBase(json);
            
            TransactionId = ExtractJsonValue(json, "TransactionId");
            FromPlayer = ExtractJsonValue(json, "FromPlayer");
            ToPlayer = ExtractJsonValue(json, "ToPlayer");
            
            if (decimal.TryParse(ExtractJsonValue(json, "Amount"), out decimal amount))
                Amount = amount;
            
            Currency = ExtractJsonValue(json, "Currency");
            Description = ExtractJsonValue(json, "Description");
        }
    }

    public class TestMod : MelonMod
    {
        private static readonly MelonLogger.Instance Logger = new("SteamNetworkLib.TestMod");
        
        private SteamNetworkClient? _client;
        private bool _isHost;
        private bool _isClient;
        private CSteamID _lobbyId;
        private bool _initialized = false;
        
        private static readonly string SharedDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp",
            "SteamNetworkLib.TestMod"
        );
        private static readonly string LobbyFile = Path.Combine(SharedDir, "lobby.txt");
        private static readonly string ResultsFile = Path.Combine(SharedDir, "results.txt");
        
        private int _messagesSent = 0;
        private int _messagesReceived = 0;
        private bool _testsPassed = false;
        
        public override void OnInitializeMelon()
        {
            Logger.Msg("===========================================");
            Logger.Msg("SteamNetworkLib Automated Test Mod");
            Logger.Msg("===========================================");
            
            // Parse command line arguments
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--host")
                {
                    _isHost = true;
                    Logger.Msg("Running as HOST");
                }
                else if (args[i] == "--join")
                {
                    _isClient = true;
                    Logger.Msg("Running as CLIENT");
                }
            }
            
            if (!_isHost && !_isClient)
            {
                Logger.Error("No --host or --join argument provided!");
                return;
            }
            
            // Create shared directory
            if (!Directory.Exists(SharedDir))
            {
                Directory.CreateDirectory(SharedDir);
            }
            
            // Clear old results
            if (File.Exists(ResultsFile))
            {
                File.Delete(ResultsFile);
            }
        }
        
        public override void OnUpdate()
        {
            if (!_initialized)
            {
                // Initialization phase
                if (!SteamAPI.Init())
                {
                    return;
                }
                
                var steamId = SteamUser.GetSteamID();
                Logger.Msg($"Steam initialized - SteamID: {steamId.m_SteamID}");
                
                _client = new SteamNetworkClient();
                if (!_client.Initialize())
                {
                    Logger.Error("Failed to initialize SteamNetworkClient!");
                    WriteResults(false, "Failed to initialize SteamNetworkClient");
                    return;
                }
                
                Logger.Msg("SteamNetworkClient initialized successfully");
                
                // Register message handlers
                Logger.Msg("[DEBUG] Registering message handlers...");
                _client.RegisterMessageHandler<TextMessage>(OnTextMessageReceived);
                Logger.Msg("[DEBUG] TextMessage handler registered");
                _client.RegisterMessageHandler<DataSyncMessage>(OnDataSyncMessageReceived);
                Logger.Msg("[DEBUG] DataSyncMessage handler registered");
                _client.RegisterMessageHandler<TransactionMessage>(OnTransactionMessageReceived);
                Logger.Msg("[DEBUG] TransactionMessage handler registered");
                
                _initialized = true;
                
                if (_isHost)
                {
                    MelonCoroutines.Start(RunHostTests());
                }
                else if (_isClient)
                {
                    MelonCoroutines.Start(RunClientTests());
                }
            }
            else
            {
                // Process incoming P2P packets every frame
                _client?.ProcessIncomingMessages();
            }
        }
        
        private IEnumerator RunHostTests()
        {
            Logger.Msg("========================================");
            Logger.Msg("HOST: Starting automated tests...");
            Logger.Msg("========================================");
            
            // Test 1: Create lobby
            Logger.Msg("[Test 1/5] Creating lobby...");
            var createTask = _client!.CreateLobbyAsync(ELobbyType.k_ELobbyTypePrivate, 4);
            while (!createTask.IsCompleted)
            {
                yield return null;
            }
            
            if (createTask.IsFaulted)
            {
                Logger.Error($"[FAIL] Failed to create lobby: {createTask.Exception?.GetBaseException().Message}");
                WriteResults(false, "Failed to create lobby");
                Application.Quit();
                yield break;
            }
            
            var lobbyInfo = createTask.Result;
            _lobbyId = lobbyInfo.LobbyId;
            Logger.Msg($"[PASS] Lobby created: {_lobbyId}");
            
            // Write lobby ID to shared file
            File.WriteAllText(LobbyFile, _lobbyId.m_SteamID.ToString());
            Logger.Msg($"Lobby ID written to: {LobbyFile}");
            
            // Test 2: Wait for client to join
            Logger.Msg("[Test 2/5] Waiting for client to join...");
            float timeout = 30f;
            float elapsed = 0f;
            int memberCount = 1;
            
            while (memberCount < 2 && elapsed < timeout)
            {
                memberCount = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (memberCount < 2)
            {
                Logger.Error("[FAIL] Client failed to join lobby within 30 seconds");
                WriteResults(false, "Client join timeout");
                Application.Quit();
                yield break;
            }
            
            Logger.Msg($"[PASS] Client joined! Lobby now has {memberCount} members");
            
            // Get client Steam ID
            CSteamID clientId = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyId, 1);
            Logger.Msg($"Client SteamID: {clientId}");
            
            // Wait a bit for client to fully initialize
            Logger.Msg("[DEBUG] Waiting 5 seconds for client to initialize P2P...");
            yield return new WaitForSeconds(5f);
            
            // Test 3: Send TextMessage to client
            Logger.Msg("[Test 3/5] Sending TextMessage to client...");
            Logger.Msg($"[DEBUG] Creating TextMessage with SenderId: {SteamUser.GetSteamID().m_SteamID}");
            var textMsg = new TextMessage
            {
                Content = "Hello from Host!",
                SenderId = (CSteamID)SteamUser.GetSteamID().m_SteamID
            };
            
            Logger.Msg($"[DEBUG] TextMessage created - Content: '{textMsg.Content}', SenderId: {textMsg.SenderId.m_SteamID}");
            
            Exception? sendException = null;
            Logger.Msg($"[DEBUG] Calling SendMessageToPlayerAsync to client {clientId}...");
            var sendTask3 = _client.SendMessageToPlayerAsync(clientId, textMsg);
            while (!sendTask3.IsCompleted)
            {
                yield return null;
            }
            
            if (sendTask3.IsFaulted)
            {
                sendException = sendTask3.Exception?.GetBaseException();
                Logger.Error($"[DEBUG] SendMessageToPlayerAsync failed: {sendException?.Message}");
                Logger.Error($"[DEBUG] Exception type: {sendException?.GetType().Name}");
                if (sendException?.InnerException != null)
                {
                    Logger.Error($"[DEBUG] Inner exception: {sendException.InnerException.Message}");
                }
            }
            
            if (sendException != null)
            {
                Logger.Error($"[FAIL] Failed to send TextMessage: {sendException.Message}");
                WriteResults(false, $"Failed to send TextMessage: {sendException.Message}");
                Application.Quit();
                yield break;
            }
            
            _messagesSent++;
            Logger.Msg("[PASS] TextMessage sent successfully");
            
            // Test 4: Send DataSyncMessage to client
            Logger.Msg("[Test 4/6] Sending DataSyncMessage to client...");
            var dataMsg = new DataSyncMessage
            {
                Key = "test_key",
                Value = "test_value",
                DataType = "string",
                SenderId = (CSteamID)SteamUser.GetSteamID().m_SteamID
            };
            
            sendException = null;
            var sendTask4 = _client.SendMessageToPlayerAsync(clientId, dataMsg);
            while (!sendTask4.IsCompleted)
            {
                yield return null;
            }
            
            if (sendTask4.IsFaulted)
            {
                sendException = sendTask4.Exception?.GetBaseException();
            }
            
            if (sendException != null)
            {
                Logger.Error($"[FAIL] Failed to send DataSyncMessage: {sendException.Message}");
                WriteResults(false, $"Failed to send DataSyncMessage: {sendException.Message}");
                Application.Quit();
                yield break;
            }
            
            _messagesSent++;
            Logger.Msg("[PASS] DataSyncMessage sent successfully");
            
            // Test 5: Send custom TransactionMessage to client
            Logger.Msg("[Test 5/6] Sending custom TransactionMessage to client...");
            var txnMsg = new TransactionMessage
            {
                TransactionId = $"txn_host_{DateTime.UtcNow.Ticks}",
                FromPlayer = "HostPlayer",
                ToPlayer = "ClientPlayer",
                Amount = 99.99m,
                Currency = "USD",
                Description = "Test transaction from host"
            };
            
            sendException = null;
            var sendTask5 = _client.SendMessageToPlayerAsync(clientId, txnMsg);
            while (!sendTask5.IsCompleted)
            {
                yield return null;
            }
            
            if (sendTask5.IsFaulted)
            {
                sendException = sendTask5.Exception?.GetBaseException();
            }
            
            if (sendException != null)
            {
                Logger.Error($"[FAIL] Failed to send TransactionMessage: {sendException.Message}");
                WriteResults(false, $"Failed to send TransactionMessage: {sendException.Message}");
                Application.Quit();
                yield break;
            }
            
            _messagesSent++;
            Logger.Msg($"[PASS] TransactionMessage sent successfully: {txnMsg.TransactionId}");
            
            // Test 6: Wait for client responses
            Logger.Msg("[Test 6/6] Waiting for client responses...");
            timeout = 15f;
            elapsed = 0f;
            
            while (_messagesReceived < 3 && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (_messagesReceived < 3)
            {
                Logger.Error($"[FAIL] Only received {_messagesReceived}/3 expected messages from client");
                WriteResults(false, $"Only received {_messagesReceived}/3 messages");
                Application.Quit();
                yield break;
            }
            
            Logger.Msg($"[PASS] Received all {_messagesReceived} messages from client");
            
            // All tests passed!
            Logger.Msg("========================================");
            Logger.Msg("ALL TESTS PASSED!");
            Logger.Msg($"Messages Sent: {_messagesSent}");
            Logger.Msg($"Messages Received: {_messagesReceived}");
            Logger.Msg("========================================");
            
            _testsPassed = true;
            WriteResults(true, "All tests passed");
            
            yield return new WaitForSeconds(2f);
            Application.Quit();
        }
        
        private IEnumerator RunClientTests()
        {
            Logger.Msg("========================================");
            Logger.Msg("CLIENT: Starting automated tests...");
            Logger.Msg("========================================");
            
            // Test 1: Wait for lobby file
            Logger.Msg("[Test 1/4] Waiting for lobby file...");
            float timeout = 30f;
            float elapsed = 0f;
            
            while (!File.Exists(LobbyFile) && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (!File.Exists(LobbyFile))
            {
                Logger.Error("[FAIL] Lobby file not found within 30 seconds");
                WriteResults(false, "Lobby file timeout");
                Application.Quit();
                yield break;
            }
            
            string lobbyIdStr = File.ReadAllText(LobbyFile);
            if (!ulong.TryParse(lobbyIdStr, out ulong lobbyId))
            {
                Logger.Error($"[FAIL] Invalid lobby ID in file: {lobbyIdStr}");
                WriteResults(false, "Invalid lobby ID");
                Application.Quit();
                yield break;
            }
            
            _lobbyId = new CSteamID(lobbyId);
            Logger.Msg($"[PASS] Found lobby ID: {_lobbyId}");
            
            // Test 2: Join lobby
            Logger.Msg("[Test 2/4] Joining lobby...");
            var joinTask = _client!.JoinLobbyAsync(_lobbyId);
            while (!joinTask.IsCompleted)
            {
                yield return null;
            }
            
            if (joinTask.IsFaulted)
            {
                Logger.Error($"[FAIL] Failed to join lobby: {joinTask.Exception?.GetBaseException().Message}");
                WriteResults(false, "Failed to join lobby");
                Application.Quit();
                yield break;
            }
            
            Logger.Msg("[PASS] Joined lobby successfully");
            
            // Get host Steam ID
            CSteamID hostId = SteamMatchmaking.GetLobbyOwner(_lobbyId);
            Logger.Msg($"Host SteamID: {hostId}");
            
            // Wait a bit for host to be ready
            yield return new WaitForSeconds(2f);
            
            // Test 3: Wait for messages from host
            Logger.Msg("[Test 3/6] Waiting for messages from host...");
            Logger.Msg("[DEBUG] Client message handlers should be active. Waiting for P2P messages...");
            timeout = 30f;  // Increased timeout for debugging
            elapsed = 0f;
            float lastLogTime = 0f;
            
            while (_messagesReceived < 3 && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                
                // Log every 5 seconds to show we're still waiting
                if (elapsed - lastLogTime >= 5f)
                {
                    Logger.Msg($"[DEBUG] Still waiting... Elapsed: {elapsed:F1}s, Messages received: {_messagesReceived}/3");
                    lastLogTime = elapsed;
                }
                
                yield return null;
            }
            
            if (_messagesReceived < 3)
            {
                Logger.Error($"[FAIL] Only received {_messagesReceived}/3 expected messages from host");
                Logger.Error("[DEBUG] Timeout reached. Message handlers may not be receiving P2P packets.");
                WriteResults(false, $"Only received {_messagesReceived}/3 messages");
                Application.Quit();
                yield break;
            }
            
            Logger.Msg($"[PASS] Received all {_messagesReceived} messages from host");
            
            // Test 4: Send responses back to host
            Logger.Msg("[Test 4/6] Sending responses to host...");
            
            var textMsg = new TextMessage
            {
                Content = "Hello from Client!",
                SenderId = (CSteamID)SteamUser.GetSteamID().m_SteamID
            };
            
            Exception? sendException = null;
            var sendTask4 = _client.SendMessageToPlayerAsync(hostId, textMsg);
            while (!sendTask4.IsCompleted)
            {
                yield return null;
            }
            
            if (sendTask4.IsFaulted)
            {
                sendException = sendTask4.Exception?.GetBaseException();
            }
            
            if (sendException != null)
            {
                Logger.Error($"[FAIL] Failed to send TextMessage: {sendException.Message}");
                WriteResults(false, $"Failed to send TextMessage: {sendException.Message}");
                Application.Quit();
                yield break;
            }
            
            _messagesSent++;
            Logger.Msg("[PASS] TextMessage sent to host");
            
            var dataMsg = new DataSyncMessage
            {
                Key = "client_key",
                Value = "client_value",
                DataType = "string",
                SenderId = (CSteamID)SteamUser.GetSteamID().m_SteamID
            };
            
            sendException = null;
            var sendTask5 = _client.SendMessageToPlayerAsync(hostId, dataMsg);
            while (!sendTask5.IsCompleted)
            {
                yield return null;
            }
            
            if (sendTask5.IsFaulted)
            {
                sendException = sendTask5.Exception?.GetBaseException();
            }
            
            if (sendException != null)
            {
                Logger.Error($"[FAIL] Failed to send DataSyncMessage: {sendException.Message}");
                WriteResults(false, $"Failed to send DataSyncMessage: {sendException.Message}");
                Application.Quit();
                yield break;
            }
            
            _messagesSent++;
            Logger.Msg("[PASS] DataSyncMessage sent to host");
            
            // Test 5: Send custom TransactionMessage to host
            Logger.Msg("[Test 5/6] Sending custom TransactionMessage to host...");
            var txnMsg = new TransactionMessage
            {
                TransactionId = $"txn_client_{DateTime.UtcNow.Ticks}",
                FromPlayer = "ClientPlayer",
                ToPlayer = "HostPlayer",
                Amount = 42.50m,
                Currency = "EUR",
                Description = "Test transaction from client"
            };
            
            sendException = null;
            var sendTask6 = _client.SendMessageToPlayerAsync(hostId, txnMsg);
            while (!sendTask6.IsCompleted)
            {
                yield return null;
            }
            
            if (sendTask6.IsFaulted)
            {
                sendException = sendTask6.Exception?.GetBaseException();
            }
            
            if (sendException != null)
            {
                Logger.Error($"[FAIL] Failed to send TransactionMessage: {sendException.Message}");
                WriteResults(false, $"Failed to send TransactionMessage: {sendException.Message}");
                Application.Quit();
                yield break;
            }
            
            _messagesSent++;
            Logger.Msg($"[PASS] TransactionMessage sent to host: {txnMsg.TransactionId}");
            
            // Test 6: Wait for host acknowledgment
            Logger.Msg("[Test 6/6] Waiting for final acknowledgment...");
            yield return new WaitForSeconds(2f);
            
            // All tests passed!
            Logger.Msg("========================================");
            Logger.Msg("ALL CLIENT TESTS PASSED!");
            Logger.Msg($"Messages Sent: {_messagesSent}");
            Logger.Msg($"Messages Received: {_messagesReceived}");
            Logger.Msg("========================================");
            
            _testsPassed = true;
            WriteResults(true, "All client tests passed");
            
            yield return new WaitForSeconds(2f);
            Application.Quit();
        }
        
        private void OnTextMessageReceived(TextMessage message, CSteamID sender)
        {
            _messagesReceived++;
            Logger.Msg($"[MESSAGE RECEIVED] TextMessage from {sender.m_SteamID}: '{message.Content}'");
            Logger.Msg($"[MESSAGE DETAILS] SenderId: {message.SenderId.m_SteamID}, Timestamp: {message.Timestamp}");
        }

        private void OnDataSyncMessageReceived(DataSyncMessage message, CSteamID sender)
        {
            _messagesReceived++;
            Logger.Msg($"[MESSAGE RECEIVED] DataSyncMessage from {sender.m_SteamID}: Key='{message.Key}', Value='{message.Value}'");
            Logger.Msg($"[MESSAGE DETAILS] DataType: {message.DataType}, SenderId: {message.SenderId.m_SteamID}");
        }

        private void OnTransactionMessageReceived(TransactionMessage message, CSteamID sender)
        {
            _messagesReceived++;
            Logger.Msg($"[MESSAGE RECEIVED] TransactionMessage from {sender.m_SteamID}:");
            Logger.Msg($"[MESSAGE DETAILS] TransactionId: {message.TransactionId}");
            Logger.Msg($"[MESSAGE DETAILS] From: {message.FromPlayer} -> To: {message.ToPlayer}");
            Logger.Msg($"[MESSAGE DETAILS] Amount: {message.Amount} {message.Currency}");
            Logger.Msg($"[MESSAGE DETAILS] Description: {message.Description}");
            Logger.Msg($"[MESSAGE DETAILS] SenderId: {message.SenderId.m_SteamID}, Timestamp: {message.Timestamp}");
        }
        
        private void WriteResults(bool passed, string details)
        {
            try
            {
                var role = _isHost ? "HOST" : "CLIENT";
                var result = $"{role}|{(passed ? "PASS" : "FAIL")}|{details}|Sent:{_messagesSent}|Received:{_messagesReceived}";
                File.WriteAllText(ResultsFile, result);
                Logger.Msg($"Results written to: {ResultsFile}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to write results: {ex.Message}");
            }
        }
        
        public override void OnApplicationQuit()
        {
            if (!_testsPassed && _initialized)
            {
                WriteResults(false, "Application quit before tests completed");
            }
            
            if (_lobbyId.IsValid())
            {
                SteamMatchmaking.LeaveLobby(_lobbyId);
            }
            
            _client?.Dispose();
            SteamAPI.Shutdown();
        }
    }
}
