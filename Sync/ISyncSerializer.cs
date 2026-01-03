using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Interface for custom value serialization in <see cref="HostSyncVar{T}"/> and <see cref="ClientSyncVar{T}"/>.
    /// </summary>
    /// <remarks>
    /// <para>Implement this interface to provide custom serialization logic for complex types
    /// or when the default <see cref="JsonSyncSerializer"/> doesn't meet your needs.</para>
    /// <example>
    /// <code>
    /// public class MyCustomSerializer : ISyncSerializer
    /// {
    ///     public string Serialize&lt;T&gt;(T value)
    ///     {
    ///         // Custom serialization logic
    ///         return MySerializationLibrary.ToJson(value);
    ///     }
    ///     
    ///     public T Deserialize&lt;T&gt;(string data)
    ///     {
    ///         // Custom deserialization logic
    ///         return MySerializationLibrary.FromJson&lt;T&gt;(data);
    ///     }
    ///     
    ///     public bool CanSerialize(Type type)
    ///     {
    ///         return MySerializationLibrary.IsSupported(type);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public interface ISyncSerializer
    {
        /// <summary>
        /// Serializes a value to a string for network transmission.
        /// </summary>
        /// <typeparam name="T">The type of value to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <returns>A string representation of the value.</returns>
        /// <exception cref="SyncSerializationException">Thrown when serialization fails.</exception>
        string Serialize<T>(T value);

        /// <summary>
        /// Deserializes a string back to the original value type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="data">The serialized string data.</param>
        /// <returns>The deserialized value.</returns>
        /// <exception cref="SyncSerializationException">Thrown when deserialization fails.</exception>
        T Deserialize<T>(string data);

        /// <summary>
        /// Checks if a type can be serialized by this serializer.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type can be serialized; otherwise, false.</returns>
        /// <remarks>
        /// This method is called during <see cref="HostSyncVar{T}"/> and <see cref="ClientSyncVar{T}"/>
        /// creation to validate that the type parameter is serializable.
        /// </remarks>
        bool CanSerialize(Type type);
    }
}
