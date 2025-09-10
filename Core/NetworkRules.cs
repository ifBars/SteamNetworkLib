using System;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using SteamNetworkLib.Models;

namespace SteamNetworkLib.Core
{
    /// <summary>
    /// Configurable network rules that influence how SteamNetworkLib behaves.
    /// </summary>
    public class NetworkRules
    {
        /// <summary>
        /// Enables Steam relay usage for NAT traversal.
        /// Applied via SteamNetworking.AllowP2PPacketRelay().
        /// </summary>
        public bool EnableRelay { get; set; } = true;

        /// <summary>
        /// Default send type when a policy does not supply one.
        /// </summary>
        public EP2PSend DefaultSendType { get; set; } = EP2PSend.k_EP2PSendReliable;

        /// <summary>
        /// Minimum channel index to poll for incoming packets (IL2CPP).
        /// </summary>
        public int MinReceiveChannel { get; set; } = 0;

        /// <summary>
        /// Maximum channel index to poll for incoming packets (IL2CPP).
        /// </summary>
        public int MaxReceiveChannel { get; set; } = 3;

        /// <summary>
        /// If true, only accept P2P sessions from friends (auto-filter in callback).
        /// </summary>
        public bool AcceptOnlyFriends { get; set; } = false;

        /// <summary>
        /// Optional message policy to choose channel and send type per message.
        /// If null, DefaultSendType and caller-provided channel are used.
        /// </summary>
        public Func<P2PMessage, (int channel, EP2PSend sendType)>? MessagePolicy { get; set; }
    }
}

