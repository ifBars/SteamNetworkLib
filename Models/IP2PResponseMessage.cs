namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Identifies a correlated P2P response message that can report responder failures.
    /// </summary>
    public interface IP2PResponseMessage : IP2PCorrelatedMessage
    {
        /// <summary>
        /// Gets or sets whether the request completed successfully.
        /// </summary>
        bool Success { get; set; }

        /// <summary>
        /// Gets or sets a developer-facing error message when <see cref="Success"/> is false.
        /// </summary>
        string Error { get; set; }
    }
}
