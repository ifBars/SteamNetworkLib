using SteamNetworkLib.Exceptions;
using System;

namespace SteamNetworkLib.Utilities
{
    /// <summary>
    /// Owns the common SteamNetworkLib initialize-retry and per-frame message-pump loop.
    /// </summary>
    /// <remarks>
    /// This helper is intended for MelonLoader mods that want networking to be optional.
    /// Create it during mod initialization, call <see cref="Tick"/> from `OnUpdate`, and
    /// keep local/single-player behavior active until <see cref="IsAvailable"/> is true.
    /// </remarks>
    public sealed class SteamNetworkClientRunner : IDisposable
    {
        private readonly ISteamNetworkClientLifecycle _client;
        private readonly SteamNetworkClientRunnerOptions _options;
        private readonly Func<DateTime> _utcNowProvider;
        private DateTime _nextRetryUtc = DateTime.MinValue;
        private bool _hasAttemptedInitialization;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamNetworkClientRunner"/> class.
        /// </summary>
        /// <param name="client">The network client lifecycle to run.</param>
        /// <param name="options">Optional runner configuration.</param>
        public SteamNetworkClientRunner(
            ISteamNetworkClientLifecycle client,
            SteamNetworkClientRunnerOptions? options = null)
            : this(client, options, () => DateTime.UtcNow)
        {
        }

        internal SteamNetworkClientRunner(
            ISteamNetworkClientLifecycle client,
            SteamNetworkClientRunnerOptions? options,
            Func<DateTime> utcNowProvider)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = options ?? new SteamNetworkClientRunnerOptions();
            _utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));

            if (_options.RetryInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "RetryInterval must be greater than zero.");
            }
        }

        /// <summary>
        /// Occurs once when initialization succeeds through this runner.
        /// </summary>
        public event Action? OnInitialized;

        /// <summary>
        /// Occurs when an initialization attempt fails.
        /// </summary>
        public event Action<SteamNetworkException?>? OnInitializationFailed;

        /// <summary>
        /// Occurs when message processing throws.
        /// </summary>
        public event Action<Exception>? OnProcessingFailed;

        /// <summary>
        /// Gets a value indicating whether networking is initialized and available.
        /// </summary>
        public bool IsAvailable => !_disposed && _client.IsInitialized;

        /// <summary>
        /// Advances the runner by one update tick.
        /// </summary>
        /// <returns>True when networking is available after the tick; otherwise, false.</returns>
        public bool Tick()
        {
            ThrowIfDisposed();

            if (_client.IsInitialized)
            {
                ProcessMessages();
                return true;
            }

            if (!_options.RetryUntilInitialized && _hasAttemptedInitialization)
            {
                return false;
            }

            var now = _utcNowProvider();
            if (now < _nextRetryUtc)
            {
                return false;
            }

            return TryInitializeNow();
        }

        /// <summary>
        /// Attempts initialization immediately, ignoring the retry timer.
        /// </summary>
        /// <returns>True when networking is available after the attempt; otherwise, false.</returns>
        public bool TryInitializeNow()
        {
            ThrowIfDisposed();

            if (_client.IsInitialized)
            {
                return true;
            }

            _hasAttemptedInitialization = true;

            if (_client.TryInitialize(out var error))
            {
                OnInitialized?.Invoke();
                return true;
            }

            _nextRetryUtc = _utcNowProvider().Add(_options.RetryInterval);
            OnInitializationFailed?.Invoke(error);
            return false;
        }

        /// <summary>
        /// Disposes the wrapped client.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _client.Dispose();
        }

        private void ProcessMessages()
        {
            if (!_options.ProcessMessagesWhenInitialized)
            {
                return;
            }

            try
            {
                _client.ProcessIncomingMessages();
            }
            catch (Exception ex)
            {
                OnProcessingFailed?.Invoke(ex);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SteamNetworkClientRunner));
            }
        }
    }
}
