#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif
using System;
using System.Text;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Represents a universal real-time streaming message for audio, video, or continuous data streams.
    /// Supports compression, quality settings, and proper sequencing for low-latency communication.
    /// Optimized for streaming applications that require minimal delay and high throughput.
    /// </summary>
    public class StreamMessage : P2PMessage
    {
        /// <summary>
        /// Gets the message type identifier for stream messages.
        /// </summary>
        public override string MessageType => "STREAM";

        /// <summary>
        /// Gets or sets the type of stream data being transmitted.
        /// Common values include "audio", "video", "data", "mixed", etc.
        /// </summary>
        public string StreamType { get; set; } = "audio";

        /// <summary>
        /// Gets or sets the unique stream identifier to handle multiple concurrent streams.
        /// Allows multiple independent streams between the same players.
        /// </summary>
        public string StreamId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sequence number for packet ordering and loss detection.
        /// Essential for maintaining proper stream continuity and detecting missing packets.
        /// </summary>
        public uint SequenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of when this data was captured or generated.
        /// Used for synchronization between audio and video streams or maintaining timing.
        /// </summary>
        public long CaptureTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the sample rate for audio streams in Hz.
        /// Common values include 44100, 48000, 22050, etc.
        /// </summary>
        public int SampleRate { get; set; } = 44100;

        /// <summary>
        /// Gets or sets the number of audio channels.
        /// 1 = mono, 2 = stereo, 6 = 5.1 surround, etc.
        /// </summary>
        public int Channels { get; set; } = 1;

        /// <summary>
        /// Gets or sets the bits per sample for audio quality.
        /// Common values include 16, 24, 32 bits per sample.
        /// </summary>
        public int BitsPerSample { get; set; } = 16;

        /// <summary>
        /// Gets or sets the number of samples per frame.
        /// For example, 960 samples for 20ms at 48kHz sampling rate.
        /// </summary>
        public int FrameSamples { get; set; } = 960;

        /// <summary>
        /// Gets or sets the compression codec used for the stream data.
        /// Common values include "none", "opus", "mp3", "h264", "vp8", etc.
        /// </summary>
        public string Codec { get; set; } = "none";

        /// <summary>
        /// Gets or sets the quality level for compression.
        /// Value between 0-100, where higher values mean better quality but larger size.
        /// </summary>
        public int Quality { get; set; } = 75;

        /// <summary>
        /// Gets or sets a value indicating whether this is the start of a new stream.
        /// Used to initialize stream decoders and reset state on the receiving end.
        /// </summary>
        public bool IsStreamStart { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the end of the stream.
        /// Signals to the receiver that no more data will be sent for this stream.
        /// </summary>
        public bool IsStreamEnd { get; set; }

        /// <summary>
        /// Gets or sets the recommended send type for this stream.
        /// Defaults to UnreliableNoDelay for real-time audio/video to minimize latency.
        /// </summary>
        public EP2PSend RecommendedSendType { get; set; } = EP2PSend.k_EP2PSendUnreliableNoDelay;

        /// <summary>
        /// Gets or sets the actual stream data payload.
        /// Contains the raw or compressed audio/video/data bytes.
        /// </summary>
        public byte[] StreamData { get; set; } = new byte[0];

        /// <summary>
        /// Gets or sets additional metadata as a JSON string.
        /// Can contain codec-specific parameters, custom headers, or other stream information.
        /// </summary>
        public string Metadata { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sequence number this message is acknowledging.
        /// Used for reliability feedback and flow control mechanisms.
        /// </summary>
        public uint? AckForSequence { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a retransmitted frame.
        /// Used for reliability mechanisms when important frames need to be resent.
        /// </summary>
        public bool IsRetransmit { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether this frame contains forward error correction data.
        /// FEC data can help recover from packet loss without retransmission.
        /// </summary>
        public bool IsFecFrame { get; set; } = false;

        /// <summary>
        /// Gets or sets the payload type for extensibility.
        /// More specific than StreamType, can include "pcm_audio", "h264_video", "metadata", etc.
        /// </summary>
        public string PayloadType { get; set; } = "audio";

        /// <summary>
        /// Gets or sets the expected duration of this frame in milliseconds.
        /// Used for timing and buffering calculations on the receiving end.
        /// </summary>
        public int FrameDurationMs { get; set; } = 20;

        /// <summary>
        /// Gets or sets the priority level for this frame.
        /// Value between 0-255, where higher values indicate more important frames (e.g., keyframes).
        /// </summary>
        public byte Priority { get; set; } = 128;

        /// <summary>
        /// Serializes the stream message to a byte array for network transmission.
        /// Uses a hybrid format with JSON header followed by raw binary stream data.
        /// </summary>
        /// <returns>A byte array containing the serialized message with header and stream data.</returns>
        public override byte[] Serialize()
        {
            var headerJson = $"{{{CreateJsonBase($"\"StreamType\":\"{StreamType}\",\"StreamId\":\"{StreamId}\",\"SequenceNumber\":{SequenceNumber},\"CaptureTimestamp\":{CaptureTimestamp},\"SampleRate\":{SampleRate},\"Channels\":{Channels},\"BitsPerSample\":{BitsPerSample},\"FrameSamples\":{FrameSamples},\"Codec\":\"{Codec}\",\"Quality\":{Quality},\"IsStreamStart\":{IsStreamStart.ToString().ToLower()},\"IsStreamEnd\":{IsStreamEnd.ToString().ToLower()},\"RecommendedSendType\":{(int)RecommendedSendType},\"Metadata\":\"{Metadata}\",\"AckForSequence\":{(AckForSequence?.ToString() ?? "null")},\"IsRetransmit\":{IsRetransmit.ToString().ToLower()},\"IsFecFrame\":{IsFecFrame.ToString().ToLower()},\"PayloadType\":\"{PayloadType}\",\"FrameDurationMs\":{FrameDurationMs},\"Priority\":{Priority}")}}}";
            var headerBytes = Encoding.UTF8.GetBytes(headerJson);

            var result = new byte[4 + headerBytes.Length + StreamData.Length];
            var offset = 0;

            BitConverter.GetBytes(headerBytes.Length).CopyTo(result, offset);
            offset += 4;

            headerBytes.CopyTo(result, offset);
            offset += headerBytes.Length;

            StreamData.CopyTo(result, offset);

            return result;
        }

        /// <summary>
        /// Deserializes the stream message from a byte array received over the network.
        /// Parses the JSON header and extracts the binary stream data.
        /// </summary>
        /// <param name="data">The byte array containing the serialized message data.</param>
        public override void Deserialize(byte[] data)
        {
            if (data.Length < 4) return;

            var headerLength = BitConverter.ToInt32(data, 0);
            if (headerLength <= 0 || headerLength > data.Length - 4) return;

            var headerJson = Encoding.UTF8.GetString(data, 4, headerLength);
            ParseJsonBase(headerJson);

            StreamType = ExtractJsonValue(headerJson, "StreamType");
            StreamId = ExtractJsonValue(headerJson, "StreamId");

            if (uint.TryParse(ExtractJsonValue(headerJson, "SequenceNumber"), out uint seqNum))
                SequenceNumber = seqNum;

            if (long.TryParse(ExtractJsonValue(headerJson, "CaptureTimestamp"), out long timestamp))
                CaptureTimestamp = timestamp;

            if (int.TryParse(ExtractJsonValue(headerJson, "SampleRate"), out int sampleRate))
                SampleRate = sampleRate;

            if (int.TryParse(ExtractJsonValue(headerJson, "Channels"), out int channels))
                Channels = channels;

            if (int.TryParse(ExtractJsonValue(headerJson, "BitsPerSample"), out int bitsPerSample))
                BitsPerSample = bitsPerSample;

            if (int.TryParse(ExtractJsonValue(headerJson, "FrameSamples"), out int frameSamples))
                FrameSamples = frameSamples;

            Codec = ExtractJsonValue(headerJson, "Codec");

            if (int.TryParse(ExtractJsonValue(headerJson, "Quality"), out int quality))
                Quality = quality;

            IsStreamStart = ExtractJsonValue(headerJson, "IsStreamStart").ToLower() == "true";
            IsStreamEnd = ExtractJsonValue(headerJson, "IsStreamEnd").ToLower() == "true";

            if (int.TryParse(ExtractJsonValue(headerJson, "RecommendedSendType"), out int sendType))
                RecommendedSendType = (EP2PSend)sendType;

            Metadata = ExtractJsonValue(headerJson, "Metadata");

            var ackStr = ExtractJsonValue(headerJson, "AckForSequence");
            if (!string.IsNullOrEmpty(ackStr) && ackStr != "null" && uint.TryParse(ackStr, out uint ackSeq))
                AckForSequence = ackSeq;

            IsRetransmit = ExtractJsonValue(headerJson, "IsRetransmit").ToLower() == "true";
            IsFecFrame = ExtractJsonValue(headerJson, "IsFecFrame").ToLower() == "true";
            PayloadType = ExtractJsonValue(headerJson, "PayloadType");

            if (int.TryParse(ExtractJsonValue(headerJson, "FrameDurationMs"), out int frameDuration))
                FrameDurationMs = frameDuration;

            if (byte.TryParse(ExtractJsonValue(headerJson, "Priority"), out byte priority))
                Priority = priority;

            var streamDataLength = data.Length - 4 - headerLength;
            if (streamDataLength > 0)
            {
#if !MONO
                // IL2CPP-specific: Create a fresh copy of the data to avoid marshalling issues
                StreamData = new byte[streamDataLength];
                
                // Manual byte-by-byte copy to ensure proper data transfer
                for (int i = 0; i < streamDataLength; i++)
                {
                    if (4 + headerLength + i < data.Length)
                    {
                        StreamData[i] = data[4 + headerLength + i];
                    }
                }
                
                // Check if the data is all zeros (possible IL2CPP marshalling issue)
                bool allZeros = streamDataLength > 4;
                for (int i = 0; i < Math.Min(4, streamDataLength) && allZeros; i++)
                {
                    if (StreamData[i] != 0) allZeros = false;
                }
                
                if (allZeros && streamDataLength > 4)
                {
                    Console.WriteLine($"[StreamMessage] Warning: StreamData may be corrupted (all zeros) for frame {SequenceNumber}");
                }
#else
                // Original Mono version
                StreamData = new byte[streamDataLength];
                Array.Copy(data, 4 + headerLength, StreamData, 0, streamDataLength);
#endif
            }
            else
            {
                StreamData = new byte[0];
            }
        }
    }
}