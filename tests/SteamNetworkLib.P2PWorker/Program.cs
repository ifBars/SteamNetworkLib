using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SteamNetworkLib;
using SteamNetworkLib.Models;
using SteamNetworkLib.Utilities;
using Steamworks;

namespace SteamNetworkLib.P2PWorker
{
    /// <summary>
    /// Worker process for multi-process P2P testing.
    /// Runs as either host or client, coordinating via shared files.
    /// </summary>
    class Program
    {
        private static string? _role;
        private static ulong _steamId;
        private static string? _playerName;
        private static int _listenPort;
        private static string? _sharedDir;
        private static string? _testCase;
        private static SteamNetworkClient? _client;
        private static bool _running = true;
        private static int _exitCode = 0;

        static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine($"[P2PWorker] Starting with args: {string.Join(" ", args)}");
                
                if (!ParseArguments(args))
                {
                    Console.Error.WriteLine("Usage: P2PWorker --role <host|client> --steamId <id> --name <name> --listenPort <port> --sharedDir <path> --testCase <id>");
                    return 1;
                }

                Console.WriteLine($"[{_playerName}] Role: {_role}, SteamID: {_steamId}, Port: {_listenPort}");
                Console.WriteLine($"[{_playerName}] SharedDir: {_sharedDir}, TestCase: {_testCase}");

                // Initialize Goldberg with instance-specific settings
                if (!InitializeGoldberg())
                {
                    Console.Error.WriteLine($"[{_playerName}] Failed to initialize Goldberg");
                    return 1;
                }

                // Initialize SteamNetworkLib client
                _client = new SteamNetworkClient();
                if (!_client.Initialize())
                {
                    Console.Error.WriteLine($"[{_playerName}] Failed to initialize SteamNetworkClient");
                    return 1;
                }

                Console.WriteLine($"[{_playerName}] Initialized successfully");

                // Start callback processing task
                var callbackTask = Task.Run(ProcessCallbacks);

                // Run role-specific logic
                if (_role == "host")
                {
                    await RunHostAsync();
                }
                else
                {
                    await RunClientAsync();
                }

                // Cleanup
                _running = false;
                await callbackTask;
                
                Console.WriteLine($"[{_playerName}] Exiting with code {_exitCode}");
                return _exitCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{_playerName}] Fatal error: {ex}");
                return 1;
            }
            finally
            {
                _client?.Dispose();
                SteamAPI.Shutdown();
            }
        }

        static bool ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--role":
                        _role = args[++i];
                        break;
                    case "--steamId":
                        _steamId = ulong.Parse(args[++i]);
                        break;
                    case "--name":
                        _playerName = args[++i];
                        break;
                    case "--listenPort":
                        _listenPort = int.Parse(args[++i]);
                        break;
                    case "--sharedDir":
                        _sharedDir = args[++i];
                        break;
                    case "--testCase":
                        _testCase = args[++i];
                        break;
                }
            }

            return _role != null && _steamId != 0 && _playerName != null && 
                   _listenPort > 0 && _sharedDir != null && _testCase != null;
        }

        static bool InitializeGoldberg()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // Create steam_appid.txt
                File.WriteAllText(Path.Combine(baseDir, "steam_appid.txt"), "480");
                
                // Create account_name.txt
                File.WriteAllText(Path.Combine(baseDir, "account_name.txt"), _playerName);
                
                // Create user_steam_id.txt
                File.WriteAllText(Path.Combine(baseDir, "user_steam_id.txt"), _steamId.ToString());
                
                // Create force_account_name.txt
                File.WriteAllText(Path.Combine(baseDir, "force_account_name.txt"), _playerName);
                
                // Create settings directory and configs.main.txt
                var settingsDir = Path.Combine(baseDir, "settings");
                Directory.CreateDirectory(settingsDir);
                
                var config = $@"# Goldberg Steam Emulator Configuration
