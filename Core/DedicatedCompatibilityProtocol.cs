using System;
using System.Collections.Generic;

namespace SteamNetworkLib.Core
{
    internal static class DedicatedCompatibilityProtocol
    {
        public const string RegisterCommand = "snl_dedicated_register";
        public const string SnapshotCommand = "snl_dedicated_snapshot";
        public const string MemberJoinedCommand = "snl_dedicated_member_joined";
        public const string MemberLeftCommand = "snl_dedicated_member_left";
        public const string LobbyDataChangedCommand = "snl_dedicated_lobby_data_changed";
        public const string MemberDataChangedCommand = "snl_dedicated_member_data_changed";
        public const string SetLobbyDataCommand = "snl_dedicated_set_lobby_data";
        public const string SetMemberDataCommand = "snl_dedicated_set_member_data";
        public const string P2PSendCommand = "snl_dedicated_p2p_send";
        public const string P2PMessageCommand = "snl_dedicated_p2p_message";

        [Serializable]
        internal sealed class RegisterRequest
        {
            public string LibraryVersion { get; set; } = string.Empty;
        }

        [Serializable]
        internal sealed class SnapshotPayload
        {
            public string SessionId { get; set; } = string.Empty;
            public string LocalSteamId { get; set; } = string.Empty;
            public string OwnerSteamId { get; set; } = string.Empty;
            public string ServerSteamId { get; set; } = string.Empty;
            public List<MemberSnapshot> Members { get; set; } = new List<MemberSnapshot>();
            public Dictionary<string, string> LobbyData { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, Dictionary<string, string>> MemberData { get; set; } =
                new Dictionary<string, Dictionary<string, string>>();
        }

        [Serializable]
        internal sealed class MemberSnapshot
        {
            public string SteamId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool IsOwner { get; set; }
            public bool IsLocalPlayer { get; set; }
            public long JoinedAtUnixMs { get; set; }
        }

        [Serializable]
        internal sealed class MemberJoinedPayload
        {
            public MemberSnapshot Member { get; set; } = new MemberSnapshot();
            public string OwnerSteamId { get; set; } = string.Empty;
        }

        [Serializable]
        internal sealed class MemberLeftPayload
        {
            public string SteamId { get; set; } = string.Empty;
            public string OwnerSteamId { get; set; } = string.Empty;
        }

        [Serializable]
        internal sealed class LobbyDataChangedPayload
        {
            public string Key { get; set; } = string.Empty;
            public string OldValue { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
            public string ChangedBySteamId { get; set; } = string.Empty;
        }

        [Serializable]
        internal sealed class MemberDataChangedPayload
        {
            public string MemberSteamId { get; set; } = string.Empty;
            public string Key { get; set; } = string.Empty;
            public string OldValue { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
        }

        [Serializable]
        internal sealed class SetLobbyDataRequest
        {
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }

        [Serializable]
        internal sealed class SetMemberDataRequest
        {
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }

        [Serializable]
        internal sealed class P2PSendRequest
        {
            public string TargetSteamId { get; set; } = string.Empty;
            public string DataBase64 { get; set; } = string.Empty;
            public int Channel { get; set; }
        }

        [Serializable]
        internal sealed class P2PMessagePayload
        {
            public string SenderSteamId { get; set; } = string.Empty;
            public string DataBase64 { get; set; } = string.Empty;
            public int Channel { get; set; }
        }
    }
}
