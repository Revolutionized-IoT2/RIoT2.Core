using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// JSON helpers used across RIoT2. Since Core 1.11.0 they run on System.Text.Json with
    /// <see cref="RIoT2JsonOptions"/> and write what Newtonsoft.Json wrote (plan M2 step 3, I25). The
    /// members marked <see cref="ObsoleteAttribute"/> still use Newtonsoft.Json.
    /// </summary>
    public static class Json
    {
        private static readonly ISerializationBinder _safeTypeNameBinder = new RIoT2SerializationBinder();

        /// <summary>
        /// Converts a JSON object into a nested dictionary: objects become dictionaries, arrays
        /// <c>object[]</c>, integers <see cref="long"/>, other numbers <see cref="double"/>.
        /// </summary>
        /// <param name="json">A JSON object.</param>
        /// <returns>The dictionary.</returns>
        /// <exception cref="System.Text.Json.JsonException">The text is not a JSON object.</exception>
        public static IDictionary<string, object> ToDictionary(string json)
        {
            using (var document = JsonDocument.Parse(json, DocumentOptions))
                return ObjectToDictionary(document.RootElement);
        }

        /// <summary>
        /// Converts an object into a nested dictionary via its JSON, with the member names as declared.
        /// </summary>
        /// <param name="obj">The object to convert.</param>
        /// <returns>The dictionary.</returns>
        public static IDictionary<string, object> ToDictionary(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
#pragma warning disable CS0618 // A JObject keeps its own conversion until JObjects are gone (I26).
            if (obj is JObject jObject)
                return ToDictionary(jObject);
#pragma warning restore CS0618
            if (obj is JsonElement element)
                return ObjectToDictionary(element);

            using (var document = JsonSerializer.SerializeToDocument(obj, obj.GetType(), RIoT2JsonOptions.AsDeclared))
                return ObjectToDictionary(document.RootElement);
        }

        /// <summary>
        /// Converts a <see cref="JObject"/> into a nested dictionary.
        /// </summary>
        /// <param name="jObject">The JSON object to convert.</param>
        /// <returns>A dictionary representing the JSON object, or <c>null</c> if <paramref name="jObject"/> is <c>null</c>.</returns>
        [Obsolete("Newtonsoft.Json only. Use ToDictionary(string) or ToDictionary(object).")]
        public static IDictionary<string, object> ToDictionary(JObject jObject)
        {
            if (jObject == null)
                return null;

            var result = jObject.ToObject<Dictionary<string, object>>();

            var JObjectKeys = (from r in result
                               let key = r.Key
                               let value = r.Value
                               where value != null && value.GetType() == typeof(JObject)
                               select key).ToList();

            var JArrayKeys = (from r in result
                              let key = r.Key
                              let value = r.Value
                              where value != null && value.GetType() == typeof(JArray)
                              select key).ToList();

            JArrayKeys.ForEach(key => result[key] = ((JArray)result[key]).Select(ConvertToken).ToArray());
            JObjectKeys.ForEach(key => result[key] = ToDictionary((result[key] as JObject).ToString()));

            return result;
        }

#pragma warning disable CS0618
        private static object ConvertToken(JToken token)
        {
            switch (token)
            {
                case JObject obj:
                    return ToDictionary(obj);
                case JArray arr:
                    return arr.Select(ConvertToken).ToArray();
                case JValue val:
                    return val.Value;
                default:
                    return token?.ToString();
            }
        }
#pragma warning restore CS0618

        /// <summary>
        /// Deserializes a JSON string into an object of the specified type.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <param name="toType">The target type.</param>
        /// <returns>The deserialized object, or <c>null</c> if <paramref name="json"/> is null or white space.</returns>
        public static object Deserialize(string json, System.Type toType)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize(json, toType, RIoT2JsonOptions.Default);
        }

        /// <summary>
        /// Deserializes a JSON string with Newtonsoft's automatic type-name handling, allowing
        /// polymorphic types to be resolved.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized object, or the default of <typeparamref name="T"/> if <paramref name="json"/> is null or empty.</returns>
        [Obsolete("Newtonsoft.Json only, for stored configuration with $type metadata (removed with typed persistence, I26). Use Deserialize<T>.")]
        public static T DeserializeAutoTypeNameHandling<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default(T);

            return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = _safeTypeNameBinder
            });
        }

        /// <summary>
        /// Serializes an object with Newtonsoft to camelCase JSON, optionally emitting type information and nulls.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="autoTypeNameHandling">If <c>true</c>, embeds type information to support polymorphic deserialization.</param>
        /// <param name="includeNulls">If <c>true</c>, includes properties with null values.</param>
        /// <returns>The JSON representation of the object.</returns>
        [Obsolete("Newtonsoft.Json only, for stored configuration (removed with typed persistence, I26). Use Serialize or SerializeIgnoreNulls.")]
        public static string SerializeAutoTypeNameHandling(object obj, bool autoTypeNameHandling = false, bool includeNulls = false)
        {
            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings()
            {
                TypeNameHandling = autoTypeNameHandling ? TypeNameHandling.Auto : TypeNameHandling.None,
                SerializationBinder = _safeTypeNameBinder,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = includeNulls ? NullValueHandling.Include : NullValueHandling.Ignore,
            });
        }

        /// <summary>
        /// Deserializes a JSON string into an object of the specified type with
        /// <see cref="RIoT2JsonOptions"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized object, or the default of <typeparamref name="T"/> if <paramref name="json"/> is null or white space.</returns>
        /// <exception cref="System.Text.Json.JsonException">The JSON is invalid or doesn't fit <typeparamref name="T"/>.</exception>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default(T);

            return JsonSerializer.Deserialize<T>(json, RIoT2JsonOptions.Default);
        }

        /// <summary>
        /// Deserializes a JSON string into an object of the specified type using PascalCase property naming.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized object, or the default of <typeparamref name="T"/> if <paramref name="json"/> is null or empty.</returns>
        [Obsolete("Newtonsoft.Json only. Deserialize<T> matches property names case-insensitively.")]
        public static T DeserializePascal<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default(T);

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new DefaultContractResolver()
                {
                    NamingStrategy = new PascalCaseNamingStrategy()
                }
            };

            return JsonConvert.DeserializeObject<T>(json, settings);
        }

        /// <summary>
        /// Deserializes a JSON string into a loosely typed Newtonsoft object using PascalCase property naming.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized object, the original string if deserialization fails, or <c>null</c> if <paramref name="json"/> is null or empty.</returns>
        [Obsolete("Newtonsoft.Json only: returns JObject or JArray. Use Deserialize<T>, Deserialize<JsonElement> or ToDictionary.")]
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new DefaultContractResolver()
                {
                    NamingStrategy = new PascalCaseNamingStrategy()
                }
            };

            try
            {
                return JsonConvert.DeserializeObject(json, settings);
            }
            catch
            {
                return json;
            }
        }

        /// <summary>
        /// Serializes an object to camelCase JSON, leaving out null members.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <returns>The JSON representation, or <c>null</c> if serialization fails.</returns>
        public static string SerializeIgnoreNulls(object value)
        {
            try
            {
                return JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), RIoT2JsonOptions.IgnoreNulls);
            }
            catch (Exception x)
            {
                Console.WriteLine("Error:" + x.Message);
                return null;
            }
        }

        /// <summary>
        /// Serializes an object to camelCase JSON.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <returns>The JSON representation of the object.</returns>
        public static string Serialize(object value)
        {
            return JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), RIoT2JsonOptions.Default);
        }

        /// <summary>
        /// Adds a top-level attribute to a JSON object.
        /// </summary>
        /// <param name="json">The JSON string to modify.</param>
        /// <param name="attributeName">The name of the attribute to add.</param>
        /// <param name="attributeValue">The value of the attribute to add.</param>
        /// <returns>The updated JSON string, or the original string if it is not a JSON object or already has the attribute.</returns>
        public static string InjectAttributeToJson(string json, string attributeName, string attributeValue)
        {
            try
            {
                var msg = ParseObject(json);
                msg.Add(attributeName, attributeValue);
                return msg.ToJsonString(RIoT2JsonOptions.Default);
            }
            catch (Exception x) when (x is System.Text.Json.JsonException || x is ArgumentException || x is InvalidOperationException)
            {
                // Not a JSON object: keep the original.
                return json;
            }
        }

        /// <summary>
        /// Replaces every top-level property whose value matches the specified placeholder with the given value.
        /// </summary>
        /// <param name="json">The JSON object to modify.</param>
        /// <param name="placeholder">The placeholder value to match (case-insensitive).</param>
        /// <param name="value">The replacement value, typed as a boolean, number, object or text.</param>
        /// <returns>The updated JSON, indented.</returns>
        public static string InjectValueToJson(string json, string placeholder, string value)
        {
            var jObject = ParseObject(json);
            var keys = jObject.Where(e => string.Equals(TokenText(e.Value), placeholder, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Key).ToList();

            foreach (var key in keys)
                jObject[key] = CreateValue(value);

            return jObject.ToJsonString(RIoT2JsonOptions.Indented);
        }

        /// <summary>
        /// Replaces placeholders within a text template using values from a JSON payload.
        /// </summary>
        /// <param name="jsonData">The JSON data providing replacement values.</param>
        /// <param name="text">The text template containing placeholders.</param>
        /// <param name="placeholder">The base placeholder token; object properties are matched as <c>placeholder.property</c>.</param>
        /// <returns>The text with placeholders replaced.</returns>
        public static string InjectDataToText(string jsonData, string text, string placeholder)
        {
            var valueType = GetDataType(jsonData, out object value);
            if (valueType != ValueType.Entity)
                return text.Replace(placeholder, jsonData);

            var injectedText = text;
            foreach (var e in (JsonObject)value)
                injectedText = injectedText.Replace($"{placeholder}.{e.Key}", TokenText(e.Value));
            return injectedText;
        }

        /// <summary>
        /// Gets the text of a top-level attribute: a string as it is, <c>True</c>/<c>False</c>, a
        /// number's digits, or an object's or array's indented JSON.
        /// </summary>
        /// <param name="json">The JSON string to read.</param>
        /// <param name="attribute">The name of the attribute to read (case-sensitive).</param>
        /// <returns>The attribute value, or <c>null</c> if it is missing or the JSON is not an object.</returns>
        public static string GetValue(string json, string attribute)
        {
            try
            {
                var msg = ParseObject(json);
                if (msg.TryGetPropertyValue(attribute, out var value))
                    return TokenText(value);
            }
            catch (Exception x) when (x is System.Text.Json.JsonException || x is InvalidOperationException || x is ArgumentException)
            {
                // Not a JSON object.
            }
            return null;
        }

        /// <summary>
        /// Finds a value in a JSON object with a JSONPath. Supported: an optional <c>$</c>, member
        /// names (<c>a.b</c>, <c>['a b']</c>) and array indexes (<c>items[0]</c>); not wildcards,
        /// filters, slices or recursive descent.
        /// </summary>
        /// <param name="json">The JSON object to search.</param>
        /// <param name="path">The path.</param>
        /// <returns>The value's text (as <see cref="GetValue"/>, but objects and arrays compact), or <c>null</c> if not found.</returns>
        /// <exception cref="ArgumentException">The path uses unsupported JSONPath syntax.</exception>
        public static string FindValue(string json, string path)
        {
            if (!TrySelect(ParseObject(json), path, out var token, out _, out _) || token == null)
                return null;

            return token is JsonValue ? TokenText(token) : token.ToJsonString(RIoT2JsonOptions.Default);
        }

        /// <summary>
        /// Sets a value in a JSON object at the location identified by a JSONPath (see
        /// <see cref="FindValue"/>).
        /// </summary>
        /// <param name="json">The JSON object to modify.</param>
        /// <param name="path">The path of an existing value.</param>
        /// <param name="value">The value to set, serialized with its member names as declared.</param>
        /// <returns>The updated JSON, indented, or the original JSON if the path was not found.</returns>
        public static string SetValue(string json, string path, object value)
        {
            var root = ParseObject(json);
            if (!TrySelect(root, path, out _, out var parent, out var key) || parent == null)
                return json;

            var replacement = JsonSerializer.SerializeToNode(value, value?.GetType() ?? typeof(object), RIoT2JsonOptions.AsDeclared);
            if (parent is JsonObject asObject)
                asObject[(string)key] = replacement;
            else if (parent is JsonArray asArray)
                asArray[(int)key] = replacement;

            return root.ToJsonString(RIoT2JsonOptions.Indented);
        }

        /// <summary>
        /// Determines whether the specified string is a valid JSON object or array.
        /// </summary>
        /// <param name="str">The string to test.</param>
        /// <returns><c>true</c> if the string is valid JSON; otherwise, <c>false</c>.</returns>
        public static bool IsJson(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) { return false; }
            str = str.Trim();
            if ((str.StartsWith("{") && str.EndsWith("}")) || //For object
                (str.StartsWith("[") && str.EndsWith("]"))) //For array
            {
                try
                {
                    using (JsonDocument.Parse(str, DocumentOptions))
                        return true;
                }
                catch (System.Text.Json.JsonException)
                {
                    return false;
                }
            }
            return false;
        }

        private static readonly JsonDocumentOptions DocumentOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        private static readonly JsonNodeOptions NodeOptions = new JsonNodeOptions { PropertyNameCaseInsensitive = false };

        private static JsonObject ParseObject(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));
            using (var document = JsonDocument.Parse(json, DocumentOptions))
                return ToNode(document.RootElement) as JsonObject
                    ?? throw new System.Text.Json.JsonException("The JSON is not an object.");
        }

        // A JsonNode where a repeated property name keeps its last value, as Newtonsoft's JObject did.
        private static JsonNode ToNode(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new JsonObject(NodeOptions);
                    foreach (var property in element.EnumerateObject())
                        obj[property.Name] = ToNode(property.Value);
                    return obj;
                case JsonValueKind.Array:
                    var array = new JsonArray(NodeOptions);
                    foreach (var item in element.EnumerateArray())
                        array.Add(ToNode(item));
                    return array;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                default:
                    return JsonValue.Create(element.Clone(), NodeOptions);
            }
        }

        private static IDictionary<string, object> ObjectToDictionary(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new System.Text.Json.JsonException($"Expected a JSON object, not {element.ValueKind}.");
            return (IDictionary<string, object>)ToClr(element);
        }

        // Newtonsoft's materialization: long for integers, double for other numbers.
        private static object ToClr(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dictionary = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                        dictionary[property.Name] = ToClr(property.Value);
                    return dictionary;
                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(ToClr).ToArray();
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return element.TryGetInt64(out var integer) ? integer : (object)element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }

        // The text Newtonsoft's JToken.ToString() gave a value: strings as they are, True/False,
        // numbers as their digits, null as empty, and objects and arrays as indented JSON.
        private static string TokenText(JsonNode node)
        {
            if (node == null)
                return string.Empty;
            if (!(node is JsonValue value))
                return node.ToJsonString(RIoT2JsonOptions.Indented);

            if (!value.TryGetValue(out JsonElement element))
                element = JsonSerializer.SerializeToElement(value);
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.True:
                    return bool.TrueString;
                case JsonValueKind.False:
                    return bool.FalseString;
                case JsonValueKind.Number:
                    return element.TryGetInt64(out var integer)
                        ? integer.ToString(CultureInfo.InvariantCulture)
                        : element.GetDouble().ToString("R", CultureInfo.InvariantCulture);
                default:
                    return string.Empty;
            }
        }

        // Follows a JSONPath subset. True when the path exists (its value may be JSON null); parent and
        // key say where it is. A JSON null before the end of the path means the path doesn't exist.
        private static bool TrySelect(JsonObject root, string path, out JsonNode value, out JsonNode parent, out object key)
        {
            value = root;
            parent = null;
            key = null;
            foreach (var segment in PathSegments(path))
            {
                var container = value;
                bool found;
                if (segment is string name)
                {
                    found = container is JsonObject obj && obj.TryGetPropertyValue(name, out value);
                }
                else
                {
                    var index = (int)segment;
                    found = container is JsonArray array && index >= 0 && index < array.Count;
                    value = found ? ((JsonArray)container)[index] : null;
                }
                if (!found)
                {
                    value = null;
                    parent = null;
                    key = null;
                    return false;
                }
                parent = container;
                key = segment;
            }
            return true;
        }

        private static IEnumerable<object> PathSegments(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("The path is empty.", nameof(path));

            var segments = new List<object>();
            var i = 0;
            if (path[0] == '$')
                i = 1;
            while (i < path.Length)
            {
                var c = path[i];
                if (c == '.')
                {
                    if (i + 1 < path.Length && path[i + 1] == '.')
                        throw Unsupported(path);
                    i++;
                    continue;
                }
                if (c == '[')
                {
                    var end = path.IndexOf(']', i);
                    if (end < 0)
                        throw Unsupported(path);
                    var inner = path.Substring(i + 1, end - i - 1).Trim();
                    if (inner.Length >= 2 && (inner[0] == '\'' || inner[0] == '"') && inner[inner.Length - 1] == inner[0])
                        segments.Add(inner.Substring(1, inner.Length - 2));
                    else if (int.TryParse(inner, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                        segments.Add(index);
                    else
                        throw Unsupported(path);
                    i = end + 1;
                    continue;
                }
                var start = i;
                while (i < path.Length && path[i] != '.' && path[i] != '[')
                    i++;
                var name = path.Substring(start, i - start);
                if (name == "*" || name.Contains("?") || name.Contains("("))
                    throw Unsupported(path);
                segments.Add(name);
            }
            return segments;
        }

        private static ArgumentException Unsupported(string path) =>
            new ArgumentException($"Unsupported JSONPath '{path}': use member names, ['quoted names'] and [index].", nameof(path));

        private static ValueType GetDataType(string data, out object value)
        {
            if (bool.TryParse(data, out var boolValue))
            {
                value = boolValue;
                return ValueType.Boolean;
            }

            // Prefer higher-precision numeric types over float to avoid precision loss.
            // Integers that fit in a long stay integral; fractional/large values use decimal.
            if (long.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                value = longValue;
                return ValueType.Number;
            }

            if (decimal.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
            {
                value = decimalValue;
                return ValueType.Number;
            }

            try
            {
                if (ParseObject(data) is JsonObject jObject && jObject.Count > 0)
                {
                    value = jObject;
                    return ValueType.Entity;
                }
            }
            catch (System.Text.Json.JsonException) { }

            value = data;
            return ValueType.Text;
        }

        private static JsonNode CreateValue(string value)
        {
            GetDataType(value, out object typedValue);
            switch (typedValue)
            {
                case JsonNode node:
                    return node;
                case bool b:
                    return JsonValue.Create(b);
                case long l:
                    return JsonValue.Create(l);
                case decimal d:
                    return JsonValue.Create(d);
                default:
                    return JsonValue.Create((string)typedValue);
            }
        }

        /// <summary>
        /// Converts a JSON string into a JavaScript object literal string.
        /// </summary>
        /// <param name="json">A valid JSON string.</param>
        /// <param name="unquotePropertyNamesWhenSafe">When true, property names that are valid JS identifiers are not quoted.</param>
        /// <param name="prettyPrint">When true, formats with indentation and newlines.</param>
        /// <param name="indentSize">Number of spaces per indent level when prettyPrint is true.</param>
        /// <param name="useSingleQuotesForStrings">When true, uses single quotes for strings; otherwise double quotes.</param>
        /// <returns>JavaScript object literal text.</returns>
        public static string JsonToJsObjectLiteral(
            string json,
            bool unquotePropertyNamesWhenSafe = true,
            bool prettyPrint = true,
            int indentSize = 2,
            bool useSingleQuotesForStrings = true)
        {
            var options = new System.Text.Json.JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = System.Text.Json.JsonCommentHandling.Skip
            };

            System.Text.Json.JsonDocument doc = null;
            try
            {
                doc = System.Text.Json.JsonDocument.Parse(json, options);

                var sb = new StringBuilder(json.Length + 64);
                var ctx = new WriteContext(sb, prettyPrint, indentSize, useSingleQuotesForStrings, unquotePropertyNamesWhenSafe);
                WriteValue(doc.RootElement, ctx, 0);
                return sb.ToString();
            }
            finally
            {
                if (doc != null)
                    doc.Dispose();
            }
        }

        /// <summary>
        /// Safe try-version that won't throw. On error, returns false and sets error message.
        /// </summary>
        /// <param name="json">A valid JSON string to convert.</param>
        /// <param name="js">When this method returns <c>true</c>, contains the JavaScript object literal text; otherwise, <c>null</c>.</param>
        /// <param name="error">When this method returns <c>false</c>, contains the error message; otherwise, <c>null</c>.</param>
        /// <param name="unquotePropertyNamesWhenSafe">When true, property names that are valid JS identifiers are not quoted.</param>
        /// <param name="prettyPrint">When true, formats with indentation and newlines.</param>
        /// <param name="indentSize">Number of spaces per indent level when prettyPrint is true.</param>
        /// <param name="useSingleQuotesForStrings">When true, uses single quotes for strings; otherwise double quotes.</param>
        /// <returns><c>true</c> if the conversion succeeded; otherwise, <c>false</c>.</returns>
        public static bool TryJsonToJsObjectLiteral(
            string json,
            out string js,
            out string error,
            bool unquotePropertyNamesWhenSafe = true,
            bool prettyPrint = true,
            int indentSize = 2,
            bool useSingleQuotesForStrings = true)
        {
            try
            {
                js = JsonToJsObjectLiteral(json, unquotePropertyNamesWhenSafe, prettyPrint, indentSize, useSingleQuotesForStrings);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                js = null;
                error = ex.Message;
                return false;
            }
        }

        private static void WriteValue(System.Text.Json.JsonElement el, WriteContext ctx, int level)
        {
            switch (el.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    ctx.Append('{');
                    int propCount = 0;

                    foreach (var prop in el.EnumerateObject())
                    {
                        if (propCount++ == 0)
                        {
                            if (ctx.Pretty) ctx.AppendLine();
                        }
                        else
                        {
                            ctx.Append(',');
                            if (ctx.Pretty) ctx.AppendLine();
                        }

                        if (ctx.Pretty) ctx.AppendIndent(level + 1);

                        WritePropertyName(prop.Name, ctx);
                        ctx.Append(ctx.Pretty ? ": " : ":");
                        WriteValue(prop.Value, ctx, level + 1);
                    }

                    if (propCount > 0 && ctx.Pretty)
                    {
                        ctx.AppendLine();
                        ctx.AppendIndent(level);
                    }

                    ctx.Append('}');
                    break;

                case System.Text.Json.JsonValueKind.Array:
                    ctx.Append('[');
                    int itemCount = 0;

                    foreach (var item in el.EnumerateArray())
                    {
                        if (itemCount++ == 0)
                        {
                            if (ctx.Pretty) ctx.AppendLine();
                        }
                        else
                        {
                            ctx.Append(',');
                            if (ctx.Pretty) ctx.AppendLine();
                        }

                        if (ctx.Pretty) ctx.AppendIndent(level + 1);
                        WriteValue(item, ctx, level + 1);
                    }

                    if (itemCount > 0 && ctx.Pretty)
                    {
                        ctx.AppendLine();
                        ctx.AppendIndent(level);
                    }

                    ctx.Append(']');
                    break;

                case System.Text.Json.JsonValueKind.String:
                    {
                        var s = el.GetString() ?? string.Empty;
                        ctx.Append(EscapeJsString(s, ctx.UseSingleQuotes));
                        break;
                    }

                case System.Text.Json.JsonValueKind.Number:
                case System.Text.Json.JsonValueKind.True:
                case System.Text.Json.JsonValueKind.False:
                case System.Text.Json.JsonValueKind.Null:
                    // Use raw text to preserve formatting (e.g., "1e-6", "0.0")
                    ctx.Append(el.GetRawText());
                    break;

                default:
                    ctx.Append(el.GetRawText());
                    break;
            }
        }

        private static void WritePropertyName(string name, WriteContext ctx)
        {
            if (ctx.UnquotePropertyNamesWhenSafe && IsValidJsIdentifier(name))
            {
                ctx.Append(name);
            }
            else
            {
                ctx.Append(EscapeJsString(name, ctx.UseSingleQuotes));
            }
        }

        /// <summary>
        /// Checks if a string is a safe JS identifier (ASCII-only heuristic).
        /// </summary>
        private static bool IsValidJsIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            char first = name[0];
            if (!(first == '$' || first == '_' || char.IsLetter(first))) return false;

            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!(c == '$' || c == '_' || char.IsLetterOrDigit(c)))
                    return false;
            }

            // ES5+ allows reserved words in object literal property names without quotes.
            return true;
        }

        /// <summary>
        /// Escapes a .NET string to a JavaScript string literal (single or double quotes), including surrounding quotes.
        /// Handles control chars, backslash, the quote char, and U+2028/U+2029.
        /// </summary>
        private static string EscapeJsString(string s, bool useSingleQuotes)
        {
            char quote = useSingleQuotes ? '\'' : '"';
            var sb = new StringBuilder(s.Length + 2);
            sb.Append(quote);

            for (int i = 0; i < s.Length; i++)
            {
                var ch = s[i];
                switch (ch)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '\b': sb.Append(@"\b"); break;
                    case '\f': sb.Append(@"\f"); break;
                    case '\n': sb.Append(@"\n"); break;
                    case '\r': sb.Append(@"\r"); break;
                    case '\t': sb.Append(@"\t"); break;
                    case '\u2028': sb.Append(@"\u2028"); break; // Line separator
                    case '\u2029': sb.Append(@"\u2029"); break; // Paragraph separator
                    default:
                        if (ch == quote)
                        {
                            sb.Append('\\').Append(quote);
                        }
                        else if (char.IsControl(ch))
                        {
                            sb.Append(@"\u").Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }

            sb.Append(quote);
            return sb.ToString();
        }

        private sealed class WriteContext
        {
            public WriteContext(StringBuilder sb, bool pretty, int indentSize, bool useSingleQuotes, bool unquotePropNames)
            {
                Sb = sb;
                Pretty = pretty;
                IndentSize = Math.Max(0, indentSize);
                UseSingleQuotes = useSingleQuotes;
                UnquotePropertyNamesWhenSafe = unquotePropNames;
            }

            public StringBuilder Sb { get; private set; }
            public bool Pretty { get; private set; }
            public int IndentSize { get; private set; }
            public bool UseSingleQuotes { get; private set; }
            public bool UnquotePropertyNamesWhenSafe { get; private set; }

            public void Append(char c) { Sb.Append(c); }
            public void Append(string s) { Sb.Append(s); }
            public void AppendLine() { Sb.AppendLine(); }

            public void AppendIndent(int level)
            {
                if (!Pretty || IndentSize == 0 || level <= 0) return;
                Sb.Append(' ', level * IndentSize);
            }
        }
    }
}
