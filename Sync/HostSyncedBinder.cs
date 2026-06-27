using System;
using System.Collections.Generic;
using System.Reflection;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Creates SyncVars from members marked with <see cref="HostSyncedAttribute"/> or <see cref="ClientSyncedAttribute"/>.
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
            foreach (var member in DiscoverMembers(target.GetType(), SyncedMemberOwnership.Host))
            {
                bindings.Add(CreateBinding(client, target, member, options));
            }

            return new HostSyncedBindingCollection(bindings);
        }

        /// <summary>
        /// Binds all <see cref="HostSyncedAttribute"/> and <see cref="ClientSyncedAttribute"/> members on the target object.
        /// </summary>
        /// <param name="client">The initialized Steam network client.</param>
        /// <param name="target">The object containing marked members.</param>
        /// <param name="options">Optional SyncVar options. Use <see cref="NetworkSyncOptions.KeyPrefix"/> for mod-unique keys.</param>
        /// <returns>A binding collection that can publish target member changes and be disposed during cleanup.</returns>
        public static HostSyncedBindingCollection BindSynced(
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
            foreach (var member in DiscoverMembers(target.GetType(), ownershipFilter: null))
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
            foreach (var member in DiscoverMembers(targetType, SyncedMemberOwnership.Host))
            {
                members.Add(new HostSyncedMemberInfo(member.Key, member.Member.Name, member.ValueType, member.Ownership));
            }

            return members;
        }

        /// <summary>
        /// Discovers host-synced and client-synced members without creating network bindings.
        /// </summary>
        /// <param name="targetType">The type to inspect.</param>
        /// <returns>Metadata for every marked field or property.</returns>
        public static IReadOnlyList<HostSyncedMemberInfo> DiscoverSynced(Type targetType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            var members = new List<HostSyncedMemberInfo>();
            foreach (var member in DiscoverMembers(targetType, ownershipFilter: null))
            {
                members.Add(new HostSyncedMemberInfo(member.Key, member.Member.Name, member.ValueType, member.Ownership));
            }

            return members;
        }

        private static IEnumerable<DiscoveredMember> DiscoverMembers(Type targetType, SyncedMemberOwnership? ownershipFilter)
        {
            foreach (var field in targetType.GetFields(MemberFlags))
            {
                var attribute = ResolveSyncAttribute(field, out var ownership);
                if (attribute == null || (ownershipFilter.HasValue && ownership != ownershipFilter.Value))
                {
                    continue;
                }

                if (field.IsInitOnly)
                {
                    throw new InvalidOperationException($"Synced field '{field.Name}' must be writable.");
                }

                yield return new DiscoveredMember(field, field.FieldType, ResolveKey(attribute, field.Name), ownership);
            }

            foreach (var property in targetType.GetProperties(MemberFlags))
            {
                var attribute = ResolveSyncAttribute(property, out var ownership);
                if (attribute == null || (ownershipFilter.HasValue && ownership != ownershipFilter.Value))
                {
                    continue;
                }

                if (property.GetIndexParameters().Length > 0)
                {
                    throw new InvalidOperationException($"Synced property '{property.Name}' cannot be an indexer.");
                }

                if (property.GetMethod == null)
                {
                    throw new InvalidOperationException($"Synced property '{property.Name}' must have a getter.");
                }

                if (property.SetMethod == null)
                {
                    throw new InvalidOperationException($"Synced property '{property.Name}' must have a setter.");
                }

                yield return new DiscoveredMember(property, property.PropertyType, ResolveKey(attribute, property.Name), ownership);
            }
        }

        private static Attribute? ResolveSyncAttribute(MemberInfo member, out SyncedMemberOwnership ownership)
        {
            var hostAttribute = member.GetCustomAttribute<HostSyncedAttribute>(inherit: true);
            var clientAttribute = member.GetCustomAttribute<ClientSyncedAttribute>(inherit: true);

            if (hostAttribute != null && clientAttribute != null)
            {
                throw new InvalidOperationException($"Synced member '{member.Name}' cannot be both host-synced and client-synced.");
            }

            if (hostAttribute != null)
            {
                ownership = SyncedMemberOwnership.Host;
                return hostAttribute;
            }

            if (clientAttribute != null)
            {
                ownership = SyncedMemberOwnership.Client;
                return clientAttribute;
            }

            ownership = SyncedMemberOwnership.Host;
            return null;
        }

        private static string ResolveKey(Attribute attribute, string fallback)
        {
            string? key = attribute switch
            {
                HostSyncedAttribute hostAttribute => hostAttribute.Key,
                ClientSyncedAttribute clientAttribute => clientAttribute.Key,
                _ => null
            };

            return string.IsNullOrWhiteSpace(key) ? fallback : key!;
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
            if (member.Ownership == SyncedMemberOwnership.Client)
            {
                var accessor = new HostSyncedMemberAccessor<T>(target, member.Member);
                var clientSyncVar = client.CreateClientSyncVar(member.Key, accessor.GetValue(), options);
                var clientBinding = new ClientSyncedBinding<T>(clientSyncVar, accessor);
                clientBinding.ForceSyncFromTarget();
                return clientBinding;
            }

            var hostAccessor = new HostSyncedMemberAccessor<T>(target, member.Member);
            var hostSyncVar = client.CreateHostSyncVar(member.Key, hostAccessor.GetValue(), options);
            var hostBinding = new HostSyncedBinding<T>(hostSyncVar, hostAccessor);
            if (client.IsHost)
            {
                hostBinding.ForceSyncFromTarget();
            }
            else
            {
                hostBinding.ApplyCurrentValueToTarget();
            }

            return hostBinding;
        }

        private sealed class DiscoveredMember
        {
            public DiscoveredMember(MemberInfo member, Type valueType, string key, SyncedMemberOwnership ownership)
            {
                Member = member;
                ValueType = valueType;
                Key = key;
                Ownership = ownership;
            }

            public MemberInfo Member { get; }

            public Type ValueType { get; }

            public string Key { get; }

            public SyncedMemberOwnership Ownership { get; }
        }
    }

    /// <summary>
    /// Identifies the SyncVar ownership model for an attribute-bound member.
    /// </summary>
    public enum SyncedMemberOwnership
    {
        /// <summary>
        /// The lobby host owns the value.
        /// </summary>
        Host,

        /// <summary>
        /// Each client owns its local value.
        /// </summary>
        Client
    }

    /// <summary>
    /// Metadata for a member marked with <see cref="HostSyncedAttribute"/>.
    /// </summary>
    public sealed class HostSyncedMemberInfo
    {
        internal HostSyncedMemberInfo(string key, string memberName, Type valueType, SyncedMemberOwnership ownership)
        {
            Key = key;
            MemberName = memberName;
            ValueType = valueType;
            Ownership = ownership;
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

        /// <summary>
        /// Gets whether the member is host-owned or client-owned.
        /// </summary>
        public SyncedMemberOwnership Ownership { get; }
    }
}
