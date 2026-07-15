using Newtonsoft.Json.Linq;
using RIoT2.Core.Utils;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;


namespace RIoT2.Core.Models
{
    public class ValueModel
    {
        private JsonElement _value; //store value as jsonelement
        private Type _objectType;

        #region Ctor
        public ValueModel(object value)
        {
            if (value == null)
            {
                _value = JsonSerializer.SerializeToElement<object>(null);
                return;
            }

            if (value is ValueModel valueModel) 
            {
                _value = valueModel._value.Clone();
                _objectType = valueModel._objectType;
                return;
            }

            if (value is JsonElement) 
            {
                _value = (JsonElement)value;
                return;
            }

            if (value is JObject) 
            {
                var json = (value as JObject).ToString();
                var jsonDocument = isJson(json);
                if (jsonDocument != null)
                    _value = jsonDocument.RootElement;

                return;
            }

            var t = value.GetType();
            var ctor = t.GetConstructor(new Type[0]);
            if (ctor != null) 
            {
                var isJsonConstructor = Attribute.IsDefined(ctor, typeof(System.Text.Json.Serialization.JsonConstructorAttribute));
                if (isJsonConstructor)
                    _objectType = t; //store types for all objects that have JsonConstructor defined
            }
            _value = System.Text.Json.JsonSerializer.SerializeToElement(value, getOptions());
        }

        public ValueModel(bool value)
        {
            _value = System.Text.Json.JsonSerializer.SerializeToElement(value);
        }

        public ValueModel(double value)
        {
            _value = System.Text.Json.JsonSerializer.SerializeToElement(value);
        }

        public ValueModel(decimal value)
        {
            _value = System.Text.Json.JsonSerializer.SerializeToElement(value);
        }

        public ValueModel(int value)
        {
            _value = System.Text.Json.JsonSerializer.SerializeToElement(value);
        }

        public ValueModel(string value, bool enforceCamelCase = true)
        {
            var jsonDocument = isJson(value, enforceCamelCase);
            if (jsonDocument != null)
                _value = jsonDocument.RootElement;
            else
                _value = System.Text.Json.JsonSerializer.SerializeToElement(value, getOptions());
        }
        #endregion

        public ValueType Type
        {
            get {
                switch (_value.ValueKind)
                {
                    case JsonValueKind.String:
                        return ValueType.Text;
                    case JsonValueKind.Number:
                        return ValueType.Number;
                    case JsonValueKind.Object:
                        return ValueType.Entity;
                    case JsonValueKind.Array:
                        return ValueType.TextArray;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return ValueType.Boolean;
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                    default:
                        return ValueType.Text;
                }
            }
        }

        public static object GetDefaultValueForType(ValueType valueType)
        {
            switch (valueType)
            {
                case Core.ValueType.Boolean:
                    return false;
                case Core.ValueType.Text:
                    return "string";
                case Core.ValueType.Number:
                    return 0;
                case Core.ValueType.Entity:
                    return new { };
                case Core.ValueType.TextArray:
                    return new string[] { };
                default:
                    return default;
            }
        }

