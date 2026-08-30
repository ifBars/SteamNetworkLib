namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Identifies a P2P message that belongs to a request/response exchange.
    /// </summary>
    public interface IP2PCorrelatedMessage
    {
        /// <summary>
        /// Gets or sets the correlation identifier shared by a request and its response.
        /// </summary>
        string RequestId { get; set; }
    }
}
