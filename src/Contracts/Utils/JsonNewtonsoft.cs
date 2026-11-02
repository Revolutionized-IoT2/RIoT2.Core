using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

// Newtonsoft.Json helpers that remain while stored configuration is read with type-name handling
// (plan M2: typed persistence replaces them in I26, and Core drops Newtonsoft in I45). The wire
// format is System.Text.Json (Json.cs, JsonConverters.cs).
namespace RIoT2.Core.Utils
{
    /// <summary>
    /// Restricts Json.NET type-name handling to RIoT2-owned contract types and simple collection
    /// shapes so stored polymorphic configuration files remain loadable without allowing arbitrary
    /// framework types to be constructed from untrusted JSON.
    /// </summary>
    public sealed class RIoT2SerializationBinder : ISerializationBinder
    {
        private static readonly ISerializationBinder _defaultBinder = new DefaultSerializationBinder();

        public Type BindToType(string? assemblyName, string typeName)
        {
            var type = _defaultBinder.BindToType(assemblyName, typeName);
            if (!IsAllowedType(type))
                throw new JsonSerializationException($"Type '{type.FullName}' from assembly '{type.Assembly.GetName().Name}' is not allowed in RIoT2 JSON type metadata.");
            return type;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            if (!IsAllowedType(serializedType))
                throw new JsonSerializationException($"Type '{serializedType.FullName}' from assembly '{serializedType.Assembly.GetName().Name}' is not allowed in RIoT2 JSON type metadata.");
            _defaultBinder.BindToName(serializedType, out assemblyName, out typeName);
        }

        private static bool IsAllowedType(Type? type)
        {
            if (type == null)
                return false;

            if (type.IsArray)
                return IsAllowedType(type.GetElementType());

            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return true;

            if (IsRIoT2Assembly(type.Assembly.GetName().Name ?? string.Empty))
                return true;

            if (!type.IsGenericType)
                return false;

            var definition = type.GetGenericTypeDefinition();
            if (definition != typeof(List<>) &&
                definition != typeof(Dictionary<,>) &&
                definition != typeof(HashSet<>))
                return false;

            return type.GetGenericArguments().All(IsAllowedType);
        }

        private static bool IsRIoT2Assembly(string assemblyName)
        {
            return string.Equals(assemblyName, "RIoT2.Core", StringComparison.Ordinal) ||
                   assemblyName.StartsWith("RIoT2.", StringComparison.Ordinal);
        }
    }

    /// <summary>The Newtonsoft.Json converter for <see cref="ValueModel"/>. The wire format uses <see cref="ValueModelJsonConverter"/>.</summary>
    [Obsolete("Newtonsoft.Json only. Json.Serialize and Json.Deserialize use System.Text.Json and ValueModelJsonConverter.")]
    public class ValueModelConverter : JsonConverter<ValueModel>
    {
        public override void WriteJson(JsonWriter writer, ValueModel? value, Newtonsoft.Json.JsonSerializer serializer)
        {
            writer.WriteRawValue(value?.ToJson());
        }