        /// <summary>
        /// JsonElement is converted to Expando, List, or Primitive. 
        /// Json numbers are converted either into Int or Decimal, depending if there is a dot in json-value
        /// Undefined or Null is returned as Null
        /// </summary>
        /// <returns>Expando object, List, or a primitive</returns>
        public object GetAsObject()
        {
            try
            {
                if (_objectType != null)
                    return System.Text.Json.JsonSerializer.Deserialize(_value, _objectType, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch
            {
                //do nothing if deserialization fails..
            }

            return handleElement(_value);
        }

        private object handleElement(JsonElement elem) 
        {
            if (elem.ValueKind == JsonValueKind.Null || elem.ValueKind == JsonValueKind.Undefined)
                return null;

            if (elem.ValueKind == JsonValueKind.Object)
            {
                var expandoDict = new ExpandoObject() as IDictionary<string, Object>;
                foreach (JsonProperty property in elem.EnumerateObject())
                {
                    switch (property.Value.ValueKind)
                    {
                        case JsonValueKind.Object:
                            expandoDict.Add(property.Name, handleElement(property.Value));
                            break;
                        case JsonValueKind.Array:
                            var lst = new List<object>();
                            foreach (var arrayProp in property.Value.EnumerateArray())
                                lst.Add(handleElement(arrayProp));

                            expandoDict.Add(property.Name, lst);
                            break;
                        case JsonValueKind.String:
                            expandoDict.Add(property.Name, property.Value.GetString());
                            break;
                        case JsonValueKind.Number:
                            if (property.Value.TryGetInt32(out var propInt))
                                expandoDict.Add(property.Name, propInt);
                            else if (property.Value.TryGetInt64(out var propLong))
                                expandoDict.Add(property.Name, propLong);
                            else
                                expandoDict.Add(property.Name, property.Value.GetDecimal());
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            expandoDict.Add(property.Name, property.Value.GetBoolean());
                            break;
                        case JsonValueKind.Null:
                        case JsonValueKind.Undefined:
                        default:
                            expandoDict.Add(property.Name, null);
                            break;
                    }
                }
                return expandoDict as ExpandoObject;
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                var lst = new List<object>();
                foreach (var arrayProp in elem.EnumerateArray())
                    lst.Add(handleElement(arrayProp));

                return lst;
            }
            else if (elem.ValueKind == JsonValueKind.String)
            {
                return elem.GetString();
            }
            else if (elem.ValueKind == JsonValueKind.Number)
            {
                if (elem.TryGetInt32(out var intVal))
                    return intVal;
                if (elem.TryGetInt64(out var longVal))
                    return longVal;
                return elem.GetDecimal();
            }
            else if (elem.ValueKind == JsonValueKind.False || elem.ValueKind == JsonValueKind.True) 
            {
                return elem.GetBoolean();
            }

            return null;
        }

        private JsonNode Node 
        { 
            get 
            {
                return JsonNode.Parse(_value.GetRawText());
            }   
        }

        public bool IsOfType(ValueType type)
        {
            return Type == type;
        }

        public string ToJson()
        {
            return _value.GetRawText();
        }

        public ValueModel Copy()
        {
            return new ValueModel(_value.Clone());
        }

        public IEnumerable<string> GetStrings() 
        {
            if (Type == ValueType.TextArray) 
            {
                foreach (var v in _value.EnumerateArray()) 
                {
                    if (v.ValueKind == JsonValueKind.String)
                        yield return v.GetString();
                    else
                        yield return v.GetRawText();
                }
            }
        }

        public T GetValue<T>(string path = null)
        {
            if (Type == ValueType.Entity)
            {
                if (string.IsNullOrEmpty(path))
                {
                    if (typeof(T) == typeof(string))
                        return (T)(object)_value.GetRawText();

                    return _value.Deserialize<T>(new JsonSerializerOptions()
                    {
                        AllowTrailingCommas = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    });
                }

                //fetch requested path -> return only it
                JsonNode node = findNodePath(Node, path);
                if (node == null)
                    return default(T);

                if (typeof(T) == typeof(string))
                    return (T)(object)node.ToString();

                if (node.GetValueKind() == JsonValueKind.Number)
                    return convertNumber<T>(node.Deserialize<decimal>());

                return node.GetValue<T>();
            }

            if (_value.ValueKind == JsonValueKind.Number)
                return convertNumber<T>(_value.GetDecimal());

            if (typeof(T) == typeof(string))
                return (T)(object)_value.ToString();

            if (typeof(T) == typeof(bool))
                return (T)(object)_value.GetBoolean();

            return _value.Deserialize<T>();
        }

        /// <summary>
        /// Converts a JSON numeric value (held as <see cref="decimal"/> to preserve precision) into the
        /// requested numeric target type <typeparamref name="T"/>. Falls back to <c>default</c> for
        /// unsupported types.
        /// </summary>
        private static T convertNumber<T>(decimal number)
        {
            var target = typeof(T);

            if (target == typeof(int)) return (T)(object)Convert.ToInt32(number);
            if (target == typeof(long)) return (T)(object)Convert.ToInt64(number);
            if (target == typeof(double)) return (T)(object)Convert.ToDouble(number);
            if (target == typeof(decimal)) return (T)(object)number;
            if (target == typeof(float)) return (T)(object)Convert.ToSingle(number);
            if (target == typeof(short)) return (T)(object)Convert.ToInt16(number);
            if (target == typeof(byte)) return (T)(object)Convert.ToByte(number);
            if (target == typeof(string)) return (T)(object)number.ToString(System.Globalization.CultureInfo.InvariantCulture);

            return default(T);
        }

        private JsonNode findNodePath(JsonNode node, string path = null, bool returnParent = false) 
        {
            JsonNode parent = node;
            foreach (var p in pathParts(path))
            {
                parent = node;
                if (p.Contains('[') && p.Contains(']')) //path is targeted to array item
                {
                    var pathArray = p.Split('[');
                    var idxStr = pathArray[1].Remove(pathArray[1].Length - 1, 1); //remove last
                    if (int.TryParse(idxStr, out int idx))
                    {
                        node = node[pathArray[0]];
                        if (node == null)
                            break;

                        node = node.AsArray()[idx];
                        if (node == null)
                            break;
                    }
                }
                else
                {
                    //we must find node with all path parts
                    node = node[p];
                    if (node == null)
                        break;
                }
            }
            return returnParent ? parent:node;
        }

        private JsonNode findNode(JsonNode node, string property)
        {
            if (node.GetValueKind() == JsonValueKind.Object) 
            {
                foreach (var child in node.AsObject().AsEnumerable()) 
                {
                    if (child.Key == property)
                        return child.Value;

                    if (child.Value != null && child.Value.GetValueKind() == JsonValueKind.Object)
                    {
                        var found = findNode(child.Value, property);
                        if (found != null)
                            return found;
                    }
                }
            }
            return null;
        }

        public string GetVariable(string path = null)
        {
            if (Type == ValueType.Text || !String.IsNullOrEmpty(path))
            {
                var val = GetValue<string>(path);
                if (!String.IsNullOrEmpty(val))
                {
                    if (val != "" && val.StartsWith("{") && val.EndsWith("}"))
                        return val.Substring(1, val.Length - 2);
                }
            }
            return null;
        }

        public string CreateMappedObjectJson(ValueModel source, ValueType expectedType) 
        {
            if (Type != ValueType.Entity)
                return null;

            switch (expectedType)
            {
                case ValueType.Text:
                case ValueType.Boolean:
                case ValueType.Number:
                    return findNode(source.Node, _value.EnumerateObject().First().Value.ToString()).ToString();
                case ValueType.TextArray:
                    var a = new JsonArray();
                    foreach (var map in _value.EnumerateObject()) //loop thru mapping
                    {
                        if (source.Type == ValueType.Entity)
                        {
                            if (source.Node.AsObject().TryGetPropertyValue(map.Value.ToString(), out var sourcevalue))
                                a.Add(sourcevalue.DeepClone());
                        }
                        else
                        {
                            a.Add(source.Node.DeepClone());
                        }
                    }
                    return a.ToString();
                case ValueType.Entity:
                    var j = new JsonObject();
                    foreach (var map in _value.EnumerateObject()) //loop thru mapping
                    {
                        if (source.Type == ValueType.Entity)
                        {
                            if (source.Node.AsObject().TryGetPropertyValue(map.Value.ToString(), out var sourcevalue))
                                j.Add(map.Name, sourcevalue.DeepClone());
                        }
                        else
                        {
                            j.Add(map.Name, source.Node.DeepClone());
                        }
                    }
                    return j.ToString();
            }
            return null;
        }

        public IEnumerable<string> GetValuesAsStringList() 
        {
            foreach (var p in _value.EnumerateObject()) 
                yield return p.Value.ToString();
        }

        public ValueModel UpdateOrAddProperty(ValueModel value, string property)
        {
            if (Type == ValueType.Entity)
            {
                if (!string.IsNullOrEmpty(property))
                {
                    var root = Node;
                    var nodeToUpdate = findNode(root, property);
                    if (nodeToUpdate != null)
                    {
                        nodeToUpdate.ReplaceWith(value.Node);
                    }
                    else //create new property 
                    {
                        root.AsObject().Add(property, value.Node);
                    }
                    return new ValueModel(root.ToJsonString());
                }
            }
            return value;
        }

        public ValueModel AppendModelToArray(ValueModel value)
        {
            if (Type == ValueType.TextArray)
            {
                var root = Node;
                root.AsArray().Add(value.Node);
                return new ValueModel(root.ToJsonString());
            }
            return value;
        }

        public ValueModel Update(ValueModel value, string path = null)
        {
            if (Type == ValueType.Entity)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    var root = Node;
                    var node = findNodePath(root, path);
                    if (node != null)
                    {
                        node.ReplaceWith(value.Node);
                    }
                    else //else add new
                    {
                        var nodeToAdd = findNodePath(root, path, true);
                        nodeToAdd.AsObject().Add(pathParts(path).Last(), value.Node);
                    }
                    return new ValueModel(root.ToJsonString());
                }
            }
            return value;
        }

         public ValueModel Merge(ValueModel from, bool mergeOnlyExistingParameters = false)
        {
            if (from.Type != ValueType.Entity || Type != ValueType.Entity)
                return null; //In order to merge, both needs to be Entities

            var mergedValue = new JsonEntity(ToJson());
            var fromValue = new JsonEntity(from.ToJson());

            //if true -> remove parameters that do not exist in target object
            List<string> parametersToRemove = new List<string>();
            if (mergeOnlyExistingParameters) 
            {
                foreach (var fromParameterName in fromValue.GetProperties())
                {
                    if (!mergedValue.ContainsPropertyIgnoreCase(fromParameterName))
                        parametersToRemove.Add(fromParameterName);
                }

                foreach (var k in parametersToRemove)
                    fromValue.Remove(k);
            }

            mergedValue.Merge(fromValue);
            return new ValueModel(mergedValue.ToString());
        }

        /// <summary>
        /// Returns a new <see cref="ValueModel"/> in which every value matching <paramref name="placeholder"/>
        /// has been replaced with <paramref name="value"/>. For entity values the replacement is applied
        /// recursively to all properties; for scalar values the whole value is replaced when it matches.
        /// </summary>
        /// <param name="placeholder">The placeholder token to match (case-insensitive).</param>
        /// <param name="value">The replacement value.</param>
        /// <returns>A new <see cref="ValueModel"/> with replacements applied, or the current instance when nothing matched.</returns>
        public ValueModel ReplaceAllPlaceholdersWithValue(string placeholder, ValueModel value)
        {
            if (Type == ValueType.Entity)
            {
                var jsonEntity = new JsonEntity(ToJson());
                jsonEntity.ReplacePlaceHolderWithValue(placeholder, value.GetAsObject());
                return new ValueModel(jsonEntity.ToString());
            }

            if (string.Equals(_value.ToString(), placeholder, StringComparison.OrdinalIgnoreCase))
                return value.Copy();

            return this;
        }

        private JsonDocument isJson(string source, bool enforceCamelCase = true)
        {
            if (source == null)
                return null;

            try
            {
                var doc = JsonDocument.Parse(source);

                if (enforceCamelCase) 
                {
                    var options = new JsonSerializerOptions
                    {
                        Converters = { new JsonElementConverter() },
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    };

                    var serialized = System.Text.Json.JsonSerializer.Serialize(doc.RootElement, options);
                    doc = JsonDocument.Parse(serialized);
                }
                return doc;
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        private static string[] pathParts(string path) 
        {
            return path.Split('.');
        }

        private JsonSerializerOptions getOptions() 
        {
            return new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
    }

    public class JsonElementConverter : System.Text.Json.Serialization.JsonConverter<JsonElement>
    {
        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    var policy = options.PropertyNamingPolicy;
                    writer.WriteStartObject();
                    foreach (var pair in value.EnumerateObject())
                    {
                        writer.WritePropertyName(policy?.ConvertName(pair.Name) ?? pair.Name);
                        Write(writer, pair.Value, options);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in value.EnumerateArray())
                        Write(writer, item, options);
                    writer.WriteEndArray();
                    break;
                default:
                    value.WriteTo(writer);
                    break;
            }
        }

        public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonDocument.ParseValue(ref reader).RootElement.Clone();
        }
    }
}
