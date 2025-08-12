using SteamNetworkLib.Events;
using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Models;
using SteamNetworkLib.Utilities;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SteamNetworkLib.Core
{
    /// <summary>
    /// Manages Steam lobby operations including creation, joining, leaving, and member management.
    /// Provides core functionality for handling Steam lobbies and their associated events.
    /// </summary>
    public class SteamLobbyManager : IDisposable
    {
        private bool _disposed = false;
        private LobbyInfo? _currentLobby;
        private readonly List<MemberInfo> _lobbyMembers = new List<MemberInfo>();

        // Steam callbacks
        private Callback<LobbyCreated_t>? _lobbyCreatedCallback;
        private Callback<LobbyEnter_t>? _lobbyEnteredCallback;
        private Callback<LobbyChatUpdate_t>? _chatUpdateCallback;
        private Callback<GameLobbyJoinRequested_t>? _lobbyJoinRequestedCallback;

        // Task completion sources for async operations
        private TaskCompletionSource<LobbyInfo>? _createLobbyTcs;
        private TaskCompletionSource<LobbyInfo>? _joinLobbyTcs;

        /// <summary>
        /// Gets information about the current lobby, or null if not in a lobby.
        /// </summary>
        public LobbyInfo? CurrentLobby => _currentLobby;

        /// <summary>
        /// Gets the Steam ID of the local player.
        /// </summary>
        public CSteamID LocalPlayerID { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the local player is currently in a lobby.
        /// </summary>
        public bool IsInLobby => _currentLobby != null && SteamNetworkUtils.IsValidSteamID(_currentLobby.LobbyId);

        /// <summary>
        /// Gets a value indicating whether the local player is the host of the current lobby.
        /// </summary>
        public bool IsHost => IsInLobby && _currentLobby!.OwnerId == LocalPlayerID;

        /// <summary>
        /// Occurs when the local player joins a lobby.
        /// </summary>
        public event EventHandler<LobbyJoinedEventArgs>? OnLobbyJoined;

        /// <summary>
        /// Occurs when a new lobby is successfully created.
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
        /// Initializes a new instance of the <see cref="SteamLobbyManager"/> class.
        /// </summary>
        /// <exception cref="SteamNetworkException">Thrown when Steam is not initialized.</exception>
        public SteamLobbyManager()
        {
            InitializeSteam();
        }

        /// <summary>
        /// Creates a new Steam lobby with the specified settings.
        /// </summary>
        /// <param name="lobbyType">The type of lobby to create (public, friends only, etc.).</param>
        /// <param name="maxMembers">The maximum number of members allowed in the lobby.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the created lobby information.</returns>
        /// <exception cref="LobbyException">Thrown when lobby creation fails or is already in progress.</exception>
        public async Task<LobbyInfo> CreateLobbyAsync(ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly, int maxMembers = 4)
        {
            if (_createLobbyTcs != null && !_createLobbyTcs.Task.IsCompleted)
            {
                throw new LobbyException("A lobby creation is already in progress");
            }

            if (IsInLobby)
            {
                LeaveLobby();
            }

            _createLobbyTcs = new TaskCompletionSource<LobbyInfo>();

            var apiCall = SteamMatchmaking.CreateLobby(lobbyType, maxMembers);
            if (apiCall == SteamAPICall_t.Invalid)
            {
                _createLobbyTcs.SetException(new LobbyException("Failed to create lobby - Steam API call failed"));
                return await _createLobbyTcs.Task;
            }

            // Wait for callback with timeout
            var timeoutTask = Task.Delay(10000); // 10 second timeout
            var completedTask = await Task.WhenAny(_createLobbyTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _createLobbyTcs.SetException(new LobbyException("Lobby creation timed out"));
            }

            return await _createLobbyTcs.Task;
        }

        /// <summary>
        /// Joins an existing Steam lobby by its ID.
        /// </summary>
        /// <param name="lobbyId">The Steam ID of the lobby to join.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the joined lobby information.</returns>
        /// <exception cref="LobbyException">Thrown when the lobby ID is invalid, join fails, or is already in progress.</exception>
        public async Task<LobbyInfo> JoinLobbyAsync(CSteamID lobbyId)
        {
            if (!SteamNetworkUtils.IsValidSteamID(lobbyId))
            {
                throw new LobbyException("Invalid lobby ID");
            }

            if (_joinLobbyTcs != null && !_joinLobbyTcs.Task.IsCompleted)
            {
                throw new LobbyException("A lobby join is already in progress");
            }

            if (IsInLobby)
            {
                LeaveLobby();
            }

            _joinLobbyTcs = new TaskCompletionSource<LobbyInfo>();

            SteamMatchmaking.JoinLobby(lobbyId);

            // Wait for callback with timeout
            var timeoutTask = Task.Delay(10000); // 10 second timeout
            var completedTask = await Task.WhenAny(_joinLobbyTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _joinLobbyTcs.SetException(new LobbyException("Lobby join timed out"));
            }

            return await _joinLobbyTcs.Task;
        }

        /// <summary>
        /// Leaves the current lobby if the local player is in one.
        /// </summary>
        public void LeaveLobby()
        {
            if (!IsInLobby) return;

            var lobbyId = _currentLobby!.LobbyId;
            var reason = "Player left voluntarily";

            SteamMatchmaking.LeaveLobby(lobbyId);

            _currentLobby = null;
            _lobbyMembers.Clear();

            OnLobbyLeft?.Invoke(this, new LobbyLeftEventArgs(lobbyId, reason));
        }

        /// <summary>
        /// Gets a list of all members currently in the lobby.
        /// </summary>
        /// <returns>A list of member information for all players in the lobby, or an empty list if not in a lobby.</returns>
        public List<MemberInfo> GetLobbyMembers()
        {
            if (!IsInLobby)
                return new List<MemberInfo>();

            UpdateLobbyMembers();
            return new List<MemberInfo>(_lobbyMembers);
        }

        /// <summary>
        /// Invites a friend to the current lobby.
        /// </summary>
        /// <param name="friendId">The Steam ID of the friend to invite.</param>
        /// <exception cref="LobbyException">Thrown when not in a lobby or the friend ID is invalid.</exception>
        public void InviteFriend(CSteamID friendId)
        {
            if (!IsInLobby)
            {
                throw new LobbyException("Cannot invite friend - not in a lobby");
            }

            if (!SteamNetworkUtils.IsValidSteamID(friendId))
            {
                throw new LobbyException("Invalid friend Steam ID");
            }

            SteamMatchmaking.InviteUserToLobby(_currentLobby!.LobbyId, friendId);
        }

        /// <summary>
        /// Opens the Steam overlay invite dialog for inviting friends to the current lobby.
        /// </summary>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public void OpenInviteDialog()
        {
            if (!IsInLobby)
            {
                throw new LobbyException("Cannot open invite dialog - not in a lobby");
            }

            SteamFriends.ActivateGameOverlayInviteDialog(_currentLobby!.LobbyId);
        }

        private void InitializeSteam()
        {
            if (!SteamNetworkUtils.IsSteamInitialized())
            {
                throw new SteamNetworkException("Steam is not initialized. Make sure Steam is running and SteamAPI.Init() was called.");
            }

            LocalPlayerID = SteamUser.GetSteamID();

            // Initialize Steam callbacks
#if IL2CPP
            _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(new System.Action<LobbyCreated_t>(OnLobbyCreatedCallback));
            _lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(new System.Action<LobbyEnter_t>(OnLobbyEnteredCallback));
            _chatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(new System.Action<LobbyChatUpdate_t>(OnChatUpdateCallback));
            _lobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(new System.Action<GameLobbyJoinRequested_t>(OnLobbyJoinRequestedCallback));
#else
            _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
            _lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
            _chatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnChatUpdateCallback);
            _lobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequestedCallback);
#endif
        }

        private void OnLobbyCreatedCallback(LobbyCreated_t result)
        {
            try
            {
                if (result.m_eResult != EResult.k_EResultOK)
                {
                    _createLobbyTcs?.SetException(new LobbyException($"Failed to create lobby: {SteamNetworkUtils.FormatSteamResult(result.m_eResult)}"));
                    return;
                }

                var lobbyId = new CSteamID(result.m_ulSteamIDLobby);
                var lobby = CreateLobbyInfo(lobbyId);

                _currentLobby = lobby;

                // Set initial lobby data
                SteamMatchmaking.SetLobbyData(lobbyId, "owner", LocalPlayerID.ToString());
                SteamMatchmaking.SetLobbyData(lobbyId, "created_at", DateTime.UtcNow.ToString("O"));

                UpdateLobbyMembers();

                _createLobbyTcs?.SetResult(lobby);
                OnLobbyCreated?.Invoke(this, new LobbyCreatedEventArgs(lobby, result.m_eResult));
            }
            catch (Exception ex)
            {
                _createLobbyTcs?.SetException(new LobbyException($"Error in lobby created callback: {ex.Message}", ex));
            }
        }

        private void OnLobbyEnteredCallback(LobbyEnter_t result)
        {
            try
            {
                var lobbyId = new CSteamID(result.m_ulSteamIDLobby);

                // Check for entry response (errors)
                if ((result.m_EChatRoomEnterResponse & (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) == 0)
                {
                    var errorMsg = $"Failed to enter lobby: {result.m_EChatRoomEnterResponse}";
                    _joinLobbyTcs?.SetException(new LobbyException(errorMsg));
                    return;
                }

                var lobby = CreateLobbyInfo(lobbyId);
                _currentLobby = lobby;

                UpdateLobbyMembers();

                _joinLobbyTcs?.SetResult(lobby);
                OnLobbyJoined?.Invoke(this, new LobbyJoinedEventArgs(lobby));
            }
            catch (Exception ex)
            {
                _joinLobbyTcs?.SetException(new LobbyException($"Error in lobby entered callback: {ex.Message}", ex));
            }
        }

        private void OnChatUpdateCallback(LobbyChatUpdate_t result)
        {
            if (!IsInLobby) return;

            try
            {
                var changedUserId = new CSteamID(result.m_ulSteamIDUserChanged);
                var changeMaker = new CSteamID(result.m_ulSteamIDMakingChange);

                // Check if the lobby owner left and we need to leave too
                if (changeMaker == _currentLobby!.LobbyId &&
                    changedUserId != LocalPlayerID &&
                    (result.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0)
                {
                    LeaveLobby();
                    return;
                }

                // Create defensive copies before any modifications
                var previousMembers = new List<MemberInfo>(_lobbyMembers);
                UpdateLobbyMembers();

                // Create copies to avoid collection modification during enumeration
                var currentMembers = new List<MemberInfo>(_lobbyMembers);
                var currentMemberIds = currentMembers.Select(m => m.SteamId).ToHashSet();
                var previousMemberIds = previousMembers.Select(m => m.SteamId).ToHashSet();

                // Find new members - iterate over defensive copy
                foreach (var member in currentMembers)
                {
                    if (!previousMemberIds.Contains(member.SteamId))
                    {
                        OnMemberJoined?.Invoke(this, new MemberJoinedEventArgs(member));
                    }
                }

                // Find left members - iterate over defensive copy
                foreach (var previousMember in previousMembers)
                {
                    if (!currentMemberIds.Contains(previousMember.SteamId))
                    {
                        OnMemberLeft?.Invoke(this, new MemberLeftEventArgs(previousMember, "Left lobby"));
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Console.WriteLine($"Error in chat update callback: {ex.Message}");
            }
        }

        private void OnLobbyJoinRequestedCallback(GameLobbyJoinRequested_t result)
        {
            try
            {
                var lobbyId = result.m_steamIDLobby;

                // Auto-join requested lobby (could be made configurable)
                if (IsInLobby)
                {
                    LeaveLobby();
                }

                // Use the async method but don't await it here since this is a callback
                _ = JoinLobbyAsync(lobbyId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in lobby join requested callback: {ex.Message}");
            }
        }

        private LobbyInfo CreateLobbyInfo(CSteamID lobbyId)
        {
            var ownerData = SteamMatchmaking.GetLobbyData(lobbyId, "owner");
            var ownerId = string.IsNullOrEmpty(ownerData) ? SteamMatchmaking.GetLobbyOwner(lobbyId) : new CSteamID(ulong.Parse(ownerData));

            return new LobbyInfo
            {
                LobbyId = lobbyId,
                OwnerId = ownerId,
                MemberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId),
                MaxMembers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId),
                Name = SteamMatchmaking.GetLobbyData(lobbyId, "name"),
                CreatedAt = DateTime.UtcNow
            };
        }

        private void UpdateLobbyMembers()
        {
            if (!IsInLobby) return;

            _lobbyMembers.Clear();

            var lobbyId = _currentLobby!.LobbyId;
            var memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

            for (int i = 0; i < memberCount; i++)
            {
                var memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);
                if (memberId == CSteamID.Nil) continue;

                var memberInfo = new MemberInfo
                {
                    SteamId = memberId,
                    DisplayName = SteamNetworkUtils.GetPlayerName(memberId),
                    IsOwner = memberId == _currentLobby.OwnerId,
                    IsLocalPlayer = memberId == LocalPlayerID,
                    JoinedAt = DateTime.UtcNow // Could be stored in lobby member data
                };

                _lobbyMembers.Add(memberInfo);
            }
        }

        /// <summary>
        /// Releases all resources used by the SteamLobbyManager.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (IsInLobby)
                {
                    LeaveLobby();
                }

                // Dispose Steam callbacks
                _lobbyCreatedCallback?.Dispose();
                _lobbyEnteredCallback?.Dispose();
                _chatUpdateCallback?.Dispose();
                _lobbyJoinRequestedCallback?.Dispose();

                // Cancel any pending operations
                _createLobbyTcs?.TrySetCanceled();
                _joinLobbyTcs?.TrySetCanceled();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing SteamLobbyManager: {ex.Message}");
            }

            _disposed = true;
        }
    }
}