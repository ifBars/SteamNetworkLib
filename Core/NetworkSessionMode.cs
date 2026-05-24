namespace SteamNetworkLib.Core
{
    /// <summary>
    /// Active networking session mode for SteamNetworkClient.
    /// </summary>
    public enum NetworkSessionMode
    {
        /// <summary>
        /// No active multiplayer session has been detected.
        /// </summary>
        None,

        /// <summary>
        /// Standard base-game Steam lobby plus legacy Steam P2P transport.
        /// </summary>
        LobbyP2P,

        /// <summary>
        /// Dedicated-server session bridged through DedicatedServerMod messaging.
        /// </summary>
        DedicatedRelay
    }
}
