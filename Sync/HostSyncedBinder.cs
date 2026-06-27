using System;
using System.Collections.Generic;
using System.Reflection;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Creates host-authoritative SyncVars from members marked with <see cref="HostSyncedAttribute"/>.
    /// </summary>
    public static class HostSyncedBinder
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Binds all <see cref="HostSyncedAttribute"/> members on the target object to host SyncVars.
        /// </summary>
        /// <param name="client">The initialized Steam network client.</param>
        /// <param name="target">The object containing marked members.</param>
        /// <param name="options">Optional SyncVar options. Use <see cref="NetworkSyncOptions.KeyPrefix"/> for mod-unique keys.</param>
        /// <returns>A binding collection that can publish host-side member changes and be disposed during cleanup.</returns>
        /// <exception cref="ArgumentNullException">Thrown when client or target is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a marked member cannot be read or cannot be written from network updates.</exception>
        public static HostSyncedBindingCollection BindHostSynced(
            this SteamNetworkClient client,
            object target,
            NetworkSyncOptions? options = null)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var bindings = new List<IHostSyncedBinding>();
            foreach (var member in DiscoverMembers(target.GetType()))
            {
                bindings.Add(CreateBinding(client, target, member, options));
            }

            return new HostSyncedBindingCollection(bindings);
        }

        /// <summary>
        /// Discovers host-synced members without creating network bindings.
        /// </summary>
        /// <param name="targetType">The type to inspect.</param>
        /// <returns>Metadata for every marked field or property.</returns>
        public static IReadOnlyList<HostSyncedMemberInfo> Discover(Type targetType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            var members = new List<HostSyncedMemberInfo>();
            foreach (var member in DiscoverMembers(targetType))
            {
                members.Add(new HostSyncedMemberInfo(member.Key, member.Member.Name, member.ValueType));
            }

            return members;
        }

        private static IEnumerable<DiscoveredMember> DiscoverMembers(Type targetType)
        {
            foreach (var field in targetType.GetFields(MemberFlags))
            {
                var attribute = field.GetCustomAttribute<HostSyncedAttribute>(inherit: true);
                if (attribute == null)
                {
                    continue;
                }

                if (field.IsInitOnly)
                {
                    throw new InvalidOperationException($"Host-synced field '{field.Name}' must be writable.");
                }

                yield return new DiscoveredMember(field, field.FieldType, ResolveKey(attribute, field.Name));
            }

            foreach (var property in targetType.GetProperties(MemberFlags))
            {
                var attribute = property.GetCustomAttribute<HostSyncedAttribute>(inherit: true);
                if (attribute == null)
                {
                    continue;
                }

                if (property.GetIndexParameters().Length > 0)
                {
                    throw new InvalidOperationException($"Host-synced property '{property.Name}' cannot be an indexer.");
                }

                if (property.GetMethod == null)
                {
                    throw new InvalidOperationException($"Host-synced property '{property.Name}' must have a getter.");
                }

                if (property.SetMethod == null)
                {
                    throw new InvalidOperationException($"Host-synced property '{property.Name}' must have a setter.");
                }

                yield return new DiscoveredMember(property, property.PropertyType, ResolveKey(attribute, property.Name));
            }
        }

        private static string ResolveKey(HostSyncedAttribute attribute, string fallback)
        {
            return string.IsNullOrWhiteSpace(attribute.Key) ? fallback : attribute.Key!;
        }

        private static IHostSyncedBinding CreateBinding(
            SteamNetworkClient client,
            object target,
            DiscoveredMember member,
            NetworkSyncOptions? options)
        {
            var method = typeof(HostSyncedBinder).GetMethod(
                nameof(CreateTypedBinding),
                BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new MissingMethodException(nameof(HostSyncedBinder), nameof(CreateTypedBinding));
            }

            return (IHostSyncedBinding)method
                .MakeGenericMethod(member.ValueType)
                .Invoke(null, new object?[] { client, target, member, options })!;
        }

        private static IHostSyncedBinding CreateTypedBinding<T>(
            SteamNetworkClient client,
            object target,
            DiscoveredMember member,
            NetworkSyncOptions? options)
        {
            var accessor = new HostSyncedMemberAccessor<T>(target, member.Member);
            var syncVar = client.CreateHostSyncVar(member.Key, accessor.GetValue(), options);
            return new HostSyncedBinding<T>(syncVar, accessor);
        }

        private sealed class DiscoveredMember
        {
            public DiscoveredMember(MemberInfo member, Type valueType, string key)
            {
                Member = member;
                ValueType = valueType;
                Key = key;
            }

            public MemberInfo Member { get; }

            public Type ValueType { get; }

            public string Key { get; }
        }
    }

    /// <summary>
    /// Metadata for a member marked with <see cref="HostSyncedAttribute"/>.
    /// </summary>
    public sealed class HostSyncedMemberInfo
    {
        internal HostSyncedMemberInfo(string key, string memberName, Type valueType)
        {
            Key = key;
            MemberName = memberName;
            ValueType = valueType;
        }

        /// <summary>
        /// Gets the SyncVar key that will be used for this member before any options prefix is applied.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the reflected field or property name.
        /// </summary>
        public string MemberName { get; }

        /// <summary>
        /// Gets the synchronized value type.
        /// </summary>
        public Type ValueType { get; }
    }
}
