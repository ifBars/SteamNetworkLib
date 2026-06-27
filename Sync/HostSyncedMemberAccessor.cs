using System;
using System.Reflection;

namespace SteamNetworkLib.Sync
{
    internal sealed class HostSyncedMemberAccessor<T>
    {
        private readonly object _target;
        private readonly FieldInfo? _field;
        private readonly PropertyInfo? _property;

        public HostSyncedMemberAccessor(object target, MemberInfo member)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));

            if (member is FieldInfo field)
            {
                _field = field;
                return;
            }

            if (member is PropertyInfo property)
            {
                _property = property;
                return;
            }

            throw new ArgumentException("Host-synced member must be a field or property.", nameof(member));
        }

        public T GetValue()
        {
            object? value = _field != null
                ? _field.GetValue(_target)
                : _property!.GetValue(_target);

            return value is T typed ? typed : default!;
        }

        public void SetValue(T value)
        {
            if (_field != null)
            {
                _field.SetValue(_target, value);
                return;
            }

            _property!.SetValue(_target, value);
        }
    }
}
