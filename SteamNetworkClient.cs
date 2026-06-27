using SteamNetworkLib.Core;
using SteamNetworkLib.Events;
using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Models;
using SteamNetworkLib.Sync;
using SteamNetworkLib.Utilities;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MemberInfo = SteamNetworkLib.Models.MemberInfo;
using System.Threading;

namespace SteamNetworkLib
{
    /// <summary>
    /// Main entry point for SteamNetworkLib - provides simplified access to all Steam networking features.
    /// Perfect for use in MelonLoader mods that need Steam lobby and P2P functionality.
    /// </summary>
    public class SteamNetworkClient : IDisposable
    {
        private bool _disposed = false;
        private bool _isInitialized = false;
        private bool _versionCheckEnabled = true;
        private readonly NetworkRules _rules;
        private readonly List<IDisposable> _syncVars = new List<IDisposable>();

        // Dedicated-server compatibility state
        private readonly Dictionary<ulong, MemberInfo> _virtualMembers = new Dictionary<ulong, MemberInfo>();
        private readonly Dictionary<string, string> _virtualLobbyData = new Dictionary<string, string>();
        private readonly Dictionary<ulong, Dictionary<string, string>> _virtualMemberData =
            new Dictionary<ulong, Dictionary<string, string>>();
        private DedicatedServerMessagingBridge? _dedicatedBridge;
        private NetworkSessionMode _sessionMode = NetworkSessionMode.None;
        private string _virtualSessionId = string.Empty;
        private CSteamID _virtualOwnerId = CSteamID.Nil;
        private CSteamID _virtualLocalPlayerId = CSteamID.Nil;
        private CSteamID _virtualServerSteamId = CSteamID.Nil;
        private DateTime _lastDedicatedBridgeAttachAttemptUtc = DateTime.MinValue;
        private DateTime _lastDedicatedRegisterAttemptUtc = DateTime.MinValue;
        private DateTime _lastDedicatedSnapshotUtc = DateTime.MinValue;
        private bool _dedicatedJoinEventRaised;

        private static readonly TimeSpan DedicatedRegisterRetryDelay = TimeSpan.FromSeconds(2);
        private static readonly JsonSyncSerializer DedicatedJsonSerializer = new JsonSyncSerializer();

        /// <summary>
        /// The internal data key used for storing SteamNetworkLib version information.
        /// </summary>
        private const string STEAMNETWORKLIB_VERSION_KEY = "__snl_version";

        /// <summary>
        /// Gets the current version of SteamNetworkLib.
        /// </summary>
        public static string LibraryVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        /// <summary>
        /// Gets or sets whether automatic version checking is enabled.
        /// When enabled, the library will automatically check for version compatibility between players.
        /// </summary>
        /// <remarks>
        /// <para><strong>IMPORTANT:</strong> Version checking is crucial for ensuring proper data transfer and synchronization between players.</para>
        /// <para>Disabling this feature or ignoring version mismatches may result in:</para>
        /// <list type="bullet">
        /// <item><description>Data serialization/deserialization failures</description></item>
        /// <item><description>Message format incompatibilities</description></item>
        /// <item><description>Synchronization errors</description></item>
        /// <item><description>Unexpected networking behavior or crashes</description></item>
        /// </list>
        /// <para>It is strongly recommended to keep this enabled and ensure all players use the same SteamNetworkLib version.</para>
        /// </remarks>
        public bool VersionCheckEnabled
        {
            get => _versionCheckEnabled;
            set => _versionCheckEnabled = value;
        }

        /// <summary>
        /// Gets whether this client has successfully initialized Steam networking.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This becomes <see langword="true"/> only after <see cref="Initialize"/> or
        /// <see cref="TryInitialize()"/> succeeds. It remains <see langword="false"/> when
        /// the game is launched without Steamworks, when <c>SteamAPI.Init()</c> has not run
        /// for the current process, or when initialization fails for any other reason.
        /// </para>
        /// <para>
        /// Use this before calling lobby, member-data, SyncVar, or P2P methods in mods that
        /// support optional multiplayer or delayed Steamworks startup.
        /// </para>
        /// </remarks>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Occurs when a version mismatch is detected between players in the lobby.
        /// </summary>
        /// <remarks>
        /// <para><strong>CRITICAL:</strong> This event indicates that players are using incompatible versions of SteamNetworkLib.</para>
        /// <para>Version mismatches can cause serious issues including data corruption, synchronization failures, and networking errors.</para>
        /// </remarks>
        public event EventHandler<VersionMismatchEventArgs>? OnVersionMismatch;

        /// <summary>
        /// Gets the lobby manager for handling Steam lobby operations.
        /// </summary>
        public SteamLobbyManager? LobbyManager { get; private set; }

        /// <summary>
        /// Gets the lobby data manager for handling lobby-wide data.
        /// </summary>
        public SteamLobbyData? LobbyData { get; private set; }

        /// <summary>
        /// Gets the member data manager for handling player-specific data.
        /// </summary>
        public SteamMemberData? MemberData { get; private set; }

        /// <summary>
        /// Gets the P2P manager for handling peer-to-peer communication.
        /// </summary>
        public SteamP2PManager? P2PManager { get; private set; }

        /// <summary>
        /// Gets whether the local player is currently in a lobby.
        /// </summary>
        public bool IsInLobby
        {
            get
            {
                if (_sessionMode == NetworkSessionMode.DedicatedRelay)
                {
                    return _virtualLocalPlayerId != CSteamID.Nil;
                }

                return LobbyManager?.IsInLobby ?? false;
            }
        }

        /// <summary>
        /// Gets whether the local player is the host of the current lobby.
        /// </summary>
        public bool IsHost
        {
            get
            {
                if (_sessionMode == NetworkSessionMode.DedicatedRelay)
                {
                    return _virtualOwnerId != CSteamID.Nil && _virtualOwnerId == _virtualLocalPlayerId;
                }

                return LobbyManager?.IsHost ?? false;
            }
        }

        /// <summary>
        /// Gets the Steam ID of the local player.
        /// </summary>
        public CSteamID LocalPlayerId
        {
            get
            {
                if (_sessionMode == NetworkSessionMode.DedicatedRelay)
                {
                    return _virtualLocalPlayerId;
                }

                return LobbyManager?.LocalPlayerID ?? CSteamID.Nil;
            }
        }

        /// <summary>
        /// Gets the 64-bit Steam ID of the local player, or 0 when unavailable.
        /// </summary>
        /// <remarks>
        /// Prefer this value for logs, dictionaries, config files, JSON payloads, and
        /// cross-runtime DTOs. Use <see cref="LocalPlayerId"/> only when calling APIs that
        /// require the Steamworks <c>CSteamID</c> type.
        /// </remarks>
        public ulong LocalPlayerId64 => LocalPlayerId.m_SteamID;

        /// <summary>
        /// Gets the 64-bit Steam ID of the current host, or 0 when not in a lobby/session.
        /// </summary>
        /// <remarks>
        /// This mirrors <see cref="CurrentLobby"/> ownership using a runtime-neutral
        /// primitive value suitable for serialization and comparison.
        /// </remarks>
        public ulong HostPlayerId64 => CurrentLobby?.OwnerId.m_SteamID ?? 0UL;

        /// <summary>
        /// Gets information about the current lobby, or null if not in a lobby.
        /// </summary>
        public LobbyInfo? CurrentLobby
        {
            get
            {
                if (_sessionMode == NetworkSessionMode.DedicatedRelay)
                {
                    if (_virtualLocalPlayerId == CSteamID.Nil)
                    {
                        return null;
                    }

                    CSteamID lobbyId = _virtualServerSteamId != CSteamID.Nil
                        ? _virtualServerSteamId
                        : (_virtualOwnerId != CSteamID.Nil ? _virtualOwnerId : _virtualLocalPlayerId);

                    return new LobbyInfo
                    {
                        LobbyId = lobbyId,
                        OwnerId = _virtualOwnerId,
                        MemberCount = _virtualMembers.Count,
                        MaxMembers = Math.Max(1, _virtualMembers.Count),
                        Name = "Dedicated Server Session",
                        CreatedAt = DateTime.UtcNow
                    };
                }

                return LobbyManager?.CurrentLobby;
            }
        }

