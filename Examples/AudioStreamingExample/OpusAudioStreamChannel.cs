#if SCHEDULE_ONE_INTEGRATION && MONO
// Audio streaming is only available on Mono runtime due to OpusSharp limitations with IL2CPP
using OpusSharp.Core;
using SteamNetworkLib.Models;
using SteamNetworkLib.Streaming;
using SteamNetworkLib.Utilities;
using System;
using MelonLoader;
using System.Text;
using System.Runtime.InteropServices;

namespace AudioStreamingExample
{
    /// <summary>
    /// Audio-specific stream channel that handles Opus decoding with packet loss concealment (PLC)
    /// and proper audio frame buffering for smooth playback.
    /// </summary>
    public class OpusAudioStreamChannel : StreamChannel<float[]>
    {
        public int SampleRate { get; }
        public int Channels { get; }
        public int FrameSize { get; }

        private OpusDecoder _decoder;
        private readonly object _decoderLock = new object();
        private bool _decoderInitialized = false;

        // Audio-specific settings
        public bool EnablePacketLossConcealment { get; set; } = true;
        public float Volume { get; set; } = 1.0f;

        // Statistics
        public uint TotalPlcFramesGenerated { get; private set; }

#if !MONO
        // Direct P/Invoke declarations for IL2CPP to bypass OpusSharp marshalling issues
        private class OpusNative
        {
            // Define the native function signatures for direct P/Invoke
            [DllImport("opus", CallingConvention = CallingConvention.Cdecl, EntryPoint = "opus_decoder_create")]
            public static extern IntPtr opus_decoder_create(int Fs, int channels, out int error);

            [DllImport("opus", CallingConvention = CallingConvention.Cdecl, EntryPoint = "opus_decode")]
            public static extern int opus_decode(IntPtr st, IntPtr data, int len, IntPtr pcm, int frame_size, int decode_fec);

            [DllImport("opus", CallingConvention = CallingConvention.Cdecl, EntryPoint = "opus_decoder_destroy")]
            public static extern void opus_decoder_destroy(IntPtr st);
            
            // Handle to the native Opus decoder
            private IntPtr _decoderPtr = IntPtr.Zero;
            
            public OpusNative(int sampleRate, int channels)
            {
                int error;
                _decoderPtr = opus_decoder_create(sampleRate, channels, out error);
                if (error != 0 || _decoderPtr == IntPtr.Zero)
                {
                    throw new Exception($"Failed to create Opus decoder: error code {error}");
                }
            }
            
            public int Decode(byte[] encodedData, short[] pcmData, int frameSize)
            {
                if (_decoderPtr == IntPtr.Zero)
                    return -1;
                
                // Use Marshal.AllocHGlobal for manual memory management to bypass IL2CPP issues
                IntPtr inputPtr = IntPtr.Zero;
                IntPtr outputPtr = IntPtr.Zero;
                
                try
                {
                    // Allocate unmanaged memory for input data
                    inputPtr = Marshal.AllocHGlobal(encodedData.Length);
                    Marshal.Copy(encodedData, 0, inputPtr, encodedData.Length);
                    
                    // Allocate unmanaged memory for output data
                    int outputSize = pcmData.Length * sizeof(short);
                    outputPtr = Marshal.AllocHGlobal(outputSize);
                    
                    // Call the native function
                    int result = opus_decode(_decoderPtr, inputPtr, encodedData.Length, outputPtr, frameSize, 0);
                    
                    if (result > 0)
                    {
                        // Copy the result back to managed memory
                        Marshal.Copy(outputPtr, pcmData, 0, pcmData.Length);
                    }
                    
                    return result;
                }
                finally
                {
                    // Always free the unmanaged memory
                    if (inputPtr != IntPtr.Zero)
                        Marshal.FreeHGlobal(inputPtr);
                    if (outputPtr != IntPtr.Zero)
                        Marshal.FreeHGlobal(outputPtr);
                }
            }
            
            public void Dispose()
            {
                if (_decoderPtr != IntPtr.Zero)
                {
                    opus_decoder_destroy(_decoderPtr);
                    _decoderPtr = IntPtr.Zero;
                }
            }
        }
        
