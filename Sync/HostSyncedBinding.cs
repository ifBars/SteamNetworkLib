namespace SteamNetworkLib.Sync
{
    internal sealed class HostSyncedBinding<T> : INetworkSyncedBinding
    {
        private readonly HostSyncVar<T> _syncVar;
        private readonly NetworkSyncedMemberAccessor<T> _accessor;

        public HostSyncedBinding(HostSyncVar<T> syncVar, NetworkSyncedMemberAccessor<T> accessor)
        {
            _syncVar = syncVar;
            _accessor = accessor;
            _syncVar.OnValueChanged += HandleValueChanged;
        }

        public void SyncFromTarget()
        {
            _syncVar.Value = _accessor.GetValue();
        }

        public void ForceSyncFromTarget()
        {
            _syncVar.ForceSync(_accessor.GetValue());
        }

        public void ApplyCurrentValueToTarget()
        {
            _accessor.SetValue(_syncVar.Value);
        }

        public void RefreshFromNetwork()
        {
            _syncVar.Refresh();
            ApplyCurrentValueToTarget();
        }

        public void Dispose()
        {
            _syncVar.OnValueChanged -= HandleValueChanged;
            _syncVar.Dispose();
        }

        private void HandleValueChanged(T oldValue, T newValue)
        {
            _accessor.SetValue(newValue);
        }
    }
}
