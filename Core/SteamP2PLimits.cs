using System;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif

namespace SteamNetworkLib.Core
{
    /// <summary>
    /// Steam P2P packet limits for the legacy ISteamNetworking SendP2PPacket API.
    /// </summary>
    public static class SteamP2PLimits
    {
        /// <summary>
        /// Maximum packet size for k_EP2PSendUnreliable and k_EP2PSendUnreliableNoDelay.
        /// Steam documents this as the typical MTU-sized packet limit.
        /// </summary>
        public const int UnreliableMaxPacketSize = 1200;

        /// <summary>
        /// Maximum packet size for k_EP2PSendReliable and k_EP2PSendReliableWithBuffering.
        /// Steam fragments and reassembles reliable messages up to this size.
        /// </summary>
        public const int ReliableMaxPacketSize = 1024 * 1024;

        /// <summary>
        /// Gets Steam's maximum packet size for the selected send type.
        /// </summary>
        /// <param name="sendType">The Steam P2P send type.</param>
        /// <returns>The maximum packet size, in bytes.</returns>
        public static int GetMaxPacketSize(EP2PSend sendType)
        {
            switch (sendType)
            {
                case EP2PSend.k_EP2PSendUnreliable:
                case EP2PSend.k_EP2PSendUnreliableNoDelay:
                    return UnreliableMaxPacketSize;

                case EP2PSend.k_EP2PSendReliable:
                case EP2PSend.k_EP2PSendReliableWithBuffering:
                    return ReliableMaxPacketSize;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sendType), sendType, "Unknown Steam P2P send type.");
            }
        }

        /// <summary>
        /// Gets whether the selected send type uses Steam's reliable delivery path.
        /// </summary>
        /// <param name="sendType">The Steam P2P send type.</param>
        /// <returns>True for reliable send types; otherwise, false.</returns>
        public static bool IsReliable(EP2PSend sendType)
        {
            return sendType == EP2PSend.k_EP2PSendReliable ||
                   sendType == EP2PSend.k_EP2PSendReliableWithBuffering;
        }
    }
}
