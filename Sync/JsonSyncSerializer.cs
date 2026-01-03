using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Default JSON serializer for <see cref="HostSyncVar{T}"/> and <see cref="ClientSyncVar{T}"/>.
    /// Provides IL2CPP-compatible JSON serialization without external dependencies.
    /// </summary>
    /// <remarks>
    /// <para><strong>Supported Types:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Primitives:</strong> int, long, float, double, bool, string, byte, short, uint, ulong</description></item>
    /// <item><description><strong>Enums:</strong> Any enum type (serialized as underlying integer value)</description></item>
    /// <item><description><strong>Collections:</strong> List&lt;T&gt;, arrays (T[]), Dictionary&lt;string, T&gt;</description></item>
    /// <item><description><strong>Custom types:</strong> Classes and structs with parameterless constructor and public properties</description></item>
    /// </list>
    /// 
    /// <para><strong>Requirements for Custom Types:</strong></para>
    /// <list type="number">
    /// <item><description>Must have a public parameterless constructor</description></item>
    /// <item><description>Properties must be public with both getter and setter</description></item>
    /// <item><description>Property types must themselves be serializable</description></item>
    /// <item><description>Circular references are not supported</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // Valid custom type
    /// public class GameSettings
    /// {
    ///     public int MaxPlayers { get; set; } = 4;
    ///     public string MapName { get; set; } = "default";
    ///     public bool FriendlyFire { get; set; } = false;
    ///     public List&lt;string&gt; EnabledMods { get; set; } = new();
    /// }
    /// 
    /// // Usage
    /// var settings = client.CreateHostSyncVar("Settings", new GameSettings());
    /// </code>
    /// </example>
    /// </remarks>
    public class JsonSyncSerializer : ISyncSerializer
    {
        /// <summary>
        /// Serializes a value to a JSON string.
        /// </summary>
        /// <typeparam name="T">The type of value to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <returns>A JSON string representation of the value.</returns>
        /// <exception cref="SyncSerializationException">Thrown when serialization fails.</exception>
        public string Serialize<T>(T value)
        {
            try
            {
                return SerializeValue(value, typeof(T));
            }
            catch (SyncSerializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SyncSerializationException(
                    $"Failed to serialize type '{typeof(T).Name}': {ex.Message}", 
                    typeof(T), 
                    string.Empty,
                    ex);
            }
        }

        /// <summary>
        /// Deserializes a JSON string to the specified type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="data">The JSON string to deserialize.</param>
        /// <returns>The deserialized value.</returns>
        /// <exception cref="SyncSerializationException">Thrown when deserialization fails.</exception>
        public T Deserialize<T>(string data)
        {
            try
            {
                var result = DeserializeValue(data, typeof(T));
                return (T)result!;
            }
            catch (SyncSerializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SyncSerializationException(
                    $"Failed to deserialize to type '{typeof(T).Name}': {ex.Message}", 
                    typeof(T), 
                    string.Empty,
                    ex);
            }
        }

        /// <summary>
        /// Checks if a type can be serialized by this serializer.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type can be serialized; otherwise, false.</returns>
        public bool CanSerialize(Type type)
        {
            return CanSerializeType(type, new HashSet<Type>());
        }

        private bool CanSerializeType(Type type, HashSet<Type> visited)
        {
            // Prevent infinite recursion on circular types
            if (visited.Contains(type))
                return true; // Assume it's okay if we've seen it before
            visited.Add(type);

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                return CanSerializeType(underlyingType, visited);
            }

            // Primitives and basic types
            if (type == typeof(string) ||
                type == typeof(int) || type == typeof(long) ||
                type == typeof(float) || type == typeof(double) ||
                type == typeof(bool) || type == typeof(byte) ||
                type == typeof(short) || type == typeof(uint) ||
                type == typeof(ulong) || type == typeof(decimal))
            {
                return true;
            }

            // Enums
            if (type.IsEnum)
            {
                return true;
            }

            // Arrays
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return elementType != null && CanSerializeType(elementType, visited);
            }

            // Generic collections
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();

                // List<T>
                if (genericDef == typeof(List<>))
                {
                    return CanSerializeType(genericArgs[0], visited);
                }

                // Dictionary<string, T>
                if (genericDef == typeof(Dictionary<,>))
                {
                    return genericArgs[0] == typeof(string) && 
                           CanSerializeType(genericArgs[1], visited);
                }
            }

            // Custom types - check for parameterless constructor and public properties
            if (type.IsClass || type.IsValueType)
            {
                // Must have parameterless constructor (structs always do)
                if (type.IsClass)
                {
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor == null)
                    {
                        return false;
                    }
                }

                // Check all public properties are serializable
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties)
                {
                    if (prop.CanRead && prop.CanWrite)
                    {
                        if (!CanSerializeType(prop.PropertyType, visited))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            return false;
        }

        private string SerializeValue(object? value, Type type)
        {
            if (value == null)
            {
                return "null";
            }

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
            }

            // Primitives
            if (type == typeof(string))
            {
                return EscapeString((string)value);
            }
            if (type == typeof(bool))
            {
                return (bool)value ? "true" : "false";
            }
            if (type == typeof(int) || type == typeof(long) || 
                type == typeof(byte) || type == typeof(short) ||
                type == typeof(uint) || type == typeof(ulong))
            {
                return value.ToString()!;
            }
            if (type == typeof(float))
            {
                return ((float)value).ToString(CultureInfo.InvariantCulture);
            }
            if (type == typeof(double))
            {
                return ((double)value).ToString(CultureInfo.InvariantCulture);
            }
            if (type == typeof(decimal))
            {
                return ((decimal)value).ToString(CultureInfo.InvariantCulture);
            }

            // Enums - serialize as integer
            if (type.IsEnum)
            {
                return Convert.ToInt64(value).ToString();
            }

            // Arrays
            if (type.IsArray)
            {
                var array = (Array)value;
                var elementType = type.GetElementType()!;
                var sb = new StringBuilder("[");
                for (int i = 0; i < array.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(SerializeValue(array.GetValue(i), elementType));
                }
                sb.Append("]");
                return sb.ToString();
            }

            // Generic collections
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();

                // List<T>
                if (genericDef == typeof(List<>))
                {
                    var list = (IList)value;
                    var elementType = genericArgs[0];
                    var sb = new StringBuilder("[");
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        sb.Append(SerializeValue(list[i], elementType));
                    }
                    sb.Append("]");
                    return sb.ToString();
                }

                // Dictionary<string, T>
                if (genericDef == typeof(Dictionary<,>) && genericArgs[0] == typeof(string))
                {
                    var dict = (IDictionary)value;
                    var valueType = genericArgs[1];
                    var sb = new StringBuilder("{");
                    bool first = true;
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (!first) sb.Append(",");
                        first = false;
                        sb.Append(EscapeString((string)entry.Key));
                        sb.Append(":");
                        sb.Append(SerializeValue(entry.Value, valueType));
                    }
                    sb.Append("}");
                    return sb.ToString();
                }
            }

            // Custom objects
            if (type.IsClass || type.IsValueType)
            {
                var sb = new StringBuilder("{");
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                bool first = true;
                foreach (var prop in properties)
                {
                    if (prop.CanRead && prop.CanWrite)
                    {
                        if (!first) sb.Append(",");
                        first = false;
                        sb.Append(EscapeString(prop.Name));
                        sb.Append(":");
                        sb.Append(SerializeValue(prop.GetValue(value), prop.PropertyType));
                    }
                }
                sb.Append("}");
                return sb.ToString();
            }

            throw new SyncSerializationException($"Cannot serialize type: {type.Name}", type);
        }

        private object? DeserializeValue(string json, Type type)
        {
            json = json.Trim();

            if (json == "null")
            {
                return null;
            }

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
            }

            // Primitives
            if (type == typeof(string))
            {
                return UnescapeString(json);
            }
            if (type == typeof(bool))
            {
                return json == "true";
            }
            if (type == typeof(int))
            {
                return int.Parse(json, CultureInfo.InvariantCulture);
            }
            if (type == typeof(long))
            {
                return long.Parse(json, CultureInfo.InvariantCulture);
            }
            if (type == typeof(float))
            {
                return float.Parse(json, CultureInfo.InvariantCulture);
            }
            if (type == typeof(double))
            {
                return double.Parse(json, CultureInfo.InvariantCulture);
            }
            if (type == typeof(byte))
            {
                return byte.Parse(json, CultureInfo.InvariantCulture);
            }
            if (type == typeof(short))
            {
                return short.Parse(json, CultureInfo.InvariantCulture);
            }
            if (type == typeof(uint))
            {
                return uint.Parse(json, CultureInfo.InvariantCulture);
            }
            if (type == typeof(ulong))
            {
                return ulong.Parse(json, CultureInfo.InvariantCulture);
            }
            if (type == typeof(decimal))
            {
                return decimal.Parse(json, CultureInfo.InvariantCulture);
            }

            // Enums
            if (type.IsEnum)
            {
                var intValue = long.Parse(json, CultureInfo.InvariantCulture);
                return Enum.ToObject(type, intValue);
            }

            // Arrays
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var elements = ParseJsonArray(json);
                var array = Array.CreateInstance(elementType, elements.Count);
                for (int i = 0; i < elements.Count; i++)
                {
                    array.SetValue(DeserializeValue(elements[i], elementType), i);
                }
                return array;
            }

            // Generic collections
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();

                // List<T>
                if (genericDef == typeof(List<>))
                {
                    var elementType = genericArgs[0];
                    var list = (IList)Activator.CreateInstance(type)!;
                    var elements = ParseJsonArray(json);
                    foreach (var element in elements)
                    {
                        list.Add(DeserializeValue(element, elementType));
                    }
                    return list;
                }

                // Dictionary<string, T>
                if (genericDef == typeof(Dictionary<,>) && genericArgs[0] == typeof(string))
                {
                    var valueType = genericArgs[1];
                    var dict = (IDictionary)Activator.CreateInstance(type)!;
                    var pairs = ParseJsonObject(json);
                    foreach (var pair in pairs)
                    {
                        dict[pair.Key] = DeserializeValue(pair.Value, valueType);
                    }
                    return dict;
                }
            }

            // Custom objects
            if (type.IsClass || type.IsValueType)
            {
                var obj = Activator.CreateInstance(type)!;
                var pairs = ParseJsonObject(json);
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                
                foreach (var prop in properties)
                {
                    if (prop.CanRead && prop.CanWrite && pairs.ContainsKey(prop.Name))
                    {
                        var value = DeserializeValue(pairs[prop.Name], prop.PropertyType);
                        prop.SetValue(obj, value);
                    }
                }
                return obj;
            }

            throw new SyncSerializationException($"Cannot deserialize type: {type.Name}", type);
        }

        private string EscapeString(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        private string UnescapeString(string json)
        {
            if (json.Length < 2 || json[0] != '"' || json[json.Length - 1] != '"')
            {
                throw new SyncSerializationException("Invalid JSON string format");
            }

            var sb = new StringBuilder();
            bool escape = false;
            for (int i = 1; i < json.Length - 1; i++)
            {
                char c = json[i];
                if (escape)
                {
                    switch (c)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(c); break;
                    }
                    escape = false;
                }
                else if (c == '\\')
                {
                    escape = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private List<string> ParseJsonArray(string json)
        {
            var result = new List<string>();
            if (json.Length < 2 || json[0] != '[' || json[json.Length - 1] != ']')
            {
                throw new SyncSerializationException("Invalid JSON array format");
            }

            int depth = 0;
            bool inString = false;
            bool escape = false;
            var current = new StringBuilder();

            for (int i = 1; i < json.Length - 1; i++)
            {
                char c = json[i];

                if (escape)
                {
                    current.Append(c);
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    current.Append(c);
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    current.Append(c);
                    continue;
                }

                if (!inString)
                {
                    if (c == '[' || c == '{')
                    {
                        depth++;
                    }
                    else if (c == ']' || c == '}')
                    {
                        depth--;
                    }
                    else if (c == ',' && depth == 0)
                    {
                        var element = current.ToString().Trim();
                        if (element.Length > 0)
                        {
                            result.Add(element);
                        }
                        current.Clear();
                        continue;
                    }
                }

                current.Append(c);
            }

            var lastElement = current.ToString().Trim();
            if (lastElement.Length > 0)
            {
                result.Add(lastElement);
            }

            return result;
        }

        private Dictionary<string, string> ParseJsonObject(string json)
        {
            var result = new Dictionary<string, string>();
            if (json.Length < 2 || json[0] != '{' || json[json.Length - 1] != '}')
            {
                throw new SyncSerializationException("Invalid JSON object format");
            }

            int depth = 0;
            bool inString = false;
            bool escape = false;
            bool inKey = true;
            var currentKey = new StringBuilder();
            var currentValue = new StringBuilder();

            for (int i = 1; i < json.Length - 1; i++)
            {
                char c = json[i];

                if (escape)
                {
                    if (inKey) currentKey.Append(c);
                    else currentValue.Append(c);
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    if (inKey) currentKey.Append(c);
                    else currentValue.Append(c);
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    if (inKey) currentKey.Append(c);
                    else currentValue.Append(c);
                    continue;
                }

                if (!inString)
                {
                    if (c == '[' || c == '{')
                    {
                        depth++;
                    }
                    else if (c == ']' || c == '}')
                    {
                        depth--;
                    }
                    else if (c == ':' && depth == 0 && inKey)
                    {
                        inKey = false;
                        continue;
                    }
                    else if (c == ',' && depth == 0)
                    {
                        var key = UnescapeString(currentKey.ToString().Trim());
                        var value = currentValue.ToString().Trim();
                        if (key.Length > 0)
                        {
                            result[key] = value;
                        }
                        currentKey.Clear();
                        currentValue.Clear();
                        inKey = true;
                        continue;
                    }
                }

                if (inKey) currentKey.Append(c);
                else currentValue.Append(c);
            }

            // Handle last pair
            var lastKey = currentKey.ToString().Trim();
            var lastValue = currentValue.ToString().Trim();
            if (lastKey.Length > 0)
            {
                result[UnescapeString(lastKey)] = lastValue;
            }

            return result;
        }
    }
}
