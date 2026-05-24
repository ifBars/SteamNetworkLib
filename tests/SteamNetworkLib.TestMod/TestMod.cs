using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using SteamNetworkLib;
using SteamNetworkLib.Models;
using SteamNetworkLib.Sync;
using UnityEngine;

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
            var json = $"{{{CreateJsonBase($"\"TransactionId\":\"{Escape(TransactionId)}\",\"FromPlayer\":\"{Escape(FromPlayer)}\",\"ToPlayer\":\"{Escape(ToPlayer)}\",\"Amount\":{Amount},\"Currency\":\"{Escape(Currency)}\",\"Description\":\"{Escape(Description)}\"")}}}";
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public override void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            ParseJsonBase(json);

            TransactionId = ExtractJsonValue(json, "TransactionId");
            FromPlayer = ExtractJsonValue(json, "FromPlayer");
            ToPlayer = ExtractJsonValue(json, "ToPlayer");
            if (decimal.TryParse(ExtractJsonValue(json, "Amount"), out var amount))
            {
                Amount = amount;
            }

            Currency = ExtractJsonValue(json, "Currency");
            Description = ExtractJsonValue(json, "Description");
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    public class TestMod : MelonMod
    {
        private const string LobbyDataKey = "snl.realgame.lobby.phase";
        private const string MemberDataKey = "snl.realgame.member.role";
        private const string HostSyncKey = "snl.realgame.host.round";
        private const string ClientSyncKey = "snl.realgame.client.ready";
        private const int LargePayloadSize = 70 * 1024;

        private static readonly MelonLogger.Instance Logger = new("SteamNetworkLib.TestMod");

        private static string SharedDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp",
            "SteamNetworkLib.TestMod"
        );

        private static string LobbyFile => Path.Combine(SharedDir, "lobby.txt");
        private static string ResultsFile => Path.Combine(SharedDir, "results.txt");

        private readonly List<string> _passedPhases = new List<string>();
        private readonly Dictionary<string, List<FileTransferMessage>> _fileTransfers = new Dictionary<string, List<FileTransferMessage>>();

        private SteamNetworkClient? _client;
        private bool _isHost;
        private bool _isClient;
        private bool _initialized;
        private bool _testsPassed;
        private bool _failed;
        private string _failure = string.Empty;
        private CSteamID _lobbyId;
        private int _directMessagesReceived;
        private int _directMessagesSent;
        private int _broadcastMessagesReceived;
        private bool _largeTransferAckReceived;

        private string Role => _isHost ? "HOST" : "CLIENT";
        private string RoleResultsFile => Path.Combine(SharedDir, _isHost ? "host-results.txt" : "client-results.txt");

        public override void OnInitializeMelon()
        {
            Logger.Msg("SteamNetworkLib real-game coverage test mod starting");

            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--host")
                {
                    _isHost = true;
                }
                else if (args[i] == "--join")
                {
                    _isClient = true;
                }
                else if (args[i] == "--snl-test-dir" && i + 1 < args.Length)
                {
                    SharedDir = args[++i];
                }
            }

            if (!_isHost && !_isClient)
            {
                Logger.Error("No --host or --join argument provided.");
                return;
            }

            Directory.CreateDirectory(SharedDir);
            if (File.Exists(RoleResultsFile))
            {
                File.Delete(RoleResultsFile);
            }
        }

        public override void OnUpdate()
        {
            if (_failed)
            {
                return;
            }

            if (!_initialized)
            {
                if (!SteamAPI.Init())
                {
                    return;
                }

                _client = new SteamNetworkClient();
                if (!_client.Initialize())
                {
                    Fail("initialize", "SteamNetworkClient.Initialize returned false");
                    return;
                }

                RegisterHandlers();
                _initialized = true;

                MelonCoroutines.Start(_isHost ? RunHostTests() : RunClientTests());
                return;
            }

            _client?.ProcessIncomingMessages();
        }

        private void RegisterHandlers()
        {
            _client!.RegisterMessageHandler<TextMessage>(OnTextMessageReceived);
            _client.RegisterMessageHandler<DataSyncMessage>(OnDataSyncMessageReceived);
            _client.RegisterMessageHandler<TransactionMessage>(OnTransactionMessageReceived);
            _client.RegisterMessageHandler<FileTransferMessage>(OnFileTransferMessageReceived);
        }

        private IEnumerator RunHostTests()
        {
            yield return CreateLobby();
            if (_failed) yield break;

            yield return WaitForClientJoin();
            if (_failed) yield break;

            var clientId = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyId, 1);
            yield return new WaitForSeconds(5f);

            yield return RunHostLobbyAndMemberData(clientId);
            if (_failed) yield break;

            yield return RunHostDirectP2P(clientId);
            if (_failed) yield break;

            yield return RunHostBroadcast();
            if (_failed) yield break;

            yield return RunHostSyncVars(clientId);
            if (_failed) yield break;

            yield return RunHostLargeTransfer(clientId);
            if (_failed) yield break;

            yield return RunPacketLimitChecks(clientId);
            if (_failed) yield break;

            PassAll("All real-game host phases passed");
            yield return new WaitForSeconds(2f);
            Application.Quit();
        }

        private IEnumerator RunClientTests()
        {
            yield return JoinLobbyFromFile();
            if (_failed) yield break;

            var hostId = SteamMatchmaking.GetLobbyOwner(_lobbyId);
            yield return new WaitForSeconds(2f);

            yield return RunClientLobbyAndMemberData(hostId);
            if (_failed) yield break;

            yield return RunClientDirectP2P(hostId);
            if (_failed) yield break;

            yield return RunClientBroadcast();
            if (_failed) yield break;

            yield return RunClientSyncVars(hostId);
            if (_failed) yield break;

            yield return RunClientLargeTransferAck(hostId);
            if (_failed) yield break;

            yield return RunPacketLimitChecks(hostId);
            if (_failed) yield break;

            PassAll("All real-game client phases passed");
            yield return new WaitForSeconds(2f);
            Application.Quit();
        }

        private IEnumerator CreateLobby()
        {
            const string phase = "lobby.create";
            var task = _client!.CreateLobbyAsync(ELobbyType.k_ELobbyTypePrivate, 4);
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted)
            {
                Fail(phase, task.Exception?.GetBaseException().Message ?? "CreateLobbyAsync failed");
                yield break;
            }

            _lobbyId = task.Result.LobbyId;
            File.WriteAllText(LobbyFile, _lobbyId.m_SteamID.ToString());
            MarkPassed(phase);
        }

        private IEnumerator WaitForClientJoin()
        {
            const string phase = "lobby.member-join";
            yield return WaitFor(() => SteamMatchmaking.GetNumLobbyMembers(_lobbyId) >= 2, 30f, phase, "Client did not join lobby");
            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator JoinLobbyFromFile()
        {
            const string phase = "lobby.join";
            yield return WaitFor(() => File.Exists(LobbyFile), 30f, phase, "Lobby file was not written");
            if (_failed) yield break;

            if (!ulong.TryParse(File.ReadAllText(LobbyFile), out var lobbyId))
            {
                Fail(phase, "Lobby file did not contain a valid Steam lobby id");
                yield break;
            }

            _lobbyId = new CSteamID(lobbyId);
            var task = _client!.JoinLobbyAsync(_lobbyId);
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted)
            {
                Fail(phase, task.Exception?.GetBaseException().Message ?? "JoinLobbyAsync failed");
                yield break;
            }

            MarkPassed(phase);
        }

        private IEnumerator RunHostLobbyAndMemberData(CSteamID clientId)
        {
            const string phase = "data.lobby-member";
            _client!.SetLobbyData(LobbyDataKey, "host-ready");
            _client.SetMyData(MemberDataKey, "host");

            yield return WaitFor(
                () => _client.GetLobbyData(LobbyDataKey) == "host-ready" &&
                      _client.GetMyData(MemberDataKey) == "host" &&
                      _client.GetPlayerData(clientId, MemberDataKey) == "client",
                30f,
                phase,
                "Host did not observe expected lobby/member data");

            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator RunClientLobbyAndMemberData(CSteamID hostId)
        {
            const string phase = "data.lobby-member";
            _client!.SetMyData(MemberDataKey, "client");

            yield return WaitFor(
                () => _client.GetLobbyData(LobbyDataKey) == "host-ready" &&
                      _client.GetMyData(MemberDataKey) == "client" &&
                      _client.GetPlayerData(hostId, MemberDataKey) == "host",
                30f,
                phase,
                "Client did not observe expected lobby/member data");

            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator RunHostDirectP2P(CSteamID clientId)
        {
            const string phase = "p2p.direct";
            yield return SendStandardMessages(clientId, "host", "client");
            if (_failed) yield break;

            yield return WaitFor(() => _directMessagesReceived >= 3, 30f, phase, "Host did not receive all client direct messages");
            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator RunClientDirectP2P(CSteamID hostId)
        {
            const string phase = "p2p.direct";
            yield return WaitFor(() => _directMessagesReceived >= 3, 30f, phase, "Client did not receive all host direct messages");
            if (_failed) yield break;

            yield return SendStandardMessages(hostId, "client", "host");
            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator SendStandardMessages(CSteamID targetId, string from, string to)
        {
            var textTask = _client!.SendMessageToPlayerAsync(targetId, new TextMessage
            {
                Content = $"direct:{from}:text",
                SenderId = SteamUser.GetSteamID()
            });
            yield return WaitForSend(textTask, "p2p.direct", "TextMessage");
            if (_failed) yield break;
            _directMessagesSent++;

            var dataTask = _client.SendMessageToPlayerAsync(targetId, new DataSyncMessage
            {
                Key = $"direct:{from}:data",
                Value = "ok",
                DataType = "string",
                SenderId = SteamUser.GetSteamID()
            });
            yield return WaitForSend(dataTask, "p2p.direct", "DataSyncMessage");
            if (_failed) yield break;
            _directMessagesSent++;

            var transactionTask = _client.SendMessageToPlayerAsync(targetId, new TransactionMessage
            {
                TransactionId = $"direct-{from}-{DateTime.UtcNow.Ticks}",
                FromPlayer = from,
                ToPlayer = to,
                Amount = from == "host" ? 99.99m : 42.50m,
                Currency = "USD",
                Description = $"direct:{from}:transaction"
            });
            yield return WaitForSend(transactionTask, "p2p.direct", "TransactionMessage");
            if (!_failed) _directMessagesSent++;
        }

        private IEnumerator RunHostBroadcast()
        {
            const string phase = "p2p.broadcast";
            var task = _client!.BroadcastMessageAsync(new TextMessage
            {
                Content = "broadcast:host",
                SenderId = SteamUser.GetSteamID()
            });
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted)
            {
                Fail(phase, task.Exception?.GetBaseException().Message ?? "BroadcastMessageAsync failed");
                yield break;
            }

            MarkPassed(phase);
        }

        private IEnumerator RunClientBroadcast()
        {
            const string phase = "p2p.broadcast";
            yield return WaitFor(() => _broadcastMessagesReceived >= 1, 30f, phase, "Client did not receive host broadcast");
            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator RunHostSyncVars(CSteamID clientId)
        {
            const string phase = "syncvars";
            var hostRound = _client!.CreateHostSyncVar(HostSyncKey, 0);
            var readiness = _client.CreateClientSyncVar(ClientSyncKey, "none");

            hostRound.Value = 7;
            readiness.Value = "host-ready";

            yield return WaitFor(
                () => hostRound.Value == 7 && readiness.GetValue(clientId) == "client-ready",
                30f,
                phase,
                "Host SyncVar values did not converge");

            hostRound.Dispose();
            readiness.Dispose();
            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator RunClientSyncVars(CSteamID hostId)
        {
            const string phase = "syncvars";
            var hostRound = _client!.CreateHostSyncVar(HostSyncKey, 0);
            var readiness = _client.CreateClientSyncVar(ClientSyncKey, "none");

            readiness.Value = "client-ready";

            yield return WaitFor(
                () => hostRound.Value == 7 && readiness.GetValue(hostId) == "host-ready",
                30f,
                phase,
                "Client SyncVar values did not converge");

            hostRound.Dispose();
            readiness.Dispose();
            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator RunHostLargeTransfer(CSteamID clientId)
        {
            const string phase = "p2p.large-transfer";
            var payload = CreatePayload(LargePayloadSize);
            var checksum = Checksum(payload);
            var transferName = $"realgame-large|{payload.Length}|{checksum}";

            var task = _client!.SendLargeDataToPlayerAsync(clientId, transferName, payload, 0, 4096);
            yield return WaitForSend(task, phase, "SendLargeDataToPlayerAsync");
            if (_failed) yield break;

            yield return WaitFor(() => _largeTransferAckReceived, 30f, phase, "Host did not receive large transfer acknowledgment");
            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator RunClientLargeTransferAck(CSteamID hostId)
        {
            const string phase = "p2p.large-transfer";
            yield return WaitFor(() => HasValidLargeTransfer(), 30f, phase, "Client did not receive a valid large transfer");
            if (_failed) yield break;

            var ackTask = _client!.SendMessageToPlayerAsync(hostId, new TextMessage
            {
                Content = "large-transfer:ok",
                SenderId = SteamUser.GetSteamID()
            });
            yield return WaitForSend(ackTask, phase, "large transfer acknowledgment");
            if (!_failed) MarkPassed(phase);
        }

        private IEnumerator RunPacketLimitChecks(CSteamID targetId)
        {
            const string phase = "p2p.packet-limits";
            if (_client!.P2PManager == null)
            {
                Fail(phase, "P2PManager was null");
                yield break;
            }

            if (_client.P2PManager.GetMaxPacketSize(EP2PSend.k_EP2PSendUnreliable) != 1200 ||
                _client.P2PManager.GetMaxPacketSize(EP2PSend.k_EP2PSendReliable) != 1024 * 1024)
            {
                Fail(phase, "Unexpected Steam P2P packet limits");
                yield break;
            }

            var oversized = new byte[1201];
            var task = _client.P2PManager.SendPacketAsync(targetId, oversized, 0, EP2PSend.k_EP2PSendUnreliable);
            while (!task.IsCompleted) yield return null;

            if (!task.IsFaulted)
            {
                Fail(phase, "Oversized unreliable packet did not fault");
                yield break;
            }

            MarkPassed(phase);
        }

        private IEnumerator WaitForSend(System.Threading.Tasks.Task<bool> task, string phase, string operation)
        {
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted)
            {
                Fail(phase, $"{operation} failed: {task.Exception?.GetBaseException().Message}");
            }
            else if (!task.Result)
            {
                Fail(phase, $"{operation} returned false");
            }
        }

        private IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds, string phase, string failure)
        {
            var elapsed = 0f;
            while (!_failed && elapsed < timeoutSeconds)
            {
                if (condition())
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Fail(phase, failure);
        }

        private void OnTextMessageReceived(TextMessage message, CSteamID sender)
        {
            if (message.Content.StartsWith("direct:", StringComparison.Ordinal))
            {
                _directMessagesReceived++;
            }
            else if (message.Content == "broadcast:host")
            {
                _broadcastMessagesReceived++;
            }
            else if (message.Content == "large-transfer:ok")
            {
                _largeTransferAckReceived = true;
            }

            Logger.Msg($"TextMessage from {sender.m_SteamID}: {message.Content}");
        }

        private void OnDataSyncMessageReceived(DataSyncMessage message, CSteamID sender)
        {
            if (message.Key.StartsWith("direct:", StringComparison.Ordinal))
            {
                _directMessagesReceived++;
            }

            Logger.Msg($"DataSyncMessage from {sender.m_SteamID}: {message.Key}={message.Value}");
        }

        private void OnTransactionMessageReceived(TransactionMessage message, CSteamID sender)
        {
            if (message.Description.StartsWith("direct:", StringComparison.Ordinal))
            {
                _directMessagesReceived++;
            }

            Logger.Msg($"TransactionMessage from {sender.m_SteamID}: {message.TransactionId}");
        }

        private void OnFileTransferMessageReceived(FileTransferMessage message, CSteamID sender)
        {
            var key = string.IsNullOrEmpty(message.TransferId) ? message.FileName : message.TransferId;
            if (!_fileTransfers.TryGetValue(key, out var chunks))
            {
                chunks = new List<FileTransferMessage>();
                _fileTransfers[key] = chunks;
            }

            chunks.Add(message);
            Logger.Msg($"FileTransferMessage from {sender.m_SteamID}: {message.FileName} chunk {message.ChunkIndex + 1}/{message.TotalChunks}");
        }

        private bool HasValidLargeTransfer()
        {
            foreach (var chunks in _fileTransfers.Values)
            {
                if (chunks.Count == 0)
                {
                    continue;
                }

                var first = chunks[0];
                if (!first.FileName.StartsWith("realgame-large|", StringComparison.Ordinal) ||
                    chunks.Count < first.TotalChunks)
                {
                    continue;
                }

                chunks.Sort((left, right) => left.ChunkIndex.CompareTo(right.ChunkIndex));
                var data = new byte[first.FileSize];
                var offset = 0;
                for (var i = 0; i < chunks.Count; i++)
                {
                    if (chunks[i].ChunkIndex != i)
                    {
                        return false;
                    }

                    Array.Copy(chunks[i].ChunkData, 0, data, offset, chunks[i].ChunkData.Length);
                    offset += chunks[i].ChunkData.Length;
                }

                if (offset != first.FileSize)
                {
                    return false;
                }

                var parts = first.FileName.Split('|');
                if (parts.Length < 3 ||
                    !int.TryParse(parts[1], out var expectedLength) ||
                    !int.TryParse(parts[2], out var expectedChecksum))
                {
                    return false;
                }

                return expectedLength == data.Length && expectedChecksum == Checksum(data);
            }

            return false;
        }

        private static byte[] CreatePayload(int size)
        {
            var data = new byte[size];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 251);
            }

            return data;
        }

        private static int Checksum(byte[] data)
        {
            unchecked
            {
                var sum = 17;
                for (var i = 0; i < data.Length; i++)
                {
                    sum = (sum * 31) + data[i];
                }

                return sum;
            }
        }

        private void MarkPassed(string phase)
        {
            if (!_passedPhases.Contains(phase))
            {
                _passedPhases.Add(phase);
                Logger.Msg($"[PASS] {phase}");
            }
        }

        private void Fail(string phase, string details)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            _failure = $"{phase}: {details}";
            Logger.Error($"[FAIL] {_failure}");
            WriteResults(false, _failure);
            Application.Quit();
        }

        private void PassAll(string details)
        {
            _testsPassed = true;
            WriteResults(true, $"{details}; Phases:{string.Join(",", _passedPhases.ToArray())}");
        }

        private void WriteResults(bool passed, string details)
        {
            try
            {
                var result = $"{Role}|{(passed ? "PASS" : "FAIL")}|{details}|DirectSent:{_directMessagesSent}|DirectReceived:{_directMessagesReceived}|BroadcastReceived:{_broadcastMessagesReceived}|Phases:{string.Join(",", _passedPhases.ToArray())}";
                File.WriteAllText(RoleResultsFile, result);
                File.WriteAllText(ResultsFile, result);
                Logger.Msg($"Results written to: {RoleResultsFile}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to write results: {ex.Message}");
            }
        }

        public override void OnApplicationQuit()
        {
            if (!_testsPassed && _initialized && !_failed)
            {
                WriteResults(false, string.IsNullOrEmpty(_failure) ? "Application quit before tests completed" : _failure);
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