        /// <summary>
        /// Occurs when the local player joins a lobby.
        /// </summary>
        public event EventHandler<LobbyJoinedEventArgs>? OnLobbyJoined;

        /// <summary>
        /// Occurs when a new lobby is created.
        /// </summary>
        public event EventHandler<LobbyCreatedEventArgs>? OnLobbyCreated;

        /// <summary>
        /// Occurs when the local player leaves a lobby.
        /// </summary>
        public event EventHandler<LobbyLeftEventArgs>? OnLobbyLeft;

        /// <summary>
        /// Occurs when a new member joins the current lobby.
        /// </summary>
        public event EventHandler<MemberJoinedEventArgs>? OnMemberJoined;

        /// <summary>
        /// Occurs when a member leaves the current lobby.
        /// </summary>
        public event EventHandler<MemberLeftEventArgs>? OnMemberLeft;

        /// <summary>
        /// Occurs when lobby data is changed.
        /// </summary>
        public event EventHandler<LobbyDataChangedEventArgs>? OnLobbyDataChanged;

        /// <summary>
        /// Occurs when member data is changed.
        /// </summary>
        public event EventHandler<MemberDataChangedEventArgs>? OnMemberDataChanged;

        /// <summary>
        /// Occurs when a P2P message is received from another player.
        /// </summary>
        public event EventHandler<P2PMessageReceivedEventArgs>? OnP2PMessageReceived;

        /// <summary>
        /// Current network rules applied to P2P behavior.
        /// </summary>
        public NetworkRules NetworkRules => _rules;

        /// <summary>
        /// Gets the currently active networking session mode.
        /// </summary>
        public NetworkSessionMode SessionMode => _sessionMode;

        /// <summary>
        /// Updates network rules at runtime and propagates to managers.
        /// </summary>
        public void UpdateNetworkRules(NetworkRules rules)
        {
            if (rules == null) return;
            if (P2PManager != null)
            {
                P2PManager.UpdateRules(rules);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamNetworkClient"/> class.
        /// Call <see cref="Initialize"/> before using any other methods.
        /// </summary>
        public SteamNetworkClient()
        {
            _rules = new NetworkRules();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamNetworkClient"/> class with custom <see cref="NetworkRules"/>.
        /// Call <see cref="Initialize"/> before using any other methods.
        /// </summary>
        public SteamNetworkClient(NetworkRules rules)
        {
            _rules = rules ?? new NetworkRules();
        }

        /// <summary>
        /// Initializes the Steam networking client. Must be called before using any other methods.
        /// </summary>
        /// <returns>True if initialization was successful.</returns>
        /// <exception cref="SteamNetworkException">Thrown when Steam is not available or when initialization fails.</exception>
        public bool Initialize()
        {
            if (_isInitialized)
            {
                return true;
            }

            try
            {
                if (!SteamNetworkUtils.IsSteamInitialized())
                {
                    throw new SteamNetworkException(
                        "Steam is not initialized. Make sure Steam is running and SteamAPI.Init() was called.",
                        SteamNetworkErrorKind.SteamUnavailable,
                        operation: nameof(Initialize),
                        isRetryable: true);
                }

                LobbyManager = new SteamLobbyManager();
                LobbyData = new SteamLobbyData(LobbyManager);
                MemberData = new SteamMemberData(LobbyManager);
                P2PManager = new SteamP2PManager(LobbyManager, _rules);

                SubscribeToEvents();
                AttachDedicatedBridgeIfAvailable();
                UpdateSessionMode(forceApplyOverrides: true);

                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                throw new SteamNetworkException(
                    $"Failed to initialize SteamNetworkClient: {ex.Message}",
                    ex,
                    ex is SteamNetworkException steamEx ? steamEx.ErrorKind : SteamNetworkErrorKind.SteamUnavailable,
                    operation: nameof(Initialize),
                    isRetryable: true);
            }
        }

        /// <summary>
        /// Attempts to initialize Steam networking without throwing when Steamworks is unavailable.
        /// </summary>
        /// <returns>True when initialization succeeded; otherwise, false.</returns>
        /// <remarks>
        /// <para>
        /// This is the preferred path for consumer mods that can run in single-player mode,
        /// retry initialization from a later game lifecycle hook, or tolerate launches where
        /// Steamworks never attaches to the process.
        /// </para>
        /// <para>
        /// When this method returns <see langword="false"/>, leave local-only gameplay active
        /// and retry later instead of calling networking methods. Use
        /// <see cref="TryInitialize(out SteamNetworkException?)"/> when the caller needs the
        /// failure reason for logging or diagnostics.
        /// </para>
        /// </remarks>
        public bool TryInitialize()
        {
            return TryInitialize(out _);
        }

        /// <summary>
        /// Attempts to initialize Steam networking without throwing when Steamworks is unavailable.
        /// </summary>
        /// <param name="error">The initialization error, or null when initialization succeeded.</param>
        /// <returns>True when initialization succeeded; otherwise, false.</returns>
        /// <remarks>
        /// This method wraps <see cref="Initialize"/> and converts initialization failures into
        /// a boolean result. It does not partially initialize managers on failure, so callers
        /// can safely retry after Steamworks becomes available.
        /// </remarks>
        public bool TryInitialize(out SteamNetworkException? error)
        {
            try
            {
                Initialize();
                error = null;
                return true;
            }
            catch (SteamNetworkException ex)
            {
                error = ex;
                return false;
            }
            catch (Exception ex)
            {
                error = new SteamNetworkException(
                    $"Failed to initialize SteamNetworkClient: {ex.Message}",
                    ex,
                    SteamNetworkErrorKind.SteamUnavailable,
                    operation: nameof(TryInitialize),
                    isRetryable: true);
                return false;
            }
        }

        #region Lobby Management

        /// <summary>
        /// Creates a new lobby with the specified settings.
        /// </summary>
        /// <param name="lobbyType">The type of lobby to create.</param>
        /// <param name="maxMembers">The maximum number of members allowed in the lobby.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the lobby information.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when lobby creation fails.</exception>
        public async Task<LobbyInfo> CreateLobbyAsync(ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly, int maxMembers = 4)
        {
            EnsureInitialized();
            ClearVirtualSessionState(emitLobbyLeft: true);
            UpdateSessionMode(forceApplyOverrides: true);
            return await LobbyManager.CreateLobbyAsync(lobbyType, maxMembers);
        }

        /// <summary>
        /// Joins an existing lobby by ID.
        /// </summary>
        /// <param name="lobbyId">The Steam ID of the lobby to join.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the lobby information.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when joining the lobby fails.</exception>
        public async Task<LobbyInfo> JoinLobbyAsync(CSteamID lobbyId)
        {
            EnsureInitialized();
            ClearVirtualSessionState(emitLobbyLeft: true);
            UpdateSessionMode(forceApplyOverrides: true);
            return await LobbyManager.JoinLobbyAsync(lobbyId);
        }

        /// <summary>
        /// Leaves the current lobby.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <remarks>
        /// This method has no effect if the local player is not currently in a lobby.
        /// </remarks>
        public void LeaveLobby()
        {
            EnsureInitialized();
            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                ClearVirtualSessionState(emitLobbyLeft: true);
                UpdateSessionMode(forceApplyOverrides: true);
                return;
            }

            LobbyManager.LeaveLobby();
        }

        /// <summary>
        /// Gets all members in the current lobby.
        /// </summary>
        /// <returns>A list of <see cref="MemberInfo"/> objects for all players in the lobby.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <remarks>
        /// Returns an empty list if not currently in a lobby.
        /// </remarks>
        public List<MemberInfo> GetLobbyMembers()
        {
            EnsureInitialized();
            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                return _virtualMembers.Values.Select(CloneMember).ToList();
            }

            return LobbyManager.GetLobbyMembers();
        }

        /// <summary>
        /// Gets all non-local members in the current lobby/session.
        /// </summary>
        /// <returns>A list of remote lobby members. Returns an empty list when there are no remote members.</returns>
        public List<MemberInfo> GetRemoteMembers()
        {
            return GetLobbyMembers()
                .Where(member => !member.IsLocalPlayer)
                .ToList();
        }

        /// <summary>
        /// Gets the current host member, or null when no host can be identified.
        /// </summary>
        public MemberInfo? GetHostMember()
        {
            return GetLobbyMembers().FirstOrDefault(member => member.IsOwner);
        }

        /// <summary>
        /// Attempts to get the current host member.
        /// </summary>
        /// <param name="host">The host member when found; otherwise, null.</param>
        /// <returns>True when a host member was found.</returns>
        public bool TryGetHostMember(out MemberInfo? host)
        {
            host = GetHostMember();
            return host != null;
        }

        /// <summary>
        /// Gets the local member entry, or null when it cannot be found.
        /// </summary>
        public MemberInfo? GetLocalMember()
        {
            return GetLobbyMembers().FirstOrDefault(member => member.IsLocalPlayer);
        }

        /// <summary>
        /// Attempts to get the local member entry.
        /// </summary>
        /// <param name="localMember">The local member when found; otherwise, null.</param>
        /// <returns>True when the local member was found.</returns>
        public bool TryGetLocalMember(out MemberInfo? localMember)
        {
            localMember = GetLocalMember();
            return localMember != null;
        }

        /// <summary>
        /// Gets a lobby member by their 64-bit Steam ID.
        /// </summary>
        /// <param name="steamId64">The 64-bit Steam ID to find.</param>
        /// <returns>The matching member, or null when no member matches.</returns>
        public MemberInfo? GetMember(ulong steamId64)
        {
            return GetLobbyMembers().FirstOrDefault(member => member.SteamId.m_SteamID == steamId64);
        }

        /// <summary>
        /// Attempts to get a lobby member by their 64-bit Steam ID.
        /// </summary>
        /// <param name="steamId64">The 64-bit Steam ID to find.</param>
        /// <param name="member">The matching member when found; otherwise, null.</param>
        /// <returns>True when a matching member was found.</returns>
        public bool TryGetMember(ulong steamId64, out MemberInfo? member)
        {
            member = GetMember(steamId64);
            return member != null;
        }

        /// <summary>
        /// Invites a friend to the current lobby.
        /// </summary>
        /// <param name="friendId">The Steam ID of the friend to invite.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or invitation fails.</exception>
        public void InviteFriend(CSteamID friendId)
        {
            EnsureInitialized();
            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                throw new LobbyException("Cannot invite friends while connected to a dedicated-server session");
            }

            LobbyManager.InviteFriend(friendId);
        }

