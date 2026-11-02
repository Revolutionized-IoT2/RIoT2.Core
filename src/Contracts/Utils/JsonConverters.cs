using RIoT2.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// The System.Text.Json converters behind RIoT2JsonOptions (plan M2, I25). Together they keep the
// wire format that Newtonsoft.Json wrote, byte for byte: the golden files in contracts/golden and
// the configuration hashes of design 7.2 depend on it. They also accept the input Newtonsoft
// accepted, because device plugins read third-party APIs through Json.Deserialize.
namespace RIoT2.Core.Utils
{
    /// <summary>
    /// Reads and writes <see cref="ValueModel"/> as its JSON value, with Newtonsoft's reading rules:
    /// objects and arrays get camelCase property names, integers stay integers, other numbers become
    /// doubles, and a string stays a string even when it contains JSON.
    /// </summary>
    public sealed class ValueModelJsonConverter : JsonConverter<ValueModel>
    {
        public override ValueModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    using (var document = JsonDocument.ParseValue(ref reader))
                        return new ValueModel(document.RootElement.GetRawText());
                case JsonTokenType.True:
                case JsonTokenType.False:
                    return new ValueModel(reader.GetBoolean());
                case JsonTokenType.Number:
                    var number = JsonTokens.RawText(ref reader);
                    return JsonTokens.IsInteger(number)
                        ? new ValueModel(number)
                        : new ValueModel(reader.GetDouble());
                case JsonTokenType.String:
                    return new ValueModel((object?)reader.GetString());
                default:
                    throw new JsonException($"Unexpected token {reader.TokenType} for a value.");
            }
        }

        public override void Write(Utf8JsonWriter writer, ValueModel value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(value.ToJson(), skipInputValidation: true);
        }
    }

    /// <summary>
    /// Reads a JSON value declared as <see cref="object"/> the way Newtonsoft did: integers as
    /// <see cref="long"/>, other numbers as <see cref="double"/>, and booleans and strings as
    /// themselves. Objects and arrays become a <see cref="JsonElement"/> (Newtonsoft made a JObject or
    /// JArray); both print their JSON from <c>ToString()</c>. Writes the value's runtime type.
    /// </summary>
    public sealed class LooseObjectConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return reader.TryGetInt64(out var integer) ? integer : (object)reader.GetDouble();
                case JsonTokenType.Null:
                    return null;
                default:
                    using (var document = JsonDocument.ParseValue(ref reader))
                        return document.RootElement.Clone();
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            var type = value.GetType();
            if (type == typeof(object))
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
                return;
            }
            JsonSerializer.Serialize(writer, value, type, options);
        }
    }

    /// <summary>
    /// Reads an <see cref="ExpandoObject"/> as Newtonsoft's ExpandoObjectConverter did: nested objects are
    /// ExpandoObjects, arrays are <c>List&lt;object&gt;</c>, integers are longs and other numbers doubles.
    /// Unlike Newtonsoft, a string that looks like a date stays a string.
    /// </summary>
    public sealed class ExpandoObjectJsonConverter : JsonConverter<ExpandoObject>
    {
        public override ExpandoObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Expected an object for an ExpandoObject, not {reader.TokenType}.");
            using (var document = JsonDocument.ParseValue(ref reader))
                return (ExpandoObject)ToValue(document.RootElement)!;
        }

        public override void Write(Utf8JsonWriter writer, ExpandoObject value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, (IDictionary<string, object>)value, options);

        private static object? ToValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    IDictionary<string, object?> expando = new ExpandoObject();
                    foreach (var property in element.EnumerateObject())
                        expando[property.Name] = ToValue(property.Value);
                    return expando;
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                        list.Add(ToValue(item));
                    return list;
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
    }

    /// <summary>Accepts a number or a boolean (as its JSON text) where a string is expected, as Newtonsoft did.</summary>
    public sealed class LenientStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return JsonTokens.RawText(ref reader);
                case JsonTokenType.True:
                    return "true";
                case JsonTokenType.False:
                    return "false";
                default:
                    throw new JsonException($"Unexpected token {reader.TokenType} for a string.");
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    /// <summary>Accepts "true"/"false" strings and numbers (non-zero is true), as Newtonsoft did.</summary>
    public sealed class LenientBooleanConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.String when bool.TryParse(reader.GetString()?.Trim(), out var parsed):
                    return parsed;
                case JsonTokenType.Number:
                    return reader.GetDouble() != 0;
                default:
                    throw new JsonException($"Could not read {reader.TokenType} as a boolean.");
            }
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }

    /// <summary>
    /// Writes a double as Newtonsoft did (round-trip digits, always with a decimal point or an
    /// exponent: <c>21.0</c>, not <c>21</c>; NaN and infinities as strings) and reads numbers or
    /// numeric strings.
    /// </summary>
    public sealed class NewtonsoftDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return JsonTokens.ParseDouble(reader.GetString());
            return reader.GetDouble();
        }

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            else
                writer.WriteRawValue(JsonTokens.EnsureDecimalPlace(value.ToString("R", CultureInfo.InvariantCulture)), skipInputValidation: true);
        }
    }

    /// <summary>The <see cref="float"/> counterpart of <see cref="NewtonsoftDoubleConverter"/>.</summary>
    public sealed class NewtonsoftSingleConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return (float)JsonTokens.ParseDouble(reader.GetString());
            return reader.GetSingle();
        }

        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            else
                writer.WriteRawValue(JsonTokens.EnsureDecimalPlace(value.ToString("R", CultureInfo.InvariantCulture)), skipInputValidation: true);
        }
    }

    /// <summary>Writes a decimal as Newtonsoft did (<c>1.0</c>, not <c>1</c>) and reads numbers or numeric strings.</summary>
    public sealed class NewtonsoftDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
                throw new JsonException($"'{text}' is not a number.");
            }
            return reader.GetDecimal();
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(JsonTokens.EnsureDecimalPlace(value.ToString(CultureInfo.InvariantCulture)), skipInputValidation: true);
        }
    }

    /// <summary>
    /// Writes dates in Newtonsoft's ISO 8601 format and reads ISO 8601 or any invariant-culture date
    /// text, as Newtonsoft did.
    /// </summary>
    public sealed class LenientDateTimeConverter : JsonConverter<DateTime>
    {
        internal const string Format = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && !reader.TryGetDateTime(out _))
            {
                var text = reader.GetString();
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                    return parsed;
                throw new JsonException($"'{text}' is not a date.");
            }
            return reader.GetDateTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>The <see cref="DateTimeOffset"/> counterpart of <see cref="LenientDateTimeConverter"/>.</summary>
    public sealed class LenientDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && !reader.TryGetDateTimeOffset(out _))
            {
                var text = reader.GetString();
                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                    return parsed;
                throw new JsonException($"'{text}' is not a date.");
            }
            return reader.GetDateTimeOffset();
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(LenientDateTimeConverter.Format, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes enums as numbers, as Newtonsoft did, and reads numbers, numeric strings or names
    /// (case-insensitive).
    /// </summary>
    public sealed class LenientEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(EnumConverter<>).MakeGenericType(typeToConvert));

        private sealed class EnumConverter<T> : JsonConverter<T> where T : struct
        {
            private static readonly bool _unsigned = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(T))) == TypeCode.UInt64;

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        return _unsigned
                            ? (T)Enum.ToObject(typeof(T), reader.GetUInt64())
                            : (T)Enum.ToObject(typeof(T), reader.GetInt64());
                    case JsonTokenType.String:
                        var text = reader.GetString();
                        try
                        {
                            return (T)Enum.Parse(typeof(T), text, ignoreCase: true);
                        }
                        catch (ArgumentException x)
                        {
                            throw new JsonException($"'{text}' is not a {typeof(T).Name}.", x);
                        }
                    default:
                        throw new JsonException($"Unexpected token {reader.TokenType} for {typeof(T).Name}.");
                }
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                if (_unsigned)
                    writer.WriteNumberValue(Convert.ToUInt64(value, CultureInfo.InvariantCulture));
                else
                    writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }

            // A dictionary key is the enum's name, with the dictionary key policy, as Newtonsoft wrote it.
            public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                var name = value.ToString();
                writer.WritePropertyName(options.DictionaryKeyPolicy?.ConvertName(name) ?? name);
            }

            public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var text = reader.GetString();
                try
                {
                    return (T)Enum.Parse(typeof(T), text, ignoreCase: true);
                }
                catch (ArgumentException x)
                {
                    throw new JsonException($"'{text}' is not a {typeof(T).Name}.", x);
                }
            }
        }
    }

    /// <summary>
    /// Writes a member declared as an interface or abstract class with its runtime type, as
    /// Newtonsoft did (System.Text.Json writes only the declared type's members). Reading one throws,
    /// as it did with Newtonsoft.
    /// </summary>
    public sealed class RuntimeTypeConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            (typeToConvert.IsInterface || typeToConvert.IsAbstract)
            && !typeToConvert.IsValueType
            && !typeof(IEnumerable).IsAssignableFrom(typeToConvert)
            && !typeof(Newtonsoft.Json.Linq.JToken).IsAssignableFrom(typeToConvert)
            && !typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(typeToConvert)
            && !typeToConvert.IsDefined(typeof(JsonPolymorphicAttribute), false)
            && !typeToConvert.IsDefined(typeof(JsonDerivedTypeAttribute), false)
            && typeToConvert != typeof(Type);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(RuntimeTypeConverter<>).MakeGenericType(typeToConvert));

        private sealed class RuntimeTypeConverter<T> : JsonConverter<T> where T : class
        {
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                throw new NotSupportedException($"{typeof(T).Name} is an interface or abstract class; deserialize a concrete type instead.");

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }

    /// <summary>
    /// Writes a Newtonsoft <c>JToken</c> (still produced by the stored-configuration loader until
    /// I26) as the JSON Newtonsoft wrote for it, and reads one.
    /// </summary>
    public sealed class NewtonsoftTokenConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeof(Newtonsoft.Json.Linq.JToken).IsAssignableFrom(typeToConvert);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(TokenConverter<>).MakeGenericType(typeToConvert));

        private sealed class TokenConverter<T> : JsonConverter<T> where T : Newtonsoft.Json.Linq.JToken
        {
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using (var document = JsonDocument.ParseValue(ref reader))
                    return (T)Newtonsoft.Json.Linq.JToken.Parse(document.RootElement.GetRawText());
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                writer.WriteRawValue(value.ToString(Newtonsoft.Json.Formatting.None), skipInputValidation: true);
            }
        }
    }

    /// <summary>
    /// Reads an empty or white-space string as null for a nullable value (<c>double?</c>,
    /// <c>DateTime?</c>, <c>bool?</c>, ...), as Newtonsoft did; anything else goes to the value's own
    /// converter.
    /// </summary>
    public sealed class EmptyStringAsNullConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Nullable<>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(NullableConverter<>).MakeGenericType(Nullable.GetUnderlyingType(typeToConvert)));

        private sealed class NullableConverter<T> : JsonConverter<T?> where T : struct
        {
            // JSON null is null without calling the converter.
            public override bool HandleNull => false;

            public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String && string.IsNullOrWhiteSpace(reader.GetString()))
                    return null;
                // Through the serializer, so options such as AllowReadingFromString apply to built-in types too.
                return JsonSerializer.Deserialize<T>(ref reader, options);
            }

            public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
            {
                if (value.HasValue)
                    JsonSerializer.Serialize(writer, value.Value, options);
                else
                    writer.WriteNullValue();
            }
        }
    }

    internal static class JsonTokens
    {
        /// <summary>The token's text as it appears in the JSON.</summary>
        public static string RawText(ref Utf8JsonReader reader) =>
            Encoding.UTF8.GetString(reader.HasValueSequence ? ToArray(reader.ValueSequence) : reader.ValueSpan.ToArray());

        private static byte[] ToArray(System.Buffers.ReadOnlySequence<byte> sequence)
        {
            var bytes = new byte[sequence.Length];
            var offset = 0;
            foreach (var segment in sequence)
            {
                segment.Span.CopyTo(new Span<byte>(bytes, offset, segment.Length));
                offset += segment.Length;
            }
            return bytes;
        }

        /// <summary>Whether a JSON number is an integer token (no fraction or exponent), as Newtonsoft classified it.</summary>
        public static bool IsInteger(string number) => number.IndexOfAny(new[] { '.', 'e', 'E' }) < 0;

        /// <summary>Newtonsoft's <c>EnsureDecimalPlace</c>: "21" becomes "21.0"; "21.5" and "1E+20" stay.</summary>
        public static string EnsureDecimalPlace(string text) =>
            text.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0 ? text : text + ".0";

        public static double ParseDouble(string? text)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;
            throw new JsonException($"'{text}' is not a number.");
        }
    }
}
