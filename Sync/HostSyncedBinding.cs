namespace SteamNetworkLib.Sync
{
    internal sealed class HostSyncedBinding<T> : IHostSyncedBinding
    {
        private readonly HostSyncVar<T> _syncVar;
        private readonly HostSyncedMemberAccessor<T> _accessor;

        public HostSyncedBinding(HostSyncVar<T> syncVar, HostSyncedMemberAccessor<T> accessor)
        {
            _syncVar = syncVar;
            _accessor = accessor;
            _syncVar.OnValueChanged += HandleValueChanged;
        }

        public void SyncFromTarget()
        {
            _syncVar.Value = _accessor.GetValue();
        }

        public void RefreshFromNetwork()
        {
            _syncVar.Refresh();
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