        /// <summary>
        /// Opens the Steam overlay invite dialog.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or the overlay fails to open.</exception>
        public void OpenInviteDialog()
        {
            EnsureInitialized();
            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                throw new LobbyException("Cannot open invite dialog while connected to a dedicated-server session");
            }

            LobbyManager.OpenInviteDialog();
        }

        #endregion

        #region Data Management

        /// <summary>
        /// Sets lobby-wide data that is accessible to all players.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <param name="value">The data value.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or not the lobby owner.</exception>
        /// <remarks>
        /// Only the lobby owner can set lobby data. This method will fail silently if called by a non-owner.
        /// </remarks>
        public void SetLobbyData(string key, string value)
        {
            EnsureInitialized();

            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                ValidateDataKey(key);
                if (!IsHost)
                {
                    return;
                }

                SendDedicatedLobbyDataUpdate(key, value ?? string.Empty);
                return;
            }

            LobbyData.SetData(key, value);
        }

        /// <summary>
        /// Gets lobby-wide data.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <returns>The data value, or null if not found.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public string? GetLobbyData(string key)
        {
            EnsureInitialized();

            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                ValidateDataKey(key);
                return _virtualLobbyData.TryGetValue(key, out var value) ? value : null;
            }

            return LobbyData.GetData(key);
        }

        /// <summary>
        /// Sets data for the local player that is visible to all players.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <param name="value">The data value.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public void SetMyData(string key, string value)
        {
            EnsureInitialized();

            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                ValidateDataKey(key);
                SendDedicatedMemberDataUpdate(key, value ?? string.Empty);
                return;
            }

            MemberData.SetMemberData(key, value);
        }

        /// <summary>
        /// Gets data for the local player.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <returns>The data value, or null if not found.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public string? GetMyData(string key)
        {
            EnsureInitialized();

            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                ValidateDataKey(key);
                return GetVirtualMemberData(LocalPlayerId, key);
            }

            return MemberData.GetMemberData(key);
        }

        /// <summary>
        /// Gets data for a specific player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player.</param>
        /// <param name="key">The data key.</param>
        /// <returns>The data value, or null if not found.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or the player is not in the lobby.</exception>
        public string? GetPlayerData(CSteamID playerId, string key)
        {
            EnsureInitialized();

            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                ValidateDataKey(key);
                return GetVirtualMemberData(playerId, key);
            }

            return MemberData.GetMemberData(playerId, key);
        }

        /// <summary>
        /// Gets the same data key for all players in the lobby.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <returns>A dictionary mapping player Steam IDs to their data values.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public Dictionary<CSteamID, string> GetDataForAllPlayers(string key)
        {
            EnsureInitialized();

            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                ValidateDataKey(key);
                var result = new Dictionary<CSteamID, string>();
                foreach (var member in _virtualMembers.Values)
                {
                    string? value = GetVirtualMemberData(member.SteamId, key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        result[member.SteamId] = value;
                    }
                }

                return result;
            }

            return MemberData.GetMemberDataForAllPlayers(key);
        }

        /// <summary>
        /// Sets multiple data values at once for the local player.
        /// </summary>
        /// <param name="data">A dictionary containing key-value pairs to set.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public void SetMyDataBatch(Dictionary<string, string> data)
        {
            EnsureInitialized();

            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                if (data == null || data.Count == 0)
                {
                    return;
                }

                foreach (var kvp in data)
                {
                    SetMyData(kvp.Key, kvp.Value);
                }

                return;
            }

            MemberData.SetMemberDataBatch(data);
        }

        #endregion

        #region P2P Communication

        /// <summary>
        /// Sends a message to a specific player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the target player.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the message was sent successfully.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        public async Task<bool> SendMessageToPlayerAsync(CSteamID playerId, P2PMessage message)
        {
            EnsureInitialized();
            return await P2PManager.SendMessageAsync(playerId, message);
        }

        /// <summary>
        /// Sends a large byte payload to a specific player using reliable file-transfer chunks.
        /// </summary>
        /// <param name="playerId">The Steam ID of the target player.</param>
        /// <param name="transferName">A developer-facing name for the transfer.</param>
        /// <param name="data">The bytes to send.</param>
        /// <param name="channel">The communication channel to use.</param>
        /// <param name="chunkSize">Optional chunk payload size. When null, SteamNetworkLib calculates the largest safe reliable payload size.</param>
        /// <returns>A task whose result indicates whether every chunk was accepted by Steam for sending.</returns>
        public async Task<bool> SendLargeDataToPlayerAsync(CSteamID playerId, string transferName, byte[] data, int channel = 0, int? chunkSize = null)
        {
            EnsureInitialized();
            return await P2PManager.SendLargeDataAsync(playerId, transferName, data, channel, chunkSize);
        }

        /// <summary>
        /// Sends a message to all players in the lobby.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This method sends the message to each player individually.
        /// </remarks>
        public async Task BroadcastMessageAsync(P2PMessage message)
        {
            EnsureInitialized();
            
            var members = GetLobbyMembers();
            var sendTasks = new List<Task<bool>>();
            CSteamID localPlayerId = LocalPlayerId;
            
            // Create a task for each send operation
            foreach (var member in members)
            {
                if (member.SteamId != localPlayerId)
                {
                    sendTasks.Add(P2PManager.SendMessageAsync(member.SteamId, message));
                }
            }
            
            // Wait for all send operations to complete
            if (sendTasks.Count > 0)
            {
                await Task.WhenAll(sendTasks);
            }
        }

        /// <summary>
        /// Sends a message to all players in the lobby. This is a non-async wrapper around BroadcastMessageAsync.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This method is provided for backward compatibility. For new code, use BroadcastMessageAsync instead.
        /// </remarks>
        public void BroadcastMessage(P2PMessage message)
        {
            EnsureInitialized();
            
            // Fire and forget the async operation
            // This isn't ideal, but maintains backward compatibility
            _ = BroadcastMessageAsync(message);
        }

        /// <summary>
        /// Sends a simple text message to a player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the target player.</param>
        /// <param name="text">The text message to send.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the message was sent successfully.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This is a convenience method that creates a <see cref="TextMessage"/> internally.
        /// </remarks>
        public async Task<bool> SendTextMessageAsync(CSteamID playerId, string text)
        {
            EnsureInitialized();
            var message = new TextMessage { Content = text };
            return await P2PManager.SendMessageAsync(playerId, message);
        }

        /// <summary>
        /// Broadcasts a simple text message to all players.
        /// </summary>
        /// <param name="text">The text message to broadcast.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This is a convenience method that creates a <see cref="TextMessage"/> internally.
        /// </remarks>
        public async Task BroadcastTextMessageAsync(string text)
        {
            EnsureInitialized();
            var message = new TextMessage { Content = text };
            await BroadcastMessageAsync(message);
        }

        /// <summary>
        /// Broadcasts a simple text message to all players. This is a non-async wrapper around BroadcastTextMessageAsync.
        /// </summary>
        /// <param name="text">The text message to broadcast.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This method is provided for backward compatibility. For new code, use BroadcastTextMessageAsync instead.
        /// </remarks>
        public void BroadcastTextMessage(string text)
        {
            EnsureInitialized();
            
            // Fire and forget the async operation
            // This isn't ideal, but maintains backward compatibility
            _ = BroadcastTextMessageAsync(text);
        }

        /// <summary>
        /// Sends a data synchronization message to a player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the target player.</param>
        /// <param name="key">The data key.</param>
        /// <param name="value">The data value.</param>
        /// <param name="dataType">The data type identifier.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the message was sent successfully.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This is a convenience method that creates a <see cref="DataSyncMessage"/> internally.
        /// </remarks>
        public async Task<bool> SendDataSyncAsync(CSteamID playerId, string key, string value, string dataType = "string")
        {
            EnsureInitialized();
            var message = new DataSyncMessage
            {
                Key = key,
                Value = value,
                DataType = dataType
            };
            return await P2PManager.SendMessageAsync(playerId, message);
        }

        /// <summary>
        /// Registers a handler for a specific message type.
        /// </summary>
        /// <typeparam name="T">The type of message to handle.</typeparam>
        /// <param name="handler">The handler function that will be called when messages of this type are received.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <remarks>
        /// The handler will be called with the message and the sender's Steam ID.
        /// </remarks>
        public void RegisterMessageHandler<T>(Action<T, CSteamID> handler) where T : P2PMessage, new()
        {
            EnsureInitialized();
            P2PManager.RegisterMessageHandler(handler);
        }

        /// <summary>
        /// Registers a handler for a specific message type and returns a subscription that removes only that handler.
        /// </summary>
        /// <typeparam name="T">The type of message to handle.</typeparam>
        /// <param name="handler">The handler function that will be called when messages of this type are received.</param>
        /// <returns>A disposable subscription for this handler.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        public IDisposable SubscribeMessageHandler<T>(Action<T, CSteamID> handler) where T : P2PMessage, new()
        {
            EnsureInitialized();
            return P2PManager.SubscribeMessageHandler(handler);
        }

        /// <summary>
        /// Creates a helper for correlated P2P request/response messages.
        /// </summary>
        /// <typeparam name="TRequest">The request message type.</typeparam>
        /// <typeparam name="TResponse">The response message type.</typeparam>
        /// <param name="defaultTimeout">Optional default timeout for requests sent by the helper.</param>
        /// <returns>A request/response helper bound to this client.</returns>
        /// <remarks>
        /// Use this for host-approved actions such as checkout requests, permission checks,
        /// lock acquisition, and other flows where the caller needs a single answer from a
        /// specific peer. Dispose the returned helper when the mod unloads or no longer uses
        /// the message pair.
        /// </remarks>
        public P2PRequestResponseClient<TRequest, TResponse> CreateRequestResponseClient<TRequest, TResponse>(TimeSpan? defaultTimeout = null)
            where TRequest : P2PMessage, IP2PCorrelatedMessage, new()
            where TResponse : P2PMessage, IP2PResponseMessage, new()
        {
            EnsureInitialized();
            return new P2PRequestResponseClient<TRequest, TResponse>(this, defaultTimeout);
        }

        /// <summary>
        /// Processes incoming P2P packets. Call this regularly (e.g., in Update()).
        /// </summary>
        /// <remarks>
        /// This method should be called frequently to ensure timely processing of incoming messages.
        /// If not called regularly, messages may be delayed or dropped.
        /// </remarks>
        public void ProcessIncomingMessages()
        {
            if (!_isInitialized) return;

            AttachDedicatedBridgeIfAvailable();
            UpdateSessionMode(forceApplyOverrides: false);
            TryRegisterDedicatedSession(force: false);

#if IL2CPP
            // CRITICAL: Must call SteamAPI.RunCallbacks() in IL2CPP to process Steam callbacks
            // This is what actually triggers P2P packet reception callbacks
            try
            {
                SteamAPI.RunCallbacks();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamNetworkLib] Error running Steam callbacks: {ex.Message}");
            }
