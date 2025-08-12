using System;

namespace SteamNetworkLib.Utilities
{
    /// <summary>
    /// Provides runtime compatibility detection for audio streaming features.
    /// Audio streaming requires Mono runtime due to OpusSharp limitations with IL2CPP.
    /// </summary>
    public static class AudioStreamingCompatibility
    {
        /// <summary>
        /// Gets whether audio streaming is supported in the current runtime environment.
        /// </summary>
        /// <remarks>
        /// Audio streaming is only supported on Mono runtime due to OpusSharp library limitations.
        /// IL2CPP has memory marshalling issues that cause corruption when passing audio data
        /// between managed and native code through OpusSharp.
        /// </remarks>
        public static bool IsSupported
        {
            get
            {
#if MONO
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets the current runtime type being used.
        /// </summary>
        public static string RuntimeType
        {
            get
            {
#if MONO
                return "Mono";
#elif IL2CPP
                return "IL2CPP";
#else
                return "Unknown";
#endif
            }
        }

        /// <summary>
        /// Throws an exception if audio streaming is not supported in the current environment.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Thrown when audio streaming is not supported.</exception>
        public static void ThrowIfNotSupported()
        {
            if (!IsSupported)
            {
                throw new PlatformNotSupportedException(
                    $"Audio streaming is not supported on {RuntimeType} runtime. " +
                    "Audio streaming requires Mono runtime due to OpusSharp limitations with IL2CPP. " +
                    "IL2CPP has memory marshalling issues that cause corruption when passing audio data " +
                    "between managed and native code. Please use a Mono-based game/environment for audio streaming features."
                );
            }
        }

        /// <summary>
        /// Gets a detailed explanation of why audio streaming may not be supported.
        /// </summary>
        public static string GetCompatibilityMessage()
        {
            if (IsSupported)
            {
                return $"Audio streaming is supported on {RuntimeType} runtime.";
            }
            else
            {
                return $"Audio streaming is NOT supported on {RuntimeType} runtime. " +
                       "Audio streaming requires Mono runtime due to OpusSharp limitations. " +
                       "IL2CPP has memory marshalling issues that cause corruption when passing " +
                       "audio data between managed and native code through the OpusSharp library. " +
                       "The core SteamNetworkLib features (lobbies, P2P messaging, file transfer, etc.) " +
                       "work perfectly on both Mono and IL2CPP - only audio streaming is affected.";
            }
        }

        /// <summary>
        /// Logs a warning message about audio streaming compatibility to the console.
        /// </summary>
        public static void LogCompatibilityWarning()
        {
            if (!IsSupported)
            {
                Console.WriteLine($"[SteamNetworkLib] WARNING: {GetCompatibilityMessage()}");
            }
        }
    }
}