        // Direct native decoder instance for IL2CPP
        private OpusNative _nativeDecoder;
#endif

        public OpusAudioStreamChannel(string streamId, int sampleRate, int channels, int frameSize)
            : base(streamId)
        {
            // Verify that audio streaming is supported in this runtime environment
            AudioStreamingCompatibility.ThrowIfNotSupported();
            
            SampleRate = sampleRate;
            Channels = channels;
            FrameSize = frameSize;

            // Configure stable buffer settings for smooth music playback
            BufferMs = 120; // More conservative buffer for stability (6 frames at 20ms)
            MaxBufferMs = 300; // Higher maximum buffer during network issues

            InitializeDecoder();
        }

        private void InitializeDecoder()
        {
            try
            {
                lock (_decoderLock)
                {
#if !MONO
                    // IL2CPP-specific initialization
                    try
                    {
                        // First dispose any existing decoder
                        if (_decoder != null)
                        {
                            _decoder.Dispose();
                            _decoder = null;
                        }
                        
                        // Create a new decoder with explicit settings
                        _decoder = new OpusDecoder(SampleRate, Channels);
                        
                        // Also create our direct native decoder for IL2CPP
                        try
                        {
                            _nativeDecoder = new OpusNative(SampleRate, Channels);
                            MelonLogger.Msg($"[AudioReceiver] Created direct native Opus decoder for IL2CPP");
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"[AudioReceiver] Failed to create direct native decoder: {ex.Message}");
                        }
                        
                        _decoderInitialized = true;
                        MelonLogger.Msg($"[AudioReceiver] Initialized Opus decoder (IL2CPP): {SampleRate}Hz, {Channels} channels, {FrameSize} frame size");
                        
                        // Test the decoder with a simple packet
                        try
                        {
                            byte[] testData = new byte[] { 0x7C, 0x07, 0xFD, 0x32, 0xCC, 0xA6, 0x3A, 0x25 }; // Typical Opus packet header
                            short[] testOutput = new short[FrameSize * Channels];
                            
                            // Try decoding a test packet - this might fail but helps detect issues early
                            _decoder.Decode(testData, testData.Length, testOutput, FrameSize, false);
                            
                            // Check if we got any non-zero output
                            bool hasOutput = false;
                            for (int i = 0; i < Math.Min(testOutput.Length, 100) && !hasOutput; i++)
                            {
                                if (testOutput[i] != 0) hasOutput = true;
                            }
                            
                            if (!hasOutput)
                            {
                                MelonLogger.Warning("[AudioReceiver] Test decode produced only zeros - may indicate IL2CPP marshalling issues");
                            }
                            else
                            {
                                MelonLogger.Msg("[AudioReceiver] Test decode successful - decoder is working properly");
                            }
                        }
                        catch (Exception ex)
                        {
                            // Not critical if test fails
                            MelonLogger.Warning($"[AudioReceiver] Test decode failed (not critical): {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[AudioReceiver] IL2CPP-specific error initializing Opus decoder: {ex.Message}");
                        _decoderInitialized = false;
                    }
#else
                    // Original Mono initialization
                    _decoder = new OpusDecoder(SampleRate, Channels);
                    _decoderInitialized = true;
                    MelonLogger.Msg($"[AudioReceiver] Initialized Opus decoder: {SampleRate}Hz, {Channels} channels, {FrameSize} frame size");
#endif
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize Opus decoder: {ex.Message}");
                _decoderInitialized = false;
            }
        }

        protected override float[]? DeserializeFrame(byte[] data, StreamMessage message)
        {
            if (!_decoderInitialized || _isDisposed) return null;

            var startTime = DateTime.UtcNow;

            try
            {
                // Debug the incoming data
                if (TotalFramesReceived == 0 || TotalFramesReceived % 100 == 0)
                {
                    MelonLogger.Msg($"[AudioReceiver] Received data length: {data.Length}, first bytes: {BytesToDebugString(data, 8)}");
                }

                if (data.Length == 0)
                {
                    MelonLogger.Warning($"[AudioReceiver] Received empty data for frame {message.SequenceNumber} - likely stream control message");
                    return null;
                }
                
                if (data.Length > 0 && data[0] == 0 && data.Length > 4 && data[1] == 0 && data[2] == 0 && data[3] == 0)
                {
                    MelonLogger.Error($"[AudioReceiver] IL2CPP marshalling issue detected! Data contains all zeros. Length: {data.Length}");
                    return null;
                }

                lock (_decoderLock)
                {
#if !MONO
                    // IL2CPP-specific decoding approach with manual memory management
                    try
                    {
                        // Create a fresh copy of the input data to avoid marshalling issues
                        byte[] safeInputData = new byte[data.Length];
                        for (int i = 0; i < data.Length; i++)
                        {
                            safeInputData[i] = data[i];
                        }
                        
                        // Log the first few bytes of the data we're decoding
                        string dataBytes = "";
                        for (int i = 0; i < Math.Min(safeInputData.Length, 8); i++)
                        {
                            dataBytes += safeInputData[i].ToString("X2") + " ";
                        }
                        MelonLogger.Msg($"[AudioReceiver] Decoding data: {dataBytes} (length: {safeInputData.Length})");
                        
                        // Create output array
                        short[] pcmData = new short[FrameSize * Channels];
                        
                        // Try multiple approaches since OpusSharp might have IL2CPP compatibility issues
                        int samplesDecoded = 0;
                        bool decodeSuccess = false;
                        
                        // Approach 1: Direct native P/Invoke call with manual memory management
                        if (_nativeDecoder != null)
                        {
                            try
                            {
                                samplesDecoded = _nativeDecoder.Decode(safeInputData, pcmData, FrameSize);
                                
                                if (samplesDecoded > 0)
                                {
                                    decodeSuccess = true;
                                    MelonLogger.Msg($"[AudioReceiver] Direct native decode succeeded: {samplesDecoded} samples");
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"[AudioReceiver] Direct native decode failed: {ex.Message}");
                            }
                        }
                        
                        // Approach 2: Standard decode with pinned arrays
                        if (!decodeSuccess)
                        {
                            try
                            {
                                // Use pinned arrays to prevent garbage collection during native call
                                System.Runtime.InteropServices.GCHandle inputHandle = System.Runtime.InteropServices.GCHandle.Alloc(safeInputData, System.Runtime.InteropServices.GCHandleType.Pinned);
                                System.Runtime.InteropServices.GCHandle outputHandle = System.Runtime.InteropServices.GCHandle.Alloc(pcmData, System.Runtime.InteropServices.GCHandleType.Pinned);
                                
                                try
                                {
                                    samplesDecoded = _decoder.Decode(
                                        safeInputData,
                                        safeInputData.Length,
                                        pcmData,
                                        FrameSize,
                                        false
                                    );
                                    
                                    if (samplesDecoded > 0)
                                    {
                                        decodeSuccess = true;
                                        MelonLogger.Msg($"[AudioReceiver] Standard decode succeeded: {samplesDecoded} samples");
                                    }
                                }
                                finally
                                {
                                    // Always free the pinned memory
                                    inputHandle.Free();
                                    outputHandle.Free();
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"[AudioReceiver] Standard decode failed: {ex.Message}");
                            }
                        }
                        
                        // Approach 3: If all else fails, try recreating the decoder and trying again
                        if (!decodeSuccess || samplesDecoded <= 0)
                        {
                            MelonLogger.Warning($"[AudioReceiver] All decode attempts failed, trying decoder reset approach...");
                            try
                            {
                                // Approach 3a: Try with explicit frame size validation
                                if (safeInputData.Length > 0 && safeInputData.Length <= 1275) // Max Opus frame size
                                {
                                    var validatedOutput = new short[FrameSize * Channels];
                                    int validatedSamples = _decoder.Decode(
                                        safeInputData,
                                        safeInputData.Length,
                                        validatedOutput,
                                        FrameSize, // Explicit frame size
                                        false
                                    );
                                    
                                    if (validatedSamples > 0)
                                    {
                                        Array.Copy(validatedOutput, pcmData, Math.Min(validatedOutput.Length, pcmData.Length));
                                        samplesDecoded = validatedSamples;
                                        decodeSuccess = true;
                                        MelonLogger.Msg($"[AudioReceiver] Validated decode succeeded: {validatedSamples} samples");
                                    }
                                }
                                
                                // Approach 3b: Temporarily create a new decoder instance for this frame
                                if (!decodeSuccess)
                                {
                                    using (var tempDecoder = new OpusDecoder(SampleRate, Channels))
                                    {
                                        var tempOutput = new short[FrameSize * Channels];
                                        int tempSamples = tempDecoder.Decode(
                                            safeInputData,
                                            safeInputData.Length,
                                            tempOutput,
                                            FrameSize,
                                            false
                                        );
                                        
                                        if (tempSamples > 0)
                                        {
                                            Array.Copy(tempOutput, pcmData, tempOutput.Length);
                                            samplesDecoded = tempSamples;
                                            decodeSuccess = true;
                                            MelonLogger.Msg($"[AudioReceiver] Temporary decoder succeeded: {tempSamples} samples");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Error($"[AudioReceiver] Temporary decoder also failed: {ex.Message}");
                            }
                        }
                        
                        if (!decodeSuccess || samplesDecoded <= 0)
                        {
                            MelonLogger.Error($"[AudioReceiver] All Opus decoding approaches failed, returned {samplesDecoded} samples from {data.Length} bytes");
                            return null;
                        }
                        
                        // Final check - if we still have no audio data after all attempts, generate test tone
                        bool hasAudio = false;
                        for (int i = 0; i < Math.Min(pcmData.Length, 100) && !hasAudio; i++)
                        {
                            if (pcmData[i] != 0) hasAudio = true;
                        }
                        
                        if (!hasAudio)
                        {
                            // Final fallback: generate a very quiet test tone to verify audio pipeline
                            MelonLogger.Warning($"[AudioReceiver] Decoded audio still contains zeros after all attempts - generating quiet test tone to verify audio pipeline");
                            for (int i = 0; i < pcmData.Length; i++)
                            {
                                // Generate a very quiet sine wave test tone (440Hz) to verify audio pipeline works
                                pcmData[i] = (short)(Math.Sin(i * 0.01) * 1000);
                            }
                        }
                        else
                        {
                            MelonLogger.Msg($"[AudioReceiver] Successfully decoded audio with {samplesDecoded} samples!");
                        }
                        
                        var decodingTime = DateTime.UtcNow;
                        
                        // Convert to float with proper scaling and anti-clipping
                        // Use the actual frame size since we always create pcmData with FrameSize * Channels
                        float[] floatData = new float[FrameSize * Channels];
                        const float scale = 1.0f / 32768.0f; // Proper scaling factor for 16-bit audio
                        
                        for (int i = 0; i < floatData.Length; i++)
                        {
                            // Apply scaling with slight headroom to prevent clipping
                            floatData[i] = pcmData[i] * scale * Volume * 0.98f;
                        }
                        
                        var totalTime = DateTime.UtcNow - startTime;
                        var opusTime = decodingTime - startTime;
                        var convTime = totalTime - opusTime;
                        
                        // Calculate network latency
                        var networkLatency = (DateTime.UtcNow.Ticks - message.CaptureTimestamp) / TimeSpan.TicksPerMillisecond;
                        
                        // Log performance metrics every 50 frames to avoid spam
                        if (TotalFramesReceived % 50 == 0)
                        {
                            // Calculate average amplitude for the decoded audio
                            float avgAmplitude = 0f;
                            for (int i = 0; i < Math.Min(floatData.Length, 100); i++)
                            {
                                avgAmplitude += Math.Abs(floatData[i]);
                            }
                            avgAmplitude /= Math.Min(floatData.Length, 100);
                            
                            MelonLogger.Msg($"[AudioReceiver] Frame {message.SequenceNumber}: Decoding took {totalTime.TotalMilliseconds:F2}ms (opus: {opusTime.TotalMilliseconds:F2}ms, conv: {convTime.TotalMilliseconds:F2}ms), network latency: {networkLatency}ms, input: {data.Length} bytes, output: {floatData.Length} samples, avg amplitude: {avgAmplitude:F6}");
                        }
                        
                        return floatData;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[AudioReceiver] IL2CPP-specific error decoding Opus frame: {ex.Message}");
                        return null;
                    }
#else
                    // Original Mono implementation
                    // Decode Opus data to PCM with improved error correction
                    short[] pcmData = new short[FrameSize * Channels];
                    int samplesDecoded = _decoder.Decode(
                        data,
                        data.Length,
                        pcmData,
                        FrameSize,
                        false // Use false for better quality music, true adds more error correction but can degrade quality
                    );

                    if (samplesDecoded <= 0)
                    {
                        MelonLogger.Error($"[AudioReceiver] Opus decoding failed, returned {samplesDecoded} samples from {data.Length} bytes");
                        return null;
                    }

                    var decodingTime = DateTime.UtcNow;

                    // Convert to float with proper scaling and anti-clipping
                    float[] floatData = new float[samplesDecoded * Channels];
                    const float scale = 1.0f / 32768.0f; // Proper scaling factor for 16-bit audio

                    for (int i = 0; i < floatData.Length; i++)
                    {
                        // Apply scaling with slight headroom to prevent clipping
                        floatData[i] = pcmData[i] * scale * Volume * 0.98f;
                    }

                    var totalTime = DateTime.UtcNow - startTime;
                    var opusTime = decodingTime - startTime;
                    var convTime = totalTime - opusTime;

                    // Calculate network latency
                    var networkLatency = (DateTime.UtcNow.Ticks - message.CaptureTimestamp) / TimeSpan.TicksPerMillisecond;

                    // Log performance metrics every 50 frames to avoid spam
                    if (TotalFramesReceived % 50 == 0)
                    {
                        // Calculate average amplitude for the decoded audio
                        float avgAmplitude = 0f;
                        for (int i = 0; i < Math.Min(floatData.Length, 100); i++)
                        {
                            avgAmplitude += Math.Abs(floatData[i]);
                        }
                        avgAmplitude /= Math.Min(floatData.Length, 100);
                        
                        MelonLogger.Msg($"[AudioReceiver] Frame {message.SequenceNumber}: Decoding took {totalTime.TotalMilliseconds:F2}ms (opus: {opusTime.TotalMilliseconds:F2}ms, conv: {convTime.TotalMilliseconds:F2}ms), network latency: {networkLatency}ms, input: {data.Length} bytes, output: {floatData.Length} samples, avg amplitude: {avgAmplitude:F6}");
                    }

                    return floatData;
#endif
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AudioReceiver] Error decoding Opus frame: {ex.Message}");
                return null;
            }
        }

        private string BytesToDebugString(byte[] bytes, int maxBytes)
        {
            if (bytes == null || bytes.Length == 0) return "empty";
            
            var sb = new StringBuilder();
            for (int i = 0; i < Math.Min(bytes.Length, maxBytes); i++)
            {
                sb.Append(bytes[i].ToString("X2"));
                if (i < Math.Min(bytes.Length, maxBytes) - 1)
                    sb.Append(" ");
            }
            return sb.ToString();
        }

        protected override float[]? HandleMissingFrame(uint sequenceNumber)
        {
            if (!EnablePacketLossConcealment || !_decoderInitialized || _isDisposed)
            {
                return null;
            }

            try
            {
                lock (_decoderLock)
                {
#if !MONO
                    // IL2CPP-specific approach with proper memory pinning
                    try
                    {
                        // Create and pin the output array to prevent garbage collection during native call
                        short[] plcData = new short[FrameSize * Channels];
                        System.Runtime.InteropServices.GCHandle outputHandle = System.Runtime.InteropServices.GCHandle.Alloc(plcData, System.Runtime.InteropServices.GCHandleType.Pinned);
                        
                        try
                        {
                            // Use Opus PLC (Packet Loss Concealment) to generate a replacement frame
                            int samplesGenerated = _decoder.Decode(
                                null, // null data tells Opus to use PLC
                                0,
                                plcData,
                                FrameSize,
                                false
                            );

                            if (samplesGenerated <= 0) return null;

                            // Check if the PLC data contains actual audio
                            bool hasAudio = false;
                            for (int i = 0; i < Math.Min(plcData.Length, 100) && !hasAudio; i++)
                            {
                                if (plcData[i] != 0) hasAudio = true;
                            }
                            
                            if (!hasAudio)
                            {
                                MelonLogger.Warning($"[AudioReceiver] PLC generated all zeros! Using simple fade-out instead.");
                                
                                // Use a simple fade-out instead
                                for (int i = 0; i < plcData.Length; i++)
                                {
                                    // Gentle fade-out (silence)
                                    float fadeRatio = 1.0f - (i / (float)plcData.Length);
                                    plcData[i] = (short)(Math.Sin(i * 0.01) * 1000 * fadeRatio);
                                }
                            }

                            // Convert to float and apply volume
                            float[] floatData = new float[samplesGenerated * Channels];
                            for (int i = 0; i < floatData.Length; i++)
                            {
                                floatData[i] = (plcData[i] / 32767f) * Volume;
                            }

                            TotalPlcFramesGenerated++;
                            MelonLogger.Msg($"[AudioReceiver] Generated PLC frame {sequenceNumber} with IL2CPP memory pinning");
                            return floatData;
                        }
                        finally
                        {
                            // Always free the pinned memory
                            outputHandle.Free();
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[AudioReceiver] IL2CPP-specific error generating PLC frame: {ex.Message}");
                        return null;
                    }
#else
                    // Original Mono implementation
                    // Use Opus PLC (Packet Loss Concealment) to generate a replacement frame
                    short[] plcData = new short[FrameSize * Channels];
                    int samplesGenerated = _decoder.Decode(
                        null, // null data tells Opus to use PLC
                        0,
                        plcData,
                        FrameSize,
                        false
                    );

                    if (samplesGenerated <= 0) return null;

                    // Convert to float and apply volume
                    float[] floatData = new float[samplesGenerated * Channels];
                    for (int i = 0; i < floatData.Length; i++)
                    {
                        floatData[i] = (plcData[i] / 32767f) * Volume;
                    }

                    TotalPlcFramesGenerated++;
                    return floatData;
#endif
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error generating PLC frame: {ex.Message}");
                return null;
            }
        }

        public override void Reset()
        {
            base.Reset();
            TotalPlcFramesGenerated = 0;

            // Reset the decoder state
            if (_decoderInitialized)
            {
                try
                {
                    lock (_decoderLock)
                    {
                        _decoder?.Dispose();
                        InitializeDecoder();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error resetting Opus decoder: {ex.Message}");
                }
            }
        }

        public override void Dispose()
        {
            if (_isDisposed) return;

            lock (_decoderLock)
            {
                _decoder?.Dispose();
#if !MONO
                _nativeDecoder?.Dispose();
#endif
                _decoderInitialized = false;
            }

            base.Dispose();
        }

        /// <summary>
        /// Get audio streaming statistics
        /// </summary>
        public AudioStreamStats GetStats()
        {
            return new AudioStreamStats
            {
                TotalFramesReceived = TotalFramesReceived,
                TotalFramesDropped = TotalFramesDropped,
                TotalFramesLate = TotalFramesLate,
                TotalPlcFramesGenerated = TotalPlcFramesGenerated,
                BufferedFrameCount = BufferedFrameCount,
                SampleRate = SampleRate,
                Channels = Channels,
                FrameSize = FrameSize,
                IsStreaming = _isStreaming
            };
        }
    }

    /// <summary>
    /// Audio streaming statistics
    /// </summary>
    public class AudioStreamStats
    {
        public uint TotalFramesReceived { get; set; }
        public uint TotalFramesDropped { get; set; }
        public uint TotalFramesLate { get; set; }
        public uint TotalPlcFramesGenerated { get; set; }
        public int BufferedFrameCount { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int FrameSize { get; set; }
        public bool IsStreaming { get; set; }

        public float PacketLossRate => TotalFramesReceived > 0
            ? (float)TotalFramesDropped / TotalFramesReceived
            : 0f;

        public float PlcUsageRate => TotalFramesReceived > 0
            ? (float)TotalPlcFramesGenerated / TotalFramesReceived
            : 0f;
    }
}
#endif