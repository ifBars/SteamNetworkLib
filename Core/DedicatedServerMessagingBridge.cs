using System;
using System.Reflection;

namespace SteamNetworkLib.Core
{
    internal sealed class DedicatedServerMessagingBridge : IDedicatedServerMessagingBridge
    {
        private static readonly string[] DedicatedSignalCommands =
        {
            "auth_challenge",
            "auth_result",
            "server_data",
            DedicatedCompatibilityProtocol.SnapshotCommand,
            DedicatedCompatibilityProtocol.MemberJoinedCommand,
            DedicatedCompatibilityProtocol.MemberLeftCommand,
            DedicatedCompatibilityProtocol.LobbyDataChangedCommand,
            DedicatedCompatibilityProtocol.MemberDataChangedCommand,
            DedicatedCompatibilityProtocol.P2PMessageCommand
        };

        private const string CustomMessagingTypeName = "DedicatedServerMod.Shared.Networking.CustomMessaging";

        private readonly Type _customMessagingType;
        private readonly MethodInfo? _trySendToServerMethod;
        private readonly MethodInfo? _sendToServerMethod;
        private readonly EventInfo _clientMessageEvent;
        private readonly Action<string, string> _messageCallback;
        private readonly EventInfo? _endpointReadyEvent;
        private readonly Action _endpointReadyCallback;

        private bool _disposed;

        public event Action<string, string>? MessageReceived;

        public event Action? EndpointReady;

        public bool IsDedicatedContextLikely { get; private set; }

        private DedicatedServerMessagingBridge(
            Type customMessagingType,
            MethodInfo? trySendToServerMethod,
            MethodInfo? sendToServerMethod,
            EventInfo clientMessageEvent,
            EventInfo? endpointReadyEvent,
            bool isEndpointReady)
        {
            _customMessagingType = customMessagingType;
            _trySendToServerMethod = trySendToServerMethod;
            _sendToServerMethod = sendToServerMethod;
            _clientMessageEvent = clientMessageEvent;
            _messageCallback = HandleClientMessageReceived;
            _endpointReadyEvent = endpointReadyEvent;
            _endpointReadyCallback = HandleEndpointReady;
            IsDedicatedContextLikely = isEndpointReady;

            _clientMessageEvent.AddEventHandler(null, _messageCallback);
            _endpointReadyEvent?.AddEventHandler(null, _endpointReadyCallback);
        }

        public static DedicatedServerMessagingBridge? TryCreate()
        {
            Type? customMessagingType = ResolveCustomMessagingType();
            if (customMessagingType == null)
            {
                return null;
            }

            MethodInfo? trySend = customMessagingType.GetMethod(
                "TrySendToServer",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string) },
                null);

            MethodInfo? send = customMessagingType.GetMethod(
                "SendToServer",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string) },
                null);

            EventInfo? clientEvent = customMessagingType.GetEvent("ClientMessageReceived", BindingFlags.Public | BindingFlags.Static);
            if (clientEvent == null)
            {
                return null;
            }

            if (trySend == null && send == null)
            {
                return null;
            }

            EventInfo? endpointReadyEvent = customMessagingType.GetEvent("EndpointReady", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo? isEndpointReadyProperty = customMessagingType.GetProperty(
                "IsEndpointReady",
                BindingFlags.Public | BindingFlags.Static);

            bool isEndpointReady = false;
            try
            {
                if (isEndpointReadyProperty?.GetValue(null) is bool ready)
                {
                    isEndpointReady = ready;
                }
            }
            catch
            {
                // The endpoint can still signal readiness later.
            }

            return new DedicatedServerMessagingBridge(
                customMessagingType,
                trySend,
                send,
                clientEvent,
                endpointReadyEvent,
                isEndpointReady);
        }

        public bool TrySendToServer(string command, string payload)
        {
            if (_disposed || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            object[] args = { command, payload ?? string.Empty };

            try
            {
                if (_trySendToServerMethod != null)
                {
                    object? result = _trySendToServerMethod.Invoke(null, args);
                    if (result is bool sent)
                    {
                        return sent;
                    }
                }

                if (_sendToServerMethod != null)
                {
                    _sendToServerMethod.Invoke(null, args);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _clientMessageEvent.RemoveEventHandler(null, _messageCallback);
                _endpointReadyEvent?.RemoveEventHandler(null, _endpointReadyCallback);
            }
            catch
            {
                // Ignore teardown issues.
            }

            _disposed = true;
        }

        private void HandleEndpointReady()
        {
            IsDedicatedContextLikely = true;
            EndpointReady?.Invoke();
        }

        private void HandleClientMessageReceived(string command, string payload)
        {
            if (string.IsNullOrEmpty(command))
            {
                return;
            }

            for (int i = 0; i < DedicatedSignalCommands.Length; i++)
            {
                if (string.Equals(command, DedicatedSignalCommands[i], StringComparison.Ordinal))
                {
                    IsDedicatedContextLikely = true;
                    break;
                }
            }

            MessageReceived?.Invoke(command, payload ?? string.Empty);
        }

        private static Type? ResolveCustomMessagingType()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                try
                {
                    Type? type = assembly.GetType(CustomMessagingTypeName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // Ignore transient reflection issues while assemblies are loading.
                }
            }

            return null;
        }
    }
}
