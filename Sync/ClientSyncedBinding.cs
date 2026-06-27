namespace SteamNetworkLib.Sync
{
    internal sealed class ClientSyncedBinding<T> : INetworkSyncedBinding
    {
        private readonly ClientSyncVar<T> _syncVar;
        private readonly NetworkSyncedMemberAccessor<T> _accessor;

        public ClientSyncedBinding(ClientSyncVar<T> syncVar, NetworkSyncedMemberAccessor<T> accessor)
        {
            _syncVar = syncVar;
            _accessor = accessor;
            _syncVar.OnMyValueChanged += HandleMyValueChanged;
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
            _syncVar.OnMyValueChanged -= HandleMyValueChanged;
            _syncVar.Dispose();
        }

        private void HandleMyValueChanged(T oldValue, T newValue)
        {
            _accessor.SetValue(newValue);
        }
    }
}
