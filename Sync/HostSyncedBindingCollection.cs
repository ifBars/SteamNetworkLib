using System;
using System.Collections.Generic;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Tracks active bindings created from <see cref="HostSyncedAttribute"/> or <see cref="ClientSyncedAttribute"/> members.
    /// </summary>
    public sealed class HostSyncedBindingCollection : IDisposable
    {
        private readonly IReadOnlyList<IHostSyncedBinding> _bindings;
        private bool _disposed;

        internal HostSyncedBindingCollection(IReadOnlyList<IHostSyncedBinding> bindings)
        {
            _bindings = bindings;
        }

        /// <summary>
        /// Gets the number of active member bindings.
        /// </summary>
        public int Count => _bindings.Count;

        /// <summary>
        /// Publishes the current target member values through their SyncVars.
        /// </summary>
        /// <remarks>
        /// This method should be called after mutating marked fields or properties.
        /// Host-synced members use normal <see cref="HostSyncVar{T}"/> host-only behavior.
        /// Client-synced members use normal <see cref="ClientSyncVar{T}"/> local-player ownership.
        /// </remarks>
        public void SyncFromTarget()
        {
            ThrowIfDisposed();

            foreach (var binding in _bindings)
            {
                binding.SyncFromTarget();
            }
        }

        /// <summary>
        /// Forces every underlying SyncVar to refresh from Steam data.
        /// </summary>
        public void RefreshFromNetwork()
        {
            ThrowIfDisposed();

            foreach (var binding in _bindings)
            {
                binding.RefreshFromNetwork();
            }
        }

        /// <summary>
        /// Disposes all underlying SyncVar bindings.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var binding in _bindings)
            {
                binding.Dispose();
            }

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HostSyncedBindingCollection));
            }
        }
    }

    internal interface IHostSyncedBinding : IDisposable
    {
        void SyncFromTarget();

        void ForceSyncFromTarget();

        void ApplyCurrentValueToTarget();

        void RefreshFromNetwork();
    }
}