enable_lan_only_mode=1
disable_overlay=1
enable_local_save=1
listen_port={_listenPort}
";
                File.WriteAllText(Path.Combine(settingsDir, "configs.main.txt"), config);
                
                // Initialize Steam API
                return SteamAPI.Init();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{_playerName}] Goldberg init error: {ex.Message}");
                return false;
            }
        }

        static async Task ProcessCallbacks()
        {
            while (_running)
            {
                try
                {
                    SteamAPI.RunCallbacks();
                    _client?.ProcessIncomingMessages();
                }
                catch (Exception ex)
                {
                    // Catch exceptions from callback processing to avoid Unity Debug.LogException SecurityException
                    Console.Error.WriteLine($"[{_playerName}] Callback processing error: {ex.Message}");
                }
                await Task.Delay(10);
            }
        }

        static async Task RunHostAsync()
        {
            try
            {
                if (_testCase == "direct_request_response_checkout")
                {
                    await RunDirectHostAsync();
                    return;
                }

                // Create lobby
                Console.WriteLine($"[{_playerName}] Creating lobby...");
                var lobby = await _client!.CreateLobbyAsync(ELobbyType.k_ELobbyTypePrivate, 4);
                
                if (lobby.LobbyId == CSteamID.Nil)
                {
                    Console.Error.WriteLine($"[{_playerName}] Failed to create lobby");
                    _exitCode = 1;
                    return;
                }

                Console.WriteLine($"[{_playerName}] Lobby created: {lobby.LobbyId.m_SteamID}");

                // Write lobby info to shared directory
                var lobbyFile = Path.Combine(_sharedDir!, "lobby.txt");
                File.WriteAllText(lobbyFile, lobby.LobbyId.m_SteamID.ToString());
                
                var hostReadyFile = Path.Combine(_sharedDir!, "host_ready.json");
                var hostReady = new
                {
                    LobbyId = lobby.LobbyId.m_SteamID,
                    HostSteamId = _steamId,
                    HostName = _playerName,
                    Timestamp = DateTime.UtcNow
                };
                File.WriteAllText(hostReadyFile, JsonSerializer.Serialize(hostReady));

                Console.WriteLine($"[{_playerName}] Wrote lobby info to {lobbyFile}");

                // Wait for client to join
                Console.WriteLine($"[{_playerName}] Waiting for client to join...");
                var clientJoinedFile = Path.Combine(_sharedDir!, "client_joined.json");
                var deadline = DateTime.UtcNow.AddSeconds(30);
                
                while (!File.Exists(clientJoinedFile) && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(100);
                }

                if (!File.Exists(clientJoinedFile))
                {
                    Console.Error.WriteLine($"[{_playerName}] Client join timeout");
                    _exitCode = 1;
                    return;
                }

                var clientInfo = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(clientJoinedFile));
                var clientSteamId = new CSteamID(clientInfo.GetProperty("ClientSteamId").GetUInt64());
                
                Console.WriteLine($"[{_playerName}] Client joined: {clientSteamId.m_SteamID}");

                // Run test case
                await RunTestCaseHost(clientSteamId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{_playerName}] Host error: {ex}");
                _exitCode = 1;
            }
        }

        static async Task RunClientAsync()
        {
            try
            {
                if (_testCase == "direct_request_response_checkout")
                {
                    await RunDirectClientAsync();
                    return;
                }

                // Wait for lobby.txt
                Console.WriteLine($"[{_playerName}] Waiting for lobby info...");
                var lobbyFile = Path.Combine(_sharedDir!, "lobby.txt");
                var deadline = DateTime.UtcNow.AddSeconds(30);
                
                while (!File.Exists(lobbyFile) && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(100);
                }

                if (!File.Exists(lobbyFile))
                {
                    Console.Error.WriteLine($"[{_playerName}] Lobby file timeout");
                    _exitCode = 1;
                    return;
                }

                var lobbyIdStr = File.ReadAllText(lobbyFile);
                var lobbyId = new CSteamID(ulong.Parse(lobbyIdStr));
                
                Console.WriteLine($"[{_playerName}] Found lobby: {lobbyId.m_SteamID}, joining...");

                // Join lobby
                var lobby = await _client!.JoinLobbyAsync(lobbyId);
                
                if (lobby.LobbyId == CSteamID.Nil)
                {
                    Console.Error.WriteLine($"[{_playerName}] Failed to join lobby");
                    _exitCode = 1;
                    return;
                }

                Console.WriteLine($"[{_playerName}] Joined lobby successfully");

                // Write client_joined.json
                var clientJoinedFile = Path.Combine(_sharedDir!, "client_joined.json");
                var clientJoined = new
                {
                    ClientSteamId = _steamId,
                    ClientName = _playerName,
                    Timestamp = DateTime.UtcNow
                };
                File.WriteAllText(clientJoinedFile, JsonSerializer.Serialize(clientJoined));

                // Get host Steam ID from host_ready.json
                var hostReadyFile = Path.Combine(_sharedDir!, "host_ready.json");
                if (!File.Exists(hostReadyFile))
                {
                    Console.Error.WriteLine($"[{_playerName}] host_ready.json not found");
                    _exitCode = 1;
                    return;
                }

                var hostInfo = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(hostReadyFile));
                var hostSteamId = new CSteamID(hostInfo.GetProperty("HostSteamId").GetUInt64());

                Console.WriteLine($"[{_playerName}] Host SteamID: {hostSteamId.m_SteamID}");

                // Run test case
                await RunTestCaseClient(hostSteamId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{_playerName}] Client error: {ex}");
                _exitCode = 1;
            }
        }

        static async Task RunDirectHostAsync()
        {
            Console.WriteLine($"[{_playerName}] Starting direct P2P host mode...");

            var hostReadyFile = Path.Combine(_sharedDir!, "host_ready.json");
            var hostReady = new
            {
                HostSteamId = _steamId,
                HostName = _playerName,
                Timestamp = DateTime.UtcNow
            };
            File.WriteAllText(hostReadyFile, JsonSerializer.Serialize(hostReady));

            var clientJoinedFile = Path.Combine(_sharedDir!, "client_joined.json");
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (!File.Exists(clientJoinedFile) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            if (!File.Exists(clientJoinedFile))
            {
                Console.Error.WriteLine($"[{_playerName}] Direct client ready timeout");
                _exitCode = 1;
                return;
            }

            var clientInfo = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(clientJoinedFile));
            var clientSteamId = new CSteamID(clientInfo.GetProperty("ClientSteamId").GetUInt64());

            await TestRequestResponseCheckout_Host(
                clientSteamId,
                Path.Combine(_sharedDir!, "host_responder_ready.json"));
        }

        static async Task RunDirectClientAsync()
        {
            Console.WriteLine($"[{_playerName}] Starting direct P2P client mode...");

            var hostReadyFile = Path.Combine(_sharedDir!, "host_ready.json");
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (!File.Exists(hostReadyFile) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            if (!File.Exists(hostReadyFile))
            {
                Console.Error.WriteLine($"[{_playerName}] Direct host ready timeout");
                _exitCode = 1;
                return;
            }

            var hostInfo = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(hostReadyFile));
            var hostSteamId = new CSteamID(hostInfo.GetProperty("HostSteamId").GetUInt64());

            var clientJoinedFile = Path.Combine(_sharedDir!, "client_joined.json");
            var clientJoined = new
            {
                ClientSteamId = _steamId,
                ClientName = _playerName,
                Timestamp = DateTime.UtcNow
            };
            File.WriteAllText(clientJoinedFile, JsonSerializer.Serialize(clientJoined));

            var responderReadyFile = Path.Combine(_sharedDir!, "host_responder_ready.json");
            deadline = DateTime.UtcNow.AddSeconds(30);
            while (!File.Exists(responderReadyFile) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            if (!File.Exists(responderReadyFile))
            {
                Console.Error.WriteLine($"[{_playerName}] Direct host responder ready timeout");
                _exitCode = 1;
                return;
            }

            await TestRequestResponseCheckout_Client(hostSteamId);
        }

        static async Task RunTestCaseHost(CSteamID clientId)
        {
            Console.WriteLine($"[{_playerName}] Running test case: {_testCase} (host)");
            
            switch (_testCase)
            {
                case "text_message_event":
                    await TestTextMessageEvent_Host(clientId);
                    break;
                    
                case "text_message_handler":
                    await TestTextMessageHandler_Host(clientId);
                    break;
                    
                case "custom_message_dropped":
                    await TestCustomMessageDropped_Host(clientId);
                    break;

                case "request_response_checkout":
                    await TestRequestResponseCheckout_Host(clientId);
                    break;
                    
                default:
                    Console.Error.WriteLine($"[{_playerName}] Unknown test case: {_testCase}");
                    _exitCode = 1;
                    break;
            }
        }

        static async Task RunTestCaseClient(CSteamID hostId)
        {
            Console.WriteLine($"[{_playerName}] Running test case: {_testCase} (client)");
            
            switch (_testCase)
            {
                case "text_message_event":
                    await TestTextMessageEvent_Client(hostId);
                    break;
                    
                case "text_message_handler":
                    await TestTextMessageHandler_Client(hostId);
                    break;
                    
                case "custom_message_dropped":
                    await TestCustomMessageDropped_Client(hostId);
                    break;

                case "request_response_checkout":
                    await TestRequestResponseCheckout_Client(hostId);
                    break;
                    
                default:
                    Console.Error.WriteLine($"[{_playerName}] Unknown test case: {_testCase}");
                    _exitCode = 1;
                    break;
            }
        }

        #region Test Cases

        static async Task TestTextMessageEvent_Host(CSteamID clientId)
        {
            var messageReceived = false;
            TextMessage? receivedMessage = null;

            _client!.OnP2PMessageReceived += (sender, args) =>
            {
                Console.WriteLine($"[{_playerName}] OnP2PMessageReceived: {args.Message.MessageType}");
                if (args.Message is TextMessage textMsg)
                {
                    messageReceived = true;
                    receivedMessage = textMsg;
                }
            };

            Console.WriteLine($"[{_playerName}] Waiting for TextMessage from client...");
            
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!messageReceived && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            if (messageReceived && receivedMessage?.Content == "Hello from client")
            {
                Console.WriteLine($"[{_playerName}] ✓ Test passed: Received TextMessage");
                _exitCode = 0;
            }
            else
            {
                Console.Error.WriteLine($"[{_playerName}] ✗ Test failed: Did not receive expected message");
                _exitCode = 1;
            }
        }

        static async Task TestTextMessageEvent_Client(CSteamID hostId)
        {
            Console.WriteLine($"[{_playerName}] Sending TextMessage to host...");
            
            var message = new TextMessage
            {
                Content = "Hello from client"
            };

            var sent = await _client!.SendMessageToPlayerAsync(hostId, message);
            
            if (sent)
            {
                Console.WriteLine($"[{_playerName}] ✓ Message sent successfully");
                _exitCode = 0;
            }
            else
            {
                Console.Error.WriteLine($"[{_playerName}] ✗ Failed to send message");
                _exitCode = 1;
            }

            // Give host time to receive
            await Task.Delay(2000);
        }

        static async Task TestTextMessageHandler_Host(CSteamID clientId)
        {
            var handlerCalled = false;
            TextMessage? receivedMessage = null;

            _client!.RegisterMessageHandler<TextMessage>((message, senderId) =>
            {
                Console.WriteLine($"[{_playerName}] Handler called for TextMessage from {senderId.m_SteamID}");
                handlerCalled = true;
                receivedMessage = message;
            });

            Console.WriteLine($"[{_playerName}] Waiting for TextMessage from client...");
            
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!handlerCalled && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            if (handlerCalled && receivedMessage?.Content == "Handler test")
            {
                Console.WriteLine($"[{_playerName}] ✓ Test passed: Handler was called");
                _exitCode = 0;
            }
            else
            {
                Console.Error.WriteLine($"[{_playerName}] ✗ Test failed: Handler was not called");
                _exitCode = 1;
            }
        }

        static async Task TestTextMessageHandler_Client(CSteamID hostId)
        {
            Console.WriteLine($"[{_playerName}] Sending TextMessage to host for handler test...");
            
            var message = new TextMessage
            {
                Content = "Handler test"
            };

            var sent = await _client!.SendMessageToPlayerAsync(hostId, message);
            
            if (sent)
            {
                Console.WriteLine($"[{_playerName}] ✓ Message sent successfully");
                _exitCode = 0;
            }
            else
            {
                Console.Error.WriteLine($"[{_playerName}] ✗ Failed to send message");
                _exitCode = 1;
            }

            // Give host time to receive
            await Task.Delay(2000);
        }

        static async Task TestCustomMessageDropped_Host(CSteamID clientId)
        {
            var eventReceived = false;

            _client!.OnP2PMessageReceived += (sender, args) =>
            {
                Console.WriteLine($"[{_playerName}] OnP2PMessageReceived: {args.Message.MessageType}");
                eventReceived = true;
            };

            Console.WriteLine($"[{_playerName}] Waiting (should NOT receive custom message event)...");
            
            await Task.Delay(5000);

            if (!eventReceived)
            {
                Console.WriteLine($"[{_playerName}] ✓ Test passed: Custom message was dropped (as expected)");
                _exitCode = 0;
            }
            else
            {
                Console.Error.WriteLine($"[{_playerName}] ✗ Test failed: Unexpectedly received custom message event");
                _exitCode = 1;
            }
        }

        static async Task TestCustomMessageDropped_Client(CSteamID hostId)
        {
            Console.WriteLine($"[{_playerName}] Sending custom TransactionMessage to host...");
            
            var message = new CustomTransactionMessage
            {
                TransactionId = "txn-test-123",
                Amount = 100.00m,
                Currency = "USD"
            };

            var sent = await _client!.SendMessageToPlayerAsync(hostId, message);
            
            if (sent)
            {
                Console.WriteLine($"[{_playerName}] ✓ Message sent successfully");
                _exitCode = 0;
            }
            else
            {
                Console.Error.WriteLine($"[{_playerName}] ✗ Failed to send message");
                _exitCode = 1;
            }

            // Give host time to (not) receive
            await Task.Delay(2000);
        }

        static async Task TestRequestResponseCheckout_Host(CSteamID clientId, string? responderReadyFile = null)
        {
            var handledRequest = false;
            string handledRequestId = string.Empty;

            using var exchange = _client!.CreateRequestResponseClient<CheckoutRequestMessage, CheckoutResponseMessage>(
                TimeSpan.FromSeconds(10));

            exchange.RegisterResponder((request, senderId) =>
            {
                Console.WriteLine($"[{_playerName}] Received checkout request {request.RequestId} from {senderId.m_SteamID}");
                handledRequest = true;
                handledRequestId = request.RequestId;

                var approvedQuantity = Math.Min(request.Body.Quantity, 7);
                return new CheckoutResponseMessage(new CheckoutResponsePayload
                {
                    ReservationId = $"reservation-{request.Body.ItemId}-{approvedQuantity}",
                    ApprovedQuantity = approvedQuantity
                })
                {
                    Success = true
                };
            });

            if (!string.IsNullOrWhiteSpace(responderReadyFile))
            {
                File.WriteAllText(responderReadyFile, DateTime.UtcNow.ToString("O"));
            }

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!handledRequest && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            if (handledRequest && !string.IsNullOrWhiteSpace(handledRequestId))
            {
                Console.WriteLine($"[{_playerName}] Test passed: handled request {handledRequestId}");
                _exitCode = 0;
            }
            else
            {
                Console.Error.WriteLine($"[{_playerName}] Test failed: request responder was not called");
                _exitCode = 1;
            }
        }

        static async Task TestRequestResponseCheckout_Client(CSteamID hostId)
        {
            using var exchange = _client!.CreateRequestResponseClient<CheckoutRequestMessage, CheckoutResponseMessage>(
                TimeSpan.FromSeconds(10));

            var request = new CheckoutRequestMessage(new CheckoutRequestPayload
            {
                ItemId = "pseudo",
                Quantity = 12
            });

            Console.WriteLine($"[{_playerName}] Sending checkout request to host...");
            var response = await exchange.SendRequestAsync(hostId, request, TimeSpan.FromSeconds(10));

            if (response.RequestId == request.RequestId &&
                response.Success &&
                response.Body.ReservationId == "reservation-pseudo-7" &&
                response.Body.ApprovedQuantity == 7)
            {
                Console.WriteLine($"[{_playerName}] Test passed: received correlated checkout response {response.RequestId}");
                _exitCode = 0;
            }
            else
            {
                Console.Error.WriteLine($"[{_playerName}] Test failed: unexpected checkout response");
                Console.Error.WriteLine($"[{_playerName}] RequestId={response.RequestId}, Success={response.Success}, Reservation={response.Body.ReservationId}, Quantity={response.Body.ApprovedQuantity}");
                _exitCode = 1;
            }
        }

        #endregion

        /// <summary>
        /// Custom transaction message for testing.
        /// </summary>
        private class CustomTransactionMessage : P2PMessage
        {
            public override string MessageType => "TRANSACTION";

            public string TransactionId { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "USD";

            public override byte[] Serialize()
            {
                var json = JsonSerializer.Serialize(this);
                return System.Text.Encoding.UTF8.GetBytes(json);
            }

            public override void Deserialize(byte[] data)
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var deserialized = JsonSerializer.Deserialize<CustomTransactionMessage>(json);
                if (deserialized != null)
                {
                    TransactionId = deserialized.TransactionId;
                    Amount = deserialized.Amount;
                    Currency = deserialized.Currency;
                    SenderId = deserialized.SenderId;
                    Timestamp = deserialized.Timestamp;
                }
            }
        }

        private class CheckoutRequestPayload
        {
            public string ItemId { get; set; } = string.Empty;
            public int Quantity { get; set; }
        }

        private class CheckoutResponsePayload
        {
            public string ReservationId { get; set; } = string.Empty;
            public int ApprovedQuantity { get; set; }
        }

        private class CheckoutRequestMessage : P2PRequestMessage<CheckoutRequestPayload>
        {
            public override string MessageType => "CHECKOUT_REQUEST";

            public CheckoutRequestMessage()
            {
            }

            public CheckoutRequestMessage(CheckoutRequestPayload payload)
                : base(payload)
            {
            }
        }

        private class CheckoutResponseMessage : P2PResponseMessage<CheckoutResponsePayload>
        {
            public override string MessageType => "CHECKOUT_RESPONSE";

            public CheckoutResponseMessage()
            {
            }

            public CheckoutResponseMessage(CheckoutResponsePayload payload)
                : base(payload)
            {
            }
        }
    }
}
