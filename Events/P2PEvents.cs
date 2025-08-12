using SteamNetworkLib.Models;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;

namespace SteamNetworkLib.Events
{
    /// <summary>
    /// Provides data for the P2P packet received event.
    /// Contains raw packet data and metadata about the received packet.
    /// </summary>
    public class P2PPacketReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the Steam ID of the player who sent the packet.
        /// </summary>
        public CSteamID SenderId { get; }

        /// <summary>
        /// Gets the raw packet data that was received.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets the communication channel on which the packet was received.
        /// </summary>
        public int Channel { get; }

        /// <summary>
        /// Gets the size of the received packet in bytes.
        /// </summary>
        public uint PacketSize { get; }

        /// <summary>
        /// Gets the timestamp when the packet was received.
        /// </summary>
        public DateTime ReceivedAt { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PPacketReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="senderId">The Steam ID of the player who sent the packet.</param>
        /// <param name="data">The raw packet data that was received.</param>
        /// <param name="channel">The communication channel on which the packet was received.</param>
        /// <param name="packetSize">The size of the received packet in bytes.</param>
        public P2PPacketReceivedEventArgs(CSteamID senderId, byte[] data, int channel, uint packetSize)
        {
            SenderId = senderId;
            Data = data;
            Channel = channel;
            PacketSize = packetSize;
            ReceivedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Provides data for the P2P message received event.
    /// Contains deserialized message data and metadata about the received message.
    /// </summary>
    public class P2PMessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the deserialized message that was received.
        /// </summary>
        public P2PMessage Message { get; }

        /// <summary>
        /// Gets the Steam ID of the player who sent the message.
        /// </summary>
        public CSteamID SenderId { get; }

        /// <summary>
        /// Gets the communication channel on which the message was received.
        /// </summary>
        public int Channel { get; }

        /// <summary>
        /// Gets the timestamp when the message was received.
        /// </summary>
        public DateTime ReceivedAt { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PMessageReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="message">The deserialized message that was received.</param>
        /// <param name="senderId">The Steam ID of the player who sent the message.</param>
        /// <param name="channel">The communication channel on which the message was received.</param>
        public P2PMessageReceivedEventArgs(P2PMessage message, CSteamID senderId, int channel)
        {
            Message = message;
            SenderId = senderId;
            Channel = channel;
            ReceivedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Provides data for the P2P session request event.
    /// Contains information about an incoming P2P session request and allows controlling the response.
    /// </summary>
    public class P2PSessionRequestEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the Steam ID of the player requesting the P2P session.
        /// </summary>
        public CSteamID RequesterId { get; }

        /// <summary>
        /// Gets the display name of the player requesting the P2P session.
        /// </summary>
        public string RequesterName { get; }

        /// <summary>
        /// Gets or sets whether the P2P session request should be accepted.
        /// Defaults to true.
        /// </summary>
        public bool ShouldAccept { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PSessionRequestEventArgs"/> class.
        /// </summary>
        /// <param name="requesterId">The Steam ID of the player requesting the P2P session.</param>
        /// <param name="requesterName">The display name of the player requesting the P2P session.</param>
        public P2PSessionRequestEventArgs(CSteamID requesterId, string requesterName)
        {
            RequesterId = requesterId;
            RequesterName = requesterName;
            ShouldAccept = true;
        }
    }

    /// <summary>
    /// Provides data for the P2P session connect fail event.
    /// Contains information about a failed P2P connection attempt.
    /// </summary>
    public class P2PSessionConnectFailEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the Steam ID of the target player that could not be connected to.
        /// </summary>
        public CSteamID TargetId { get; }

        /// <summary>
        /// Gets the specific error that occurred during the connection attempt.
        /// </summary>
        public EP2PSessionError Error { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PSessionConnectFailEventArgs"/> class.
        /// </summary>
        /// <param name="targetId">The Steam ID of the target player that could not be connected to.</param>
        /// <param name="error">The specific error that occurred during the connection attempt.</param>
        public P2PSessionConnectFailEventArgs(CSteamID targetId, EP2PSessionError error)
        {
            TargetId = targetId;
            Error = error;
        }
    }

    /// <summary>
    /// Provides data for the P2P packet sent event.
    /// Contains information about the result of sending a P2P packet.
    /// </summary>
    public class P2PPacketSentEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the Steam ID of the target player the packet was sent to.
        /// </summary>
        public CSteamID TargetId { get; }

        /// <summary>
        /// Gets whether the packet was sent successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the size of the data that was sent in bytes.
        /// </summary>
        public int DataSize { get; }

        /// <summary>
        /// Gets the communication channel on which the packet was sent.
        /// </summary>
        public int Channel { get; }

        /// <summary>
        /// Gets the send type used for the packet transmission.
        /// </summary>
        public EP2PSend SendType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PPacketSentEventArgs"/> class.
        /// </summary>
        /// <param name="targetId">The Steam ID of the target player the packet was sent to.</param>
        /// <param name="success">Whether the packet was sent successfully.</param>
        /// <param name="dataSize">The size of the data that was sent in bytes.</param>
        /// <param name="channel">The communication channel on which the packet was sent.</param>
        /// <param name="sendType">The send type used for the packet transmission.</param>
        public P2PPacketSentEventArgs(CSteamID targetId, bool success, int dataSize, int channel, EP2PSend sendType)
        {
            TargetId = targetId;
            Success = success;
            DataSize = dataSize;
            Channel = channel;
            SendType = sendType;
        }
    }
}