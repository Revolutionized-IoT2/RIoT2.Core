using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// Provides static helper methods for JSON serialization, deserialization, and manipulation
    /// used across the RIoT2 solution.
    /// </summary>
    public static class Json
    {
        /// <summary>
        /// Converts a JSON string into a nested dictionary.
        /// </summary>
        /// <param name="json">The JSON string to convert.</param>
        /// <returns>A dictionary representing the JSON object, with nested objects and arrays materialized.</returns>
        public static IDictionary<string, object> ToDictionary(string json)
        {
            return ToDictionary(JObject.Parse(json));
        }

        /// <summary>
        /// Converts an object into a nested dictionary via its JSON representation.
        /// </summary>
        /// <param name="obj">The object to convert.</param>
        /// <returns>A dictionary representing the object.</returns>
        public static IDictionary<string, object> ToDictionary(object obj)
        {
            if (obj is JObject)
                return ToDictionary(obj as JObject);
            else
                return ToDictionary(JObject.FromObject(obj));
        }

        /// <summary>
        /// Converts a <see cref="JObject"/> into a nested dictionary.
        /// </summary>
        /// <param name="jObject">The JSON object to convert.</param>
        /// <returns>A dictionary representing the JSON object, or <c>null</c> if <paramref name="jObject"/> is <c>null</c>.</returns>
        public static IDictionary<string, object> ToDictionary(JObject jObject)
        {
            if (jObject == null)
                return null;

            var result = jObject.ToObject<Dictionary<string, object>>();

            var JObjectKeys = (from r in result
                               let key = r.Key
                               let value = r.Value
                               where value.GetType() == typeof(JObject)
                               select key).ToList();

            var JArrayKeys = (from r in result
                              let key = r.Key
                              let value = r.Value
                              where value.GetType() == typeof(JArray)
                              select key).ToList();

            JArrayKeys.ForEach(key => result[key] = ((JArray)result[key]).Values().Select(x => ((JValue)x).Value).ToArray());
            JObjectKeys.ForEach(key => result[key] = ToDictionary((result[key] as JObject).ToString()));

            return result;
        }

        /// <summary>
        /// Deserializes a JSON string into an object of the specified type.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <param name="toType">The target type.</param>
        /// <returns>The deserialized object, or <c>null</c> if <paramref name="json"/> is null or empty.</returns>
        public static object Deserialize(string json, System.Type toType)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonConvert.DeserializeObject(json, toType);
        }

        /// <summary>
        /// Deserializes a JSON string with automatic type-name handling, allowing polymorphic types to be resolved.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized object, or the default of <typeparamref name="T"/> if <paramref name="json"/> is null or empty.</returns>
        public static T DeserializeAutoTypeNameHandling<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default(T);

            return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto
            });
        }

        /// <summary>
        /// Serializes an object to JSON using camelCase property names, optionally emitting type information and nulls.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="autoTypeNameHandling">If <c>true</c>, embeds type information to support polymorphic deserialization.</param>
        /// <param name="includeNulls">If <c>true</c>, includes properties with null values.</param>
        /// <returns>The JSON representation of the object.</returns>
        public static string SerializeAutoTypeNameHandling(object obj, bool autoTypeNameHandling = false, bool includeNulls = false)
        {
            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings()
            {
                TypeNameHandling = autoTypeNameHandling ? TypeNameHandling.Auto : TypeNameHandling.None,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = includeNulls ? NullValueHandling.Include : NullValueHandling.Ignore,
            });
        }

        /// <summary>
        /// Deserializes a JSON string into an object of the specified type, honoring <see cref="ValueModel"/> conversion.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized object, or the default of <typeparamref name="T"/> if <paramref name="json"/> is null or empty.</returns>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default(T);

            return JsonConvert.DeserializeObject<T>(json, new ValueModelConverter());
        }

        /// <summary>
        /// Deserializes a JSON string into an object of the specified type using PascalCase property naming.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized object, or the default of <typeparamref name="T"/> if <paramref name="json"/> is null or empty.</returns>
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
        /// Deserializes a JSON string into a loosely typed object using PascalCase property naming.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized object, the original string if deserialization fails, or <c>null</c> if <paramref name="json"/> is null or empty.</returns>
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
        /// Serializes an object to camelCase JSON, ignoring properties with null values.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <returns>The JSON representation, or <c>null</c> if serialization fails.</returns>
        public static string SerializeIgnoreNulls(object value)
        {
            try
            {
                return JsonConvert.SerializeObject(value, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    Converters = { new ValueModelConverter() }
                });
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
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = { new ValueModelConverter() }
            };

            return JsonConvert.SerializeObject(value, settings);
        }

        /// <summary>
        /// Adds a top-level attribute to a JSON object.
        /// </summary>
        /// <param name="json">The JSON string to modify.</param>
        /// <param name="attributeName">The name of the attribute to add.</param>
        /// <param name="attributeValue">The value of the attribute to add.</param>
        /// <returns>The updated JSON string, or the original string if it is not valid JSON.</returns>
        public static string InjectAttributeToJson(string json, string attributeName, string attributeValue)
        {
            try
            {
                JObject msg = JObject.Parse(json);
                msg.Add(attributeName, attributeValue);
                json = Json.Serialize(msg);
            }
            catch
            {
                //not valid json -> lets use original
            }
            return json;
        }

        /// <summary>
        /// Replaces every top-level property whose value matches the specified placeholder with the given value.
        /// </summary>
        /// <param name="json">The JSON string to modify.</param>
        /// <param name="placeholder">The placeholder value to match (case-insensitive).</param>
        /// <param name="value">The replacement value.</param>
        /// <returns>The updated JSON string.</returns>
        public static string InjectValueToJson(string json, string placeholder, string value)
        {
            JObject jObject = JObject.Parse(json);
            var keys = new List<string>();
            foreach (var e in jObject)
            {
                if (e.Value.ToString().ToLower() == placeholder.ToLower())
                    keys.Add(e.Key);
            }

            var jvalue = createJValue(value);
            //update all properties
            foreach (string k in keys)
                jObject[k] = jvalue;

            return jObject.ToString();
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
            var valueType = getDataType(jsonData, out object value);
            if (valueType != ValueType.Entity) //if data is not object 
                return text.Replace(placeholder, jsonData);

            var injectedText = text;
            foreach (var e in (value as JObject))
            {
                var objectPlaceholder = $"{placeholder}.{e.Key}";
                injectedText = injectedText.Replace(objectPlaceholder, e.Value.ToString());
            }
            return injectedText;
        }

        private static ValueType getDataType(string data, out object value)
        {
            if (bool.TryParse(data, out var boolValue))
            {
                value = boolValue;
                return ValueType.Boolean;
            }

            if (float.TryParse(data, out var floatValue))
            {
                value = floatValue;
                return ValueType.Number;
            }

            try
            {
                var jObject = JObject.Parse(data);
                if (jObject.Count > 0)
                {
                    value = jObject;
                    return ValueType.Entity;
                }
            }
            catch (JsonReaderException) { }

            value = data;
            return ValueType.Text;
        }

        private static JToken createJValue(string value)
        {
            var valueType = getDataType(value, out object typedValue);
            return new JValue(typedValue);
        }

        /// <summary>
        /// Gets the string value of a top-level attribute.
        /// </summary>
        /// <param name="json">The JSON string to read.</param>
        /// <param name="attribute">The name of the attribute to read.</param>
        /// <returns>The attribute value, or <c>null</c> if it is missing or the JSON is invalid.</returns>
        public static string GetValue(string json, string attribute)
        {
            try
            {
                JObject msg = JObject.Parse(json);
                if (msg.ContainsKey(attribute))
                    return msg.GetValue(attribute).ToString();
            }
            catch
            {
                //not valid json -> return null;
            }
            return null;
        }

        /// <summary>
        /// Finds a value in a JSON document using a JSONPath expression.
        /// </summary>
        /// <param name="json">The JSON string to search.</param>
        /// <param name="path">The JSONPath expression identifying the value.</param>
        /// <returns>The matched value as a string, or <c>null</c> if not found.</returns>
        public static string FindValue(string json, string path)
        {
            return (string)JObject.Parse(json).SelectToken(path);
        }

        /// <summary>
        /// Sets a value in a JSON document at the location identified by a JSONPath expression.
        /// </summary>
        /// <param name="json">The JSON string to modify.</param>
        /// <param name="path">The JSONPath expression identifying the target.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>The updated JSON string, or the original JSON if the path was not found.</returns>
        public static string SetValue(string json, string path, object value)
        {
            var j = JObject.Parse(json).SelectToken(path);
            if (j == null)
                return json;

            (j as JProperty).Value = JToken.FromObject(value);

            return j.ToString();
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
                    var obj = JToken.Parse(str);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
    public class ValueModelConverter : JsonConverter<ValueModel>
    {
        public override void WriteJson(JsonWriter writer, ValueModel value, JsonSerializer serializer)
        {
            writer.WriteRawValue(value.ToJson());
        }

        public override ValueModel ReadJson(JsonReader reader, Type objectType, ValueModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Null)
            {
                JValue jValue = new JValue(reader.Value);
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        return new ValueModel(JObject.Load(reader).ToString());
                    case JsonToken.Boolean:
                        return new ValueModel((bool)jValue);
                    case JsonToken.Integer:
                        return new ValueModel((int)jValue);
                    case JsonToken.Float:
                        return new ValueModel((double)jValue);
                    case JsonToken.String:
                    default:
                        return new ValueModel(reader.Value.ToString());
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
        public T ToObject<T>()
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
                if (e.Key.ToLower() == property.ToLower())
                    return true;
            }
            return false;
        }

        private string getPropertyIgnoreCase(string property)
        {
            foreach (var e in _jobj)
            {
                if (e.Key.ToLower() == property.ToLower())
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
        public T FindValue<T>(string path)
        {
            var token = _jobj.SelectToken(path);
            if (token != null)
            {
                if (token.Type == JTokenType.Object)
                {
                    return (token as JObject).ToObject<T>();
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
        public string GetValueAsJson(string property)
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
        public object GetValue(string property)
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
        public void ReplacePlaceHolderWithValue(string placeholder, object value)
        {
            traverseJObject(placeholder, value, _jobj);
        }

        private void updateOrAddInPath(string path, object valueToAdd)
        {
            var pathParts = path.Split('.');
            JToken node = _jobj;
            for (int i = 0; i < pathParts.Length; i++)
            {
                var pathPart = pathParts[i];
                var partNode = node.SelectToken(pathPart);
                if (partNode == null && i < pathParts.Length - 1)
                {
                    ((JObject)node).Add(pathPart, new JObject());
                    partNode = node.SelectToken(pathPart);
                }
                else if (partNode == null && i == pathParts.Length - 1)
                {
                    ((JObject)node).Add(pathPart, JToken.FromObject(valueToAdd));
                    partNode = node.SelectToken(pathPart);
                }
                node = partNode;
            }
        }
        private void traverseJObject(string placeholder, object value, JObject parent)
        {
            foreach (var e in parent)
            {
                if (e.Value is JArray)
                {
                    var lst = (e.Value as JArray);
                    for (int i = 0; i < lst.Count; i++)
                    {
                        var ta = lst[i];
                        if (ta is JObject)
                            traverseJObject(placeholder, value, ta as JObject);

                        if (ta.ToString().ToLower() == placeholder.ToLower())
                            (e.Value as JArray)[i] = new JValue(value);
                    }
                }
                else if (e.Value is JObject)
                {
                    foreach (var to in (e.Value as JObject))
                        traverseJObject(placeholder, value, e.Value as JObject);
                }

                if (e.Value.ToString().ToLower() == placeholder.ToLower())
                    parent[e.Key] = new JValue(value);
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