#endif

            P2PManager.ProcessIncomingPackets();
        }

        #endregion

        #region High-Level Helper Methods

        /// <summary>
        /// Synchronizes data with all players in the lobby.
        /// Useful for mod compatibility checks and data synchronization.
        /// </summary>
        /// <param name="dataKey">The data key to synchronize.</param>
        /// <param name="dataValue">The data value to synchronize.</param>
        /// <param name="dataType">The data type identifier.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        /// <remarks>
        /// This method both sets the local player's data and broadcasts it to all other players.
        /// </remarks>
        public async Task SyncModDataWithAllPlayersAsync(string dataKey, string dataValue, string dataType = "string")
        {
            EnsureInitialized();

            SetMyData(dataKey, dataValue);

            var syncMessage = new DataSyncMessage
            {
                Key = dataKey,
                Value = dataValue,
                DataType = dataType
            };
            
            await BroadcastMessageAsync(syncMessage);
        }

        /// <summary>
        /// Synchronizes data with all players in the lobby. This is a non-async wrapper around SyncModDataWithAllPlayersAsync.
        /// </summary>
        /// <param name="dataKey">The data key to synchronize.</param>
        /// <param name="dataValue">The data value to synchronize.</param>
        /// <param name="dataType">The data type identifier.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        /// <remarks>
        /// This method is provided for backward compatibility. For new code, use SyncModDataWithAllPlayersAsync instead.
        /// </remarks>
        public void SyncModDataWithAllPlayers(string dataKey, string dataValue, string dataType = "string")
        {
            EnsureInitialized();

            SetMyData(dataKey, dataValue);

            var syncMessage = new DataSyncMessage
            {
                Key = dataKey,
                Value = dataValue,
                DataType = dataType
            };
            BroadcastMessage(syncMessage);
        }

        /// <summary>
        /// Checks if all players have compatible mod data for a given key.
        /// </summary>
        /// <param name="dataKey">The data key to check for compatibility.</param>
        /// <returns>True if all players have the same data value, false otherwise.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        /// <remarks>
        /// This method is useful for verifying that all players are using the same mod version.
        /// </remarks>
        public bool IsModDataCompatible(string dataKey)
        {
            EnsureInitialized();

            var allPlayerData = GetDataForAllPlayers(dataKey);
            if (allPlayerData.Count <= 1) return true;

            var values = allPlayerData.Values.Distinct().ToArray();
            return values.Length == 1;
        }

        /// <summary>
        /// Sets the SteamNetworkLib version information for the local player.
        /// This is automatically called when joining or creating lobbies if version checking is enabled.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        internal void SetLibraryVersionData()
        {
            EnsureInitialized();
            SetMyData(STEAMNETWORKLIB_VERSION_KEY, LibraryVersion);
        }

        /// <summary>
        /// Performs a comprehensive version check and fires the OnVersionMismatch event if incompatibilities are found.
        /// </summary>
        /// <returns>True if all versions are compatible, false if mismatches were detected.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public bool CheckLibraryVersionCompatibility()
        {
            EnsureInitialized();

            if (!_versionCheckEnabled)
            {
                return true; // Skip check if disabled
            }

            var playerVersions = GetDataForAllPlayers(STEAMNETWORKLIB_VERSION_KEY);
            
            // If no version data exists yet, consider it compatible
            if (playerVersions.Count == 0)
            {
                return true;
            }

            // Check for version mismatches
            var distinctVersions = playerVersions.Values.Distinct().ToList();
            if (distinctVersions.Count <= 1)
            {
                return true; // All versions are the same
            }

            // Version mismatch detected - find incompatible players
            var localVersion = LibraryVersion;
            var incompatiblePlayers = new List<CSteamID>();

            foreach (var kvp in playerVersions)
            {
                if (kvp.Value != localVersion)
                {
                    incompatiblePlayers.Add(kvp.Key);
                }
            }

            // Fire the version mismatch event
            try
            {
                OnVersionMismatch?.Invoke(this, new VersionMismatchEventArgs(localVersion, playerVersions, incompatiblePlayers));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in version mismatch event handler: {ex.Message}");
            }

            // Log the version mismatch
            var versionsInfo = string.Join(", ", playerVersions.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            Console.WriteLine($"[SteamNetworkLib] WARNING: Version mismatch detected! Local version: {localVersion}, Player versions: {versionsInfo}");

            return false;
        }

        /// <summary>
        /// Gets the SteamNetworkLib versions of all players in the lobby.
        /// </summary>
        /// <returns>A dictionary mapping player Steam IDs to their SteamNetworkLib versions. Players without version data are excluded.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        /// <remarks>
        /// <para>Use this method to identify which players have different library versions that could cause data transfer issues.</para>
        /// <para>Players with missing version data may be using older versions of SteamNetworkLib that don't support version checking,</para>
        /// <para>which could result in unpredictable networking behavior and synchronization failures.</para>
        /// </remarks>
        public Dictionary<CSteamID, string> GetPlayerLibraryVersions()
        {
            EnsureInitialized();
            return GetDataForAllPlayers(STEAMNETWORKLIB_VERSION_KEY);
        }

        #endregion

        #region Synchronized Variables

        /// <summary>
        /// Creates a host-authoritative synchronized variable.
        /// </summary>
        /// <typeparam name="T">The type of value to synchronize.</typeparam>
        /// <param name="key">A unique key for this sync variable.</param>
        /// <param name="defaultValue">The default value when no synced value exists.</param>
        /// <param name="options">Optional configuration options.</param>
        /// <param name="validator">Optional validator for value constraints.</param>
        /// <returns>A new <see cref="HostSyncVar{T}"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="ArgumentException">Thrown when key is null or empty.</exception>
        /// <exception cref="SyncSerializationException">Thrown when the type T cannot be serialized.</exception>
        /// <remarks>
        /// <para><strong>Authority:</strong> Only the lobby host can modify this value.
        /// Non-host writes are silently ignored (or logged if <see cref="NetworkSyncOptions.WarnOnIgnoredWrites"/> is enabled).</para>
        /// 
        /// <para><strong>Storage:</strong> Uses Steam lobby data, automatically synced by Steam to all lobby members.</para>
        /// 
        /// <para><strong>Use Cases:</strong> Game settings, round numbers, match state, or any host-controlled state.</para>
        /// 
        /// <para><strong>Validation:</strong> Optional validator can enforce constraints on values (e.g., ranges, formats).</para>
        /// 
        /// <example>
        /// <code>
        /// // Create a host-authoritative sync var
        /// var roundNumber = client.CreateHostSyncVar("Round", 1);
        /// 
        /// // With validation
        /// var scoreValidator = new RangeValidator&lt;int&gt;(0, 1000);
        /// var score = client.CreateHostSyncVar("Score", 0, null, scoreValidator);
        /// 
        /// // Subscribe to changes
        /// roundNumber.OnValueChanged += (oldVal, newVal) => 
        ///     MelonLogger.Msg($"Round: {oldVal} -> {newVal}");
        /// 
        /// // Only host can modify - silently ignored for non-hosts
        /// roundNumber.Value = 2;
        /// </code>
        /// </example>
        /// </remarks>
        public HostSyncVar<T> CreateHostSyncVar<T>(string key, T defaultValue, NetworkSyncOptions? options = null, ISyncValidator<T>? validator = null)
        {
            EnsureInitialized();
            var syncVar = new HostSyncVar<T>(this, key, defaultValue, options, validator);
            _syncVars.Add(syncVar);
            return syncVar;
        }

        /// <summary>
        /// Creates a client-owned synchronized variable where each client can set their own value.
        /// </summary>
        /// <typeparam name="T">The type of value to synchronize.</typeparam>
        /// <param name="key">A unique key for this sync variable.</param>
        /// <param name="defaultValue">The default value for clients who haven't set a value.</param>
        /// <param name="options">Optional configuration options.</param>
        /// <param name="validator">Optional validator for value constraints.</param>
        /// <returns>A new <see cref="ClientSyncVar{T}"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="ArgumentException">Thrown when key is null or empty.</exception>
        /// <exception cref="SyncSerializationException">Thrown when the type T cannot be serialized.</exception>
        /// <remarks>
        /// <para><strong>Authority:</strong> Each client can only modify their own value.
        /// All clients can read all other clients' values.</para>
        /// 
        /// <para><strong>Storage:</strong> Uses Steam lobby member data, automatically synced by Steam.</para>
        /// 
        /// <para><strong>Use Cases:</strong> Ready status, player loadouts, per-client preferences.</para>
        /// 
        /// <para><strong>Validation:</strong> Optional validator can enforce constraints on values.</para>
        /// 
        /// <example>
        /// <code>
        /// // Create a client-owned sync var
        /// var isReady = client.CreateClientSyncVar("Ready", false);
        /// 
        /// // With validation
        /// var teamValidator = new RangeValidator&lt;int&gt;(1, 4);
        /// var team = client.CreateClientSyncVar("Team", 1, null, teamValidator);
        /// 
        /// // Subscribe to any client's changes
        /// isReady.OnValueChanged += (playerId, oldVal, newVal) => 
        ///     MelonLogger.Msg($"Player {playerId} ready: {newVal}");
        /// 
        /// // Set my own value
        /// isReady.Value = true;
        /// 
        /// // Read another player's value
        /// bool player2Ready = isReady.GetValue(player2Id);
        /// 
        /// // Get all players' values
        /// var allReady = isReady.GetAllValues();
        /// bool everyoneReady = allReady.Values.All(r => r);
        /// </code>
        /// </example>
        /// </remarks>
        public ClientSyncVar<T> CreateClientSyncVar<T>(string key, T defaultValue, NetworkSyncOptions? options = null, ISyncValidator<T>? validator = null)
        {
            EnsureInitialized();
            var syncVar = new ClientSyncVar<T>(this, key, defaultValue, options, validator);
            _syncVars.Add(syncVar);
            return syncVar;
        }

        #endregion
        /// <summary>
        /// Subscribes to internal manager events and forwards them to client events.
        /// </summary>
        /// <remarks>
        /// This method is called during initialization to set up event forwarding.
        /// </remarks>
        private void SubscribeToEvents()
        {
            // Simple event forwarding
            LobbyManager.OnLobbyJoined += (s, e) =>
            {
                UpdateSessionMode(forceApplyOverrides: true);
                OnLobbyJoined?.Invoke(this, e);
            };
            LobbyManager.OnLobbyCreated += (s, e) =>
            {
                UpdateSessionMode(forceApplyOverrides: true);
                OnLobbyCreated?.Invoke(this, e);
            };
            LobbyManager.OnLobbyLeft += (s, e) =>
            {
                // Auto-dispose all sync vars when leaving lobby
                DisposeSyncVars();
                UpdateSessionMode(forceApplyOverrides: true);
                OnLobbyLeft?.Invoke(this, e);
            };
            LobbyManager.OnMemberJoined += (s, e) => OnMemberJoined?.Invoke(this, e);
            LobbyManager.OnMemberLeft += (s, e) => OnMemberLeft?.Invoke(this, e);
            // Two sources are intentional:
            // LobbyData.OnLobbyDataChanged → immediate local notification when host calls SetData() (same frame)
            // LobbyManager.OnLobbyDataChanged → reliable Steam LobbyDataUpdate_t path for all clients (incl. non-host)
            // HostSyncVar.HandleLobbyDataChanged deduplicates via equality check; other consumers may receive both events.
            LobbyData.OnLobbyDataChanged += (s, e) => OnLobbyDataChanged?.Invoke(this, e);
            LobbyManager.OnLobbyDataChanged += (s, e) => OnLobbyDataChanged?.Invoke(this, e);
            MemberData.OnMemberDataChanged += (s, e) => OnMemberDataChanged?.Invoke(this, e);
            P2PManager.OnMessageReceived += (s, e) => OnP2PMessageReceived?.Invoke(this, e);

            // Add version checking if enabled
            if (!_versionCheckEnabled) return;
            LobbyManager.OnLobbyJoined += (s, e) => SafeExecute(() => SetLibraryVersionData(), "setting version data on lobby join");
            LobbyManager.OnLobbyCreated += (s, e) => SafeExecute(() => SetLibraryVersionData(), "setting version data on lobby creation");
            LobbyManager.OnMemberJoined += (s, e) => SafeExecute(async () => { await Task.Delay(500); CheckLibraryVersionCompatibility(); }, "checking version compatibility");
            OnMemberDataChanged += (s, e) =>
            {
                if (e.Key == STEAMNETWORKLIB_VERSION_KEY)
                {
                    SafeExecute(() => CheckLibraryVersionCompatibility(), "checking version compatibility on data change");
                }
            };
        }

        private void AttachDedicatedBridgeIfAvailable()
        {
            if (_dedicatedBridge != null)
            {
                return;
            }

            if (DateTime.UtcNow - _lastDedicatedBridgeAttachAttemptUtc < DedicatedRegisterRetryDelay)
            {
                return;
            }

            _lastDedicatedBridgeAttachAttemptUtc = DateTime.UtcNow;
            _dedicatedBridge = DedicatedServerMessagingBridge.TryCreate();
            if (_dedicatedBridge == null)
            {
                return;
            }

            _dedicatedBridge.MessageReceived += OnDedicatedBridgeMessageReceived;
            TryRegisterDedicatedSession(force: true);
        }

        private void OnDedicatedBridgeMessageReceived(string command, string data)
        {
            if (string.IsNullOrEmpty(command))
            {
                return;
            }

            if (string.Equals(command, DedicatedCompatibilityProtocol.SnapshotCommand, StringComparison.Ordinal))
            {
                HandleDedicatedSnapshot(data);
                return;
            }

            if (string.Equals(command, DedicatedCompatibilityProtocol.MemberJoinedCommand, StringComparison.Ordinal))
            {
                HandleDedicatedMemberJoined(data);
                return;
            }

            if (string.Equals(command, DedicatedCompatibilityProtocol.MemberLeftCommand, StringComparison.Ordinal))
            {
                HandleDedicatedMemberLeft(data);
                return;
            }

            if (string.Equals(command, DedicatedCompatibilityProtocol.LobbyDataChangedCommand, StringComparison.Ordinal))
            {
                HandleDedicatedLobbyDataChanged(data);
                return;
            }

            if (string.Equals(command, DedicatedCompatibilityProtocol.MemberDataChangedCommand, StringComparison.Ordinal))
            {
                HandleDedicatedMemberDataChanged(data);
                return;
            }

            if (string.Equals(command, DedicatedCompatibilityProtocol.P2PMessageCommand, StringComparison.Ordinal))
            {
                HandleDedicatedP2PMessage(data);
                return;
            }

            if ((string.Equals(command, "auth_result", StringComparison.Ordinal) ||
                 string.Equals(command, "server_data", StringComparison.Ordinal)) &&
                !IsDedicatedSessionFresh())
            {
                TryRegisterDedicatedSession(force: true);
            }
        }

        private void TryRegisterDedicatedSession(bool force)
        {
            if (_dedicatedBridge == null)
            {
                return;
            }

            if (IsDedicatedSessionFresh())
            {
                return;
            }

            if (LobbyManager?.IsInLobby == true)
            {
                return;
            }

            if (!force && !_dedicatedBridge.IsDedicatedContextLikely && _sessionMode != NetworkSessionMode.DedicatedRelay)
            {
                return;
            }

            if (!force && DateTime.UtcNow - _lastDedicatedRegisterAttemptUtc < DedicatedRegisterRetryDelay)
            {
                return;
            }

            var register = new DedicatedCompatibilityProtocol.RegisterRequest
            {
                LibraryVersion = LibraryVersion
            };

            string payload = DedicatedJsonSerializer.Serialize(register);
            _lastDedicatedRegisterAttemptUtc = DateTime.UtcNow;
            _dedicatedBridge.TrySendToServer(DedicatedCompatibilityProtocol.RegisterCommand, payload);
        }

        private void HandleDedicatedSnapshot(string data)
        {
            DedicatedCompatibilityProtocol.SnapshotPayload? snapshot;
            try
            {
                snapshot = DedicatedJsonSerializer.Deserialize<DedicatedCompatibilityProtocol.SnapshotPayload>(data ?? string.Empty);
            }
            catch
            {
                return;
            }

            if (snapshot == null)
            {
                return;
            }

            var previousMembers = new Dictionary<ulong, MemberInfo>(_virtualMembers);
            _virtualMembers.Clear();
            _virtualLobbyData.Clear();
            _virtualMemberData.Clear();

            _virtualSessionId = snapshot.SessionId ?? string.Empty;
            _virtualLocalPlayerId = ParseSteamIdOrNil(snapshot.LocalSteamId);
            _virtualOwnerId = ParseSteamIdOrNil(snapshot.OwnerSteamId);
            _virtualServerSteamId = ParseSteamIdOrNil(snapshot.ServerSteamId);
            _lastDedicatedSnapshotUtc = DateTime.UtcNow;

            if (snapshot.LobbyData != null)
            {
                foreach (var kvp in snapshot.LobbyData)
                {
                    _virtualLobbyData[kvp.Key] = kvp.Value ?? string.Empty;
                }
            }

            if (snapshot.MemberData != null)
            {
                foreach (var memberKvp in snapshot.MemberData)
                {
                    if (!TryParseSteamId(memberKvp.Key, out var memberId))
                    {
                        continue;
                    }

                    var map = new Dictionary<string, string>();
                    if (memberKvp.Value != null)
                    {
                        foreach (var kvp in memberKvp.Value)
                        {
                            map[kvp.Key] = kvp.Value ?? string.Empty;
                        }
                    }

                    _virtualMemberData[memberId.m_SteamID] = map;
                }
            }

            if (snapshot.Members != null)
            {
                foreach (var memberSnapshot in snapshot.Members)
                {
                    UpsertVirtualMember(memberSnapshot, raiseJoinedEvent: false);
                }
            }

            RefreshVirtualOwnerFlags();

            UpdateSessionMode(forceApplyOverrides: true);
            RaiseDedicatedLobbyJoinedIfNeeded();

            foreach (var kvp in _virtualMembers)
            {
                if (!previousMembers.ContainsKey(kvp.Key) && kvp.Value.SteamId != LocalPlayerId)
                {
                    OnMemberJoined?.Invoke(this, new MemberJoinedEventArgs(CloneMember(kvp.Value)));
                }
            }

            foreach (var kvp in previousMembers)
            {
                if (!_virtualMembers.ContainsKey(kvp.Key) && kvp.Value.SteamId != LocalPlayerId)
                {
                    OnMemberLeft?.Invoke(this, new MemberLeftEventArgs(CloneMember(kvp.Value), "Left dedicated session"));
                }
            }
        }

        private void HandleDedicatedMemberJoined(string data)
        {
            var payload = DedicatedJsonSerializer.Deserialize<DedicatedCompatibilityProtocol.MemberJoinedPayload>(data ?? string.Empty);
            if (payload?.Member == null)
            {
                return;
            }

            _virtualOwnerId = ParseSteamIdOrNil(payload.OwnerSteamId);
            _lastDedicatedSnapshotUtc = DateTime.UtcNow;
            UpdateSessionMode(forceApplyOverrides: false);
            UpsertVirtualMember(payload.Member, raiseJoinedEvent: true);
            RefreshVirtualOwnerFlags();
        }

        private void HandleDedicatedMemberLeft(string data)
        {
            var payload = DedicatedJsonSerializer.Deserialize<DedicatedCompatibilityProtocol.MemberLeftPayload>(data ?? string.Empty);
            if (payload == null || !TryParseSteamId(payload.SteamId, out var memberId))
            {
                return;
            }

            _virtualOwnerId = ParseSteamIdOrNil(payload.OwnerSteamId);
            _lastDedicatedSnapshotUtc = DateTime.UtcNow;

            if (_virtualMembers.TryGetValue(memberId.m_SteamID, out var existing))
            {
                _virtualMembers.Remove(memberId.m_SteamID);
                _virtualMemberData.Remove(memberId.m_SteamID);
                if (existing.SteamId != LocalPlayerId)
                {
                    OnMemberLeft?.Invoke(this, new MemberLeftEventArgs(CloneMember(existing), "Left dedicated session"));
                }
            }

            RefreshVirtualOwnerFlags();
            UpdateSessionMode(forceApplyOverrides: false);
        }

        private void HandleDedicatedLobbyDataChanged(string data)
        {
            var payload = DedicatedJsonSerializer.Deserialize<DedicatedCompatibilityProtocol.LobbyDataChangedPayload>(data ?? string.Empty);
            if (payload == null || string.IsNullOrEmpty(payload.Key))
            {
                return;
            }

            string? oldValue = _virtualLobbyData.TryGetValue(payload.Key, out var cached) ? cached : null;
            if (payload.NewValue == null)
            {
                _virtualLobbyData.Remove(payload.Key);
            }
            else
            {
                _virtualLobbyData[payload.Key] = payload.NewValue;
            }

            CSteamID changedBy = ParseSteamIdOrNil(payload.ChangedBySteamId);
            OnLobbyDataChanged?.Invoke(this, new LobbyDataChangedEventArgs(payload.Key, oldValue, payload.NewValue, changedBy));
        }

        private void HandleDedicatedMemberDataChanged(string data)
        {
            var payload = DedicatedJsonSerializer.Deserialize<DedicatedCompatibilityProtocol.MemberDataChangedPayload>(data ?? string.Empty);
            if (payload == null || string.IsNullOrEmpty(payload.MemberSteamId) || string.IsNullOrEmpty(payload.Key))
            {
                return;
            }

            if (!TryParseSteamId(payload.MemberSteamId, out var memberId))
            {
                return;
            }

            Dictionary<string, string> map = GetOrCreateVirtualMemberMap(memberId.m_SteamID);
            string? oldValue = map.TryGetValue(payload.Key, out var oldCached) ? oldCached : null;

            if (payload.NewValue == null)
            {
                map.Remove(payload.Key);
            }
            else
            {
                map[payload.Key] = payload.NewValue;
            }

            OnMemberDataChanged?.Invoke(this, new MemberDataChangedEventArgs(memberId, payload.Key, oldValue, payload.NewValue));
        }

        private void HandleDedicatedP2PMessage(string data)
        {
            var payload = DedicatedJsonSerializer.Deserialize<DedicatedCompatibilityProtocol.P2PMessagePayload>(data ?? string.Empty);
            if (payload == null || string.IsNullOrEmpty(payload.SenderSteamId) || string.IsNullOrEmpty(payload.DataBase64))
            {
                return;
            }

            if (!TryParseSteamId(payload.SenderSteamId, out var senderId))
            {
                return;
            }

            byte[] packet;
            try
            {
                packet = Convert.FromBase64String(payload.DataBase64);
            }
            catch
            {
                return;
            }

            P2PManager?.ProcessExternalPacket(senderId, packet, payload.Channel);
        }

        private void SendDedicatedLobbyDataUpdate(string key, string value)
        {
            if (_dedicatedBridge == null)
            {
                return;
            }

            var request = new DedicatedCompatibilityProtocol.SetLobbyDataRequest
            {
                Key = key,
                Value = value
            };

            _dedicatedBridge.TrySendToServer(
                DedicatedCompatibilityProtocol.SetLobbyDataCommand,
                DedicatedJsonSerializer.Serialize(request));
        }

        private void SendDedicatedMemberDataUpdate(string key, string value)
        {
            if (_dedicatedBridge == null)
            {
                return;
            }

            var request = new DedicatedCompatibilityProtocol.SetMemberDataRequest
            {
                Key = key,
                Value = value
            };

            _dedicatedBridge.TrySendToServer(
                DedicatedCompatibilityProtocol.SetMemberDataCommand,
                DedicatedJsonSerializer.Serialize(request));
        }

        private Task<bool> SendPacketViaDedicatedAsync(CSteamID targetId, byte[] packetData, int channel, EP2PSend sendType)
        {
            if (_sessionMode != NetworkSessionMode.DedicatedRelay || _dedicatedBridge == null)
            {
                return Task.FromResult(false);
            }

            var request = new DedicatedCompatibilityProtocol.P2PSendRequest
            {
                TargetSteamId = targetId == CSteamID.Nil ? string.Empty : targetId.m_SteamID.ToString(CultureInfo.InvariantCulture),
                DataBase64 = Convert.ToBase64String(packetData),
                Channel = channel
            };

            bool sent = _dedicatedBridge.TrySendToServer(
                DedicatedCompatibilityProtocol.P2PSendCommand,
                DedicatedJsonSerializer.Serialize(request));
            return Task.FromResult(sent);
        }

        private void UpdateSessionMode(bool forceApplyOverrides)
        {
            NetworkSessionMode nextMode = DetermineSessionMode();
            if (_sessionMode == nextMode && !forceApplyOverrides)
            {
                return;
            }

            if (_sessionMode == NetworkSessionMode.DedicatedRelay && nextMode != NetworkSessionMode.DedicatedRelay)
            {
                bool shouldEmitLeft = nextMode == NetworkSessionMode.None;
                ClearVirtualSessionState(emitLobbyLeft: shouldEmitLeft);
            }

            _sessionMode = nextMode;
            ConfigureP2POverridesForCurrentMode();
        }

        private NetworkSessionMode DetermineSessionMode()
        {
            if (LobbyManager?.IsInLobby == true)
            {
                return NetworkSessionMode.LobbyP2P;
            }

            if (_virtualLocalPlayerId != CSteamID.Nil && IsDedicatedSessionFresh())
            {
                return NetworkSessionMode.DedicatedRelay;
            }

            return NetworkSessionMode.None;
        }

        private bool IsDedicatedSessionFresh()
        {
            return _lastDedicatedSnapshotUtc != DateTime.MinValue && _virtualLocalPlayerId != CSteamID.Nil;
        }

        private void ConfigureP2POverridesForCurrentMode()
        {
            if (P2PManager == null)
            {
                return;
            }

            if (_sessionMode == NetworkSessionMode.DedicatedRelay)
            {
                P2PManager.ConfigureOverrides(
                    SendPacketViaDedicatedAsync,
                    () => _virtualMembers.Values.Select(CloneMember).ToList(),
                    () => LocalPlayerId,
                    () => IsInLobby);
            }
            else
            {
                P2PManager.ConfigureOverrides(null, null, null, null);
            }
        }

        private void RaiseDedicatedLobbyJoinedIfNeeded()
        {
            if (_sessionMode != NetworkSessionMode.DedicatedRelay || _dedicatedJoinEventRaised)
            {
                return;
            }

            LobbyInfo? currentLobby = CurrentLobby;
            if (currentLobby == null)
            {
                return;
            }

            _dedicatedJoinEventRaised = true;
            OnLobbyJoined?.Invoke(this, new LobbyJoinedEventArgs(currentLobby));
        }

        private void ClearVirtualSessionState(bool emitLobbyLeft)
        {
            if (emitLobbyLeft && _dedicatedJoinEventRaised)
            {
                CSteamID lobbyId = _virtualServerSteamId != CSteamID.Nil
                    ? _virtualServerSteamId
                    : (_virtualOwnerId != CSteamID.Nil ? _virtualOwnerId : _virtualLocalPlayerId);
                OnLobbyLeft?.Invoke(this, new LobbyLeftEventArgs(lobbyId, "Dedicated session ended"));
            }

            _virtualMembers.Clear();
            _virtualLobbyData.Clear();
            _virtualMemberData.Clear();
            _virtualSessionId = string.Empty;
            _virtualOwnerId = CSteamID.Nil;
            _virtualLocalPlayerId = CSteamID.Nil;
            _virtualServerSteamId = CSteamID.Nil;
            _lastDedicatedSnapshotUtc = DateTime.MinValue;
            _dedicatedJoinEventRaised = false;
        }

        private void UpsertVirtualMember(DedicatedCompatibilityProtocol.MemberSnapshot snapshot, bool raiseJoinedEvent)
        {
            if (snapshot == null || !TryParseSteamId(snapshot.SteamId, out var memberId))
            {
                return;
            }

            bool isLocal = _virtualLocalPlayerId != CSteamID.Nil
                ? memberId == _virtualLocalPlayerId
                : snapshot.IsLocalPlayer;

            var member = new MemberInfo
            {
                SteamId = memberId,
                DisplayName = snapshot.DisplayName ?? string.Empty,
                IsOwner = _virtualOwnerId != CSteamID.Nil
                    ? memberId == _virtualOwnerId
                    : snapshot.IsOwner,
                IsLocalPlayer = isLocal,
                JoinedAt = FromUnixMillisecondsOrNow(snapshot.JoinedAtUnixMs)
            };

            bool existed = _virtualMembers.ContainsKey(memberId.m_SteamID);
            _virtualMembers[memberId.m_SteamID] = member;

            if (!existed && raiseJoinedEvent && member.SteamId != LocalPlayerId)
            {
                OnMemberJoined?.Invoke(this, new MemberJoinedEventArgs(CloneMember(member)));
            }
        }

        private string? GetVirtualMemberData(CSteamID playerId, string key)
        {
            if (!_virtualMemberData.TryGetValue(playerId.m_SteamID, out var memberData))
            {
                return null;
            }

            if (!memberData.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            {
                return null;
            }

            return value;
        }

        private Dictionary<string, string> GetOrCreateVirtualMemberMap(ulong playerSteamId)
        {
            if (!_virtualMemberData.TryGetValue(playerSteamId, out var map))
            {
                map = new Dictionary<string, string>();
                _virtualMemberData[playerSteamId] = map;
            }

            return map;
        }

        private void RefreshVirtualOwnerFlags()
        {
            foreach (var kvp in _virtualMembers)
            {
                kvp.Value.IsOwner = _virtualOwnerId != CSteamID.Nil && kvp.Value.SteamId == _virtualOwnerId;
            }
        }

        private static MemberInfo CloneMember(MemberInfo source)
        {
            return new MemberInfo
            {
                SteamId = source.SteamId,
                DisplayName = source.DisplayName,
                IsOwner = source.IsOwner,
                IsLocalPlayer = source.IsLocalPlayer,
                JoinedAt = source.JoinedAt
            };
        }

        private static DateTime FromUnixMillisecondsOrNow(long unixMs)
        {
            if (unixMs <= 0)
            {
                return DateTime.UtcNow;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        private static void ValidateDataKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Data key cannot be null or empty", nameof(key));
            }

            if (key.Length > 255)
            {
                throw new ArgumentException("Data key cannot exceed 255 characters", nameof(key));
            }

            if (key.StartsWith("__steam_", StringComparison.Ordinal))
            {
                throw new ArgumentException("Data key cannot start with '__steam_' (reserved by Steam)", nameof(key));
            }
        }

        private static bool TryParseSteamId(string? raw, out CSteamID steamId)
        {
            steamId = CSteamID.Nil;
            if (!ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) || value == 0)
            {
                return false;
            }

            steamId = new CSteamID(value);
            return true;
        }

        private static CSteamID ParseSteamIdOrNil(string? raw)
        {
            return TryParseSteamId(raw, out var steamId) ? steamId : CSteamID.Nil;
        }

        private void SafeExecute(Action action, string operation)
        {
            try { action(); }
            catch (Exception ex) { Console.WriteLine($"[SteamNetworkLib] Warning: Failed {operation}: {ex.Message}"); }
        }

        private void SafeExecute(Func<Task> action, string operation)
        {
            Task.Run(async () => { try { await action(); } catch (Exception ex) { Console.WriteLine($"[SteamNetworkLib] Warning: Failed {operation}: {ex.Message}"); } });
        }

        /// <summary>
        /// Disposes all registered sync vars. Called automatically when leaving a lobby.
        /// </summary>
        private void DisposeSyncVars()
        {
            foreach (var syncVar in _syncVars)
            {
                try
                {
                    syncVar?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SteamNetworkLib] Warning: Failed to dispose sync var: {ex.Message}");
                }
            }
            _syncVars.Clear();
        }

        /// <summary>
        /// Unsubscribes from events from the component managers.
        /// </summary>
        /// <remarks>
        /// This method is called during disposal to clean up event handlers.
        /// </remarks>
        private void UnsubscribeFromEvents()
        {
            // Clear our own event handlers
            OnLobbyJoined = null;
            OnLobbyCreated = null;
            OnLobbyLeft = null;
            OnMemberJoined = null;
            OnMemberLeft = null;
            OnLobbyDataChanged = null;
            OnMemberDataChanged = null;
            OnP2PMessageReceived = null;
            OnVersionMismatch = null;
        }

        /// <summary>
        /// Ensures that the client is initialized before performing operations.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("SteamNetworkClient is not initialized. Call Initialize() first.");
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="SteamNetworkClient"/>.
        /// </summary>
        /// <remarks>
        /// This method disposes all component managers and releases any unmanaged resources.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Dispose all sync vars first
                DisposeSyncVars();

                // Unsubscribe from all events
                UnsubscribeFromEvents();

                // Then dispose components
                P2PManager?.Dispose();
                MemberData?.Dispose();
                LobbyData?.Dispose();
                LobbyManager?.Dispose();
                _dedicatedBridge?.Dispose();
                _dedicatedBridge = null;
                ClearVirtualSessionState(emitLobbyLeft: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing SteamNetworkClient: {ex.Message}");
            }

            _disposed = true;
            
            // Suppress finalization as there's nothing for the GC to clean up
            GC.SuppressFinalize(this);
        }
    }
}
