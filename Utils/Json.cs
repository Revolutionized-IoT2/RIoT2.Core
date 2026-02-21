using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Utils
{
    public static class Json
    {
        public static IDictionary<string, object> ToDictionary(string json)
        {
            return ToDictionary(JObject.Parse(json));
        }

        public static IDictionary<string, object> ToDictionary(object obj)
        {
            if (obj is JObject)
                return ToDictionary(obj as JObject);
            else
                return ToDictionary(JObject.FromObject(obj));
        }

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

        public static object Deserialize(string json, System.Type toType)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonConvert.DeserializeObject(json, toType);
        }

        public static T DeserializeAutoTypeNameHandling<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default(T);

            return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto
            });
        }

        public static string SerializeAutoTypeNameHandling(object obj, bool autoTypeNameHandling = false, bool includeNulls = false)
        {
            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings()
            {
                TypeNameHandling = autoTypeNameHandling ? TypeNameHandling.Auto : TypeNameHandling.None,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = includeNulls ? NullValueHandling.Include : NullValueHandling.Ignore,
            });
        }

        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default(T);

            return JsonConvert.DeserializeObject<T>(json, new ValueModelConverter());
        }

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

        public static string Serialize(object value)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = { new ValueModelConverter() }
            };

            return JsonConvert.SerializeObject(value, settings);
        }

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

        public static string FindValue(string json, string path)
        {
            return (string)JObject.Parse(json).SelectToken(path);
        }

        public static string SetValue(string json, string path, object value)
        {
            var j = JObject.Parse(json).SelectToken(path);
            if (j == null)
                return json;

            (j as JProperty).Value = JToken.FromObject(value);

            return j.ToString();
        }

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
                        return new ValueModel((float)jValue);
                    case JsonToken.String:
                    default:
                        return new ValueModel(reader.Value.ToString());
                }
            }
            return null;
        }
    }

    public class JsonEntity
    {
        private JObject _jobj;

        internal JObject JsonObject { get { return _jobj; } }
        public JsonEntity(string json)
        {
            _jobj = JObject.Parse(json);
        }

        public JsonEntity(JObject jobject)
        {
            _jobj = jobject;
        }

        public void Merge(JsonEntity from)
        {

            _jobj.Merge(from.JsonObject, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Union,
                PropertyNameComparison = System.StringComparison.InvariantCultureIgnoreCase
            });
        }

        public override string ToString()
        {
            return _jobj.ToString();
        }

        public T ToObject<T>()
        {
            return _jobj.ToObject<T>();
        }

        public void RemoveProperties(List<string> properties)
        {
            foreach (var k in properties)
                _jobj.Remove(k);
        }

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

        public IEnumerable<string> GetProperties()
        {
            foreach (var e in _jobj)
                yield return e.Key;
        }

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

        public void Remove(string property)
        {
            if (_jobj.ContainsKey(property))
            {
                _jobj.Remove(property);
            }
        }

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