        public override ValueModel? ReadJson(JsonReader reader, Type objectType, ValueModel? existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Null)
            {
                JValue jValue = new JValue(reader.Value);
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        return new ValueModel(JObject.Load(reader).ToString());
                    case JsonToken.StartArray:
                        // Arrays have no scalar reader.Value; load the full array token
                        // and construct the ValueModel from its JSON representation.
                        return new ValueModel(JArray.Load(reader).ToString());
                    case JsonToken.Boolean:
                        return new ValueModel((bool)jValue);
                    case JsonToken.Integer:
                        return new ValueModel(jValue.ToString(Formatting.None));
                    case JsonToken.Float:
                        return new ValueModel((double)jValue);
                    case JsonToken.String:
                    default:
                        // reader.Value can be null for token types such as Null/StartArray/EndArray;
                        // guard against a NullReferenceException.
                        return reader.Value == null ? null : new ValueModel((object)reader.Value.ToString());
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Wraps a mutable JSON object and provides convenience operations for reading, merging, and
    /// modifying its properties.
    /// </summary>
    public class JsonEntity
    {
        private JObject _jobj;

        internal JObject JsonObject { get { return _jobj; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonEntity"/> class from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to parse.</param>
        public JsonEntity(string json)
        {
            _jobj = JObject.Parse(json);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonEntity"/> class from an existing <see cref="JObject"/>.
        /// </summary>
        /// <param name="jobject">The JSON object to wrap.</param>
        public JsonEntity(JObject jobject)
        {
            _jobj = jobject;
        }

        /// <summary>
        /// Merges the properties of another <see cref="JsonEntity"/> into this one, unioning arrays
        /// and matching property names case-insensitively.
        /// </summary>
        /// <param name="from">The entity whose properties are merged into this instance.</param>
        public void Merge(JsonEntity from)
        {
            _jobj.Merge(from.JsonObject, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Union,
                PropertyNameComparison = System.StringComparison.InvariantCultureIgnoreCase
            });
        }

        /// <summary>
        /// Returns the JSON representation of the entity.
        /// </summary>
        /// <returns>The JSON string.</returns>
        public override string ToString()
        {
            return _jobj.ToString();
        }

        /// <summary>
        /// Deserializes the entity into an object of the specified type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The deserialized object.</returns>
        public T? ToObject<T>()
        {
            return _jobj.ToObject<T>();
        }

        /// <summary>
        /// Removes the specified top-level properties from the entity.
        /// </summary>
        /// <param name="properties">The names of the properties to remove.</param>
        public void RemoveProperties(List<string> properties)
        {
            foreach (var k in properties)
                _jobj.Remove(k);
        }

        /// <summary>
        /// Determines whether the entity contains a top-level property with the specified name, ignoring case.
        /// </summary>
        /// <param name="property">The property name to look for.</param>
        /// <returns><c>true</c> if the property exists; otherwise, <c>false</c>.</returns>
        public bool ContainsPropertyIgnoreCase(string property)
        {
            foreach (var e in _jobj)
            {
                if (string.Equals(e.Key, property, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private string? getPropertyIgnoreCase(string property)
        {
            foreach (var e in _jobj)
            {
                if (string.Equals(e.Key, property, StringComparison.OrdinalIgnoreCase))
                    return e.Key;
            }
            return null;
        }

        /// <summary>
        /// Gets the names of all top-level properties in the entity.
        /// </summary>
        /// <returns>The property names.</returns>
        public IEnumerable<string> GetProperties()
        {
            foreach (var e in _jobj)
                yield return e.Key;
        }

        /// <summary>
        /// Finds a value using a JSONPath expression and converts it to the specified type.
        /// </summary>
        /// <typeparam name="T">The type to convert the value to.</typeparam>
        /// <param name="path">The JSONPath expression identifying the value.</param>
        /// <returns>The converted value, or the default of <typeparamref name="T"/> if not found.</returns>
        public T? FindValue<T>(string path)
        {
            var token = _jobj.SelectToken(path);
            if (token != null)
            {
                if (token.Type == JTokenType.Object)
                {
                    return ((JObject)token).ToObject<T>();
                }
                return token.Value<T>();
            }
            return default(T);
        }

        /// <summary>
        /// Gets the JSON representation of a top-level property.
        /// </summary>
        /// <param name="property">The property name (matched case-insensitively).</param>
        /// <returns>The property's JSON, or <c>null</c> if it is missing.</returns>
        public string? GetValueAsJson(string property)
        {
            try
            {
                var token = _jobj.GetValue(property, StringComparison.OrdinalIgnoreCase);
                if (token == null)
                    return null;

                return token.ToJson();
            }
            catch
            {
                //not valid json -> return null;
            }
            return null;
        }

        /// <summary>
        /// Gets the value of a top-level property, converted to its natural CLR type.
        /// </summary>
        /// <param name="property">The property name (matched case-insensitively).</param>
        /// <returns>The property value as a <see cref="bool"/>, <see cref="decimal"/>, <see cref="DateTime"/>, <see cref="int"/>, or string; or <c>null</c> if missing.</returns>
        public object? GetValue(string property)
        {
            try
            {
                var token = _jobj.GetValue(property, StringComparison.OrdinalIgnoreCase);
                if (token == null)
                    return null;

                switch (token.Type)
                {
                    case JTokenType.Boolean: return token.Value<bool>();
                    case JTokenType.Float: return token.Value<decimal>();
                    case JTokenType.Date: return token.Value<DateTime>();
                    case JTokenType.Integer: return token.Value<int>();
                    default: return token.ToString();
                }
            }
            catch
            {
                //not valid json -> return null;
            }
            return null;
        }

        /// <summary>
        /// Sets a value at the location identified by a JSONPath expression, creating intermediate
        /// nodes when the path does not yet exist.
        /// </summary>
        /// <param name="path">The JSONPath expression identifying the target.</param>
        /// <param name="value">The value to set.</param>
        public void SetValue(string path, object value)
        {
            try
            {
                var j = _jobj.SelectToken(path);
                if (j != null)
                {
                    j.Replace(JToken.FromObject(value));
                }
                else
                {
                    updateOrAddInPath(path, value);
                }
            }
            catch
            {
                //not valid json -> return null;
            }
        }

        /// <summary>
        /// Removes the specified top-level property, if present.
        /// </summary>
        /// <param name="property">The name of the property to remove.</param>
        public void Remove(string property)
        {
            if (_jobj.ContainsKey(property))
            {
                _jobj.Remove(property);
            }
        }

        /// <summary>
        /// Recursively replaces every value that matches the specified placeholder with the given value.
        /// </summary>
        /// <param name="placeholder">The placeholder value to match (case-insensitive).</param>
        /// <param name="value">The replacement value.</param>
        public void ReplacePlaceHolderWithValue(string placeholder, object? value)
        {
            traverseJObject(placeholder, value, _jobj);
        }

        private void updateOrAddInPath(string path, object valueToAdd)
        {
            var pathParts = path.Split('.');
            JToken? node = _jobj;
            for (int i = 0; i < pathParts.Length; i++)
            {
                var pathPart = pathParts[i];
                var partNode = node?.SelectToken(pathPart);

                // Only JObject nodes can have properties added to them.
                if (!(node is JObject objectNode))
                    return;

                if (partNode == null && i < pathParts.Length - 1)
                {
                    objectNode.Add(pathPart, new JObject());
                    partNode = node.SelectToken(pathPart);
                }
                else if (partNode == null && i == pathParts.Length - 1)
                {
                    objectNode.Add(pathPart, JToken.FromObject(valueToAdd));
                    partNode = node.SelectToken(pathPart);
                }
                node = partNode;
            }
        }
        private void traverseJObject(string placeholder, object? value, JObject parent)
        {
            foreach (var e in parent)
            {
                if (e.Value is JArray arr)
                {
                    traverseJArray(placeholder, value, arr);
                }
                else if (e.Value is JObject childObj)
                {
                    traverseJObject(placeholder, value, childObj);
                }
            }

            //Apply top-level replacements after enumeration to avoid modifying the collection while iterating
            var keysToReplace = new List<string>();
            foreach (var e in parent)
            {
                if (string.Equals(e.Value?.ToString(), placeholder, StringComparison.OrdinalIgnoreCase))
                    keysToReplace.Add(e.Key);
            }
            foreach (var key in keysToReplace)
                parent[key] = new JValue(value);
        }

        private void traverseJArray(string placeholder, object? value, JArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var element = array[i];
                if (element is JObject childObj)
                {
                    traverseJObject(placeholder, value, childObj);
                }
                else if (element is JArray childArr)
                {
                    traverseJArray(placeholder, value, childArr);
                }
                else if (string.Equals(element.ToString(), placeholder, StringComparison.OrdinalIgnoreCase))
                {
                    array[i] = new JValue(value);
                }
            }
        }
    }

    public class PascalCaseNamingStrategy : NamingStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PascalCaseNamingStrategy"/> class.
        /// </summary>
        /// <param name="processDictionaryKeys">
        /// A flag indicating whether dictionary keys should be processed.
        /// </param>
        /// <param name="overrideSpecifiedNames">
        /// A flag indicating whether explicitly specified property names should be processed,
        /// e.g. a property name customized with a <see cref="JsonPropertyAttribute"/>.
        /// </param>
        public PascalCaseNamingStrategy(bool processDictionaryKeys, bool overrideSpecifiedNames)
        {
            ProcessDictionaryKeys = processDictionaryKeys;
            OverrideSpecifiedNames = overrideSpecifiedNames;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PascalCaseNamingStrategy"/> class.
        /// </summary>
        /// <param name="processDictionaryKeys">
        /// A flag indicating whether dictionary keys should be processed.
        /// </param>
        /// <param name="overrideSpecifiedNames">
        /// A flag indicating whether explicitly specified property names should be processed,
        /// e.g. a property name customized with a <see cref="JsonPropertyAttribute"/>.
        /// </param>
        /// <param name="processExtensionDataNames">
        /// A flag indicating whether extension data names should be processed.
        /// </param>
        public PascalCaseNamingStrategy(bool processDictionaryKeys, bool overrideSpecifiedNames, bool processExtensionDataNames)
            : this(processDictionaryKeys, overrideSpecifiedNames)
        {
            ProcessExtensionDataNames = processExtensionDataNames;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PascalCaseNamingStrategy"/> class.
        /// </summary>
        public PascalCaseNamingStrategy()
        {
        }

        /// <summary>
        /// Resolves the specified property name.
        /// </summary>
        /// <param name="name">The property name to resolve.</param>
        /// <returns>The resolved property name.</returns>
        protected override string ResolvePropertyName(string name)
        {
            return ToPascalCase(name);
        }

        private static string ToPascalCase(string s)
        {
            if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0]))
            {
                return s;
            }

            char[] chars = s.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 0 && !char.IsUpper(chars[i]))
                {
                    chars[i] = ToUpper(chars[i]);
                    break;
                }
                else if (i == 0)
                {
                    break;
                }

                if (i == 1 && !char.IsUpper(chars[i]))
                {
                    break;
                }

                bool hasNext = (i + 1 < chars.Length);
                if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
                {
                    if (char.IsSeparator(chars[i + 1]))
                    {
                        chars[i] = ToLower(chars[i]);
                    }

                    break;
                }

                chars[i] = ToLower(chars[i]);
            }

            return new string(chars);
        }

        private static char ToLower(char c)
        {
            #if HAVE_CHAR_TO_LOWER_WITH_CULTURE
            c = char.ToLower(c, CultureInfo.InvariantCulture);
            #else
            c = char.ToLowerInvariant(c);
            #endif
            return c;
        }

        private static char ToUpper(char c)
        {
            #if HAVE_CHAR_TO_UPPER_WITH_CULTURE
            c = char.ToUpper(c, CultureInfo.InvariantCulture);
            #else
            c = char.ToUpperInvariant(c);
            #endif
            return c;
        }
    }
}
