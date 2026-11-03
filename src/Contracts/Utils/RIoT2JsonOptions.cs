using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// The one System.Text.Json setup for RIoT2 JSON (plan M2 step 2, I25): what <see cref="Json"/>
    /// writes and reads on MQTT, over HTTP and in configuration files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writing: camelCase property names and dictionary keys, nulls kept (or left out with
    /// <see cref="IgnoreNulls"/>), enums as numbers, public fields included, and numbers, dates and
    /// non-ASCII text exactly as Newtonsoft.Json wrote them. The configuration hashes of design 7.2
    /// are computed over this output, so a change here changes every hash.
    /// </para>
    /// <para>
    /// Reading: property names are case-insensitive, and a number or boolean is accepted where a
    /// string is expected (and the reverse), as Newtonsoft accepted it. Since Core 2.0.0 (I45)
    /// Newtonsoft's attributes are no longer read: use the System.Text.Json ones
    /// (<c>[JsonPropertyName]</c>, <c>[JsonIgnore]</c>, <c>[JsonInclude]</c>).
    /// </para>
    /// </remarks>
    public static class RIoT2JsonOptions
    {
        /// <summary>camelCase, nulls written.</summary>
        public static JsonSerializerOptions Default { get; } = ReadOnly(Create());

        /// <summary>camelCase, nulls left out.</summary>
        public static JsonSerializerOptions IgnoreNulls { get; } = ReadOnly(Create(ignoreNulls: true));

        /// <summary>The member names as declared (Newtonsoft's default naming), nulls written.</summary>
        internal static JsonSerializerOptions AsDeclared { get; } = ReadOnly(Create(camelCase: false));

        /// <summary>Indented JSON for the text helpers that returned Newtonsoft's indented output.</summary>
        internal static JsonSerializerOptions Indented { get; } = ReadOnly(Create(indented: true));

        /// <summary>
        /// Creates options with the RIoT2 converters. The shared instances above are read-only; use
        /// this to start from the same settings and change them.
        /// </summary>
        public static JsonSerializerOptions Create(bool ignoreNulls = false, bool camelCase = true, bool indented = false)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = camelCase ? JsonNamingPolicy.CamelCase : null,
                DictionaryKeyPolicy = camelCase ? JsonNamingPolicy.CamelCase : null,
                DefaultIgnoreCondition = ignoreNulls ? JsonIgnoreCondition.WhenWritingNull : JsonIgnoreCondition.Never,
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                // Newtonsoft escaped only quotes, backslashes and control characters.
                // Newtonsoft's escaping, so emoji keep their bytes in stored files and hashes.
                Encoder = RIoT2JsonEncoder.Instance,
                WriteIndented = indented,
                // The source-generated contract types first (ADR 0015), reflection for everything else.
                TypeInfoResolver = JsonTypeInfoResolver.Combine(RIoT2JsonContext.Default, new DefaultJsonTypeInfoResolver())
                    .WithAddedModifier(ApplyMemberOrder)
            };

            options.Converters.Add(new EmptyStringAsNullConverterFactory());
            options.Converters.Add(new ValueModelJsonConverter());
            options.Converters.Add(new LooseObjectConverter());
            options.Converters.Add(new ExpandoObjectJsonConverter());
            options.Converters.Add(new LenientStringConverter());
            options.Converters.Add(new LenientBooleanConverter());
            options.Converters.Add(new RIoT2DoubleConverter());
            options.Converters.Add(new RIoT2SingleConverter());
            options.Converters.Add(new RIoT2DecimalConverter());
            options.Converters.Add(new LenientDateTimeConverter());
            options.Converters.Add(new LenientDateTimeOffsetConverter());
            options.Converters.Add(new LenientEnumConverterFactory());
            options.Converters.Add(new RuntimeTypeConverterFactory());
            return options;
        }

        /// <summary>
        /// <see cref="Default"/>'s contract (names, order, ignored members) without the lenient
        /// converters for strings, booleans, numbers, dates, enums and nullables, so that the schema
        /// exporter sees their types (<see cref="Schema.ContractSchemas"/>). Not for serializing.
        /// </summary>
        internal static JsonSerializerOptions CreateForSchema()
        {
            var options = Create();
            options.NumberHandling = JsonNumberHandling.Strict;
            for (var i = options.Converters.Count - 1; i >= 0; i--)
            {
                var converter = options.Converters[i];
                if (converter is EmptyStringAsNullConverterFactory || converter is LenientStringConverter
                    || converter is LenientBooleanConverter || converter is RIoT2DoubleConverter
                    || converter is RIoT2SingleConverter || converter is RIoT2DecimalConverter
                    || converter is LenientDateTimeConverter || converter is LenientDateTimeOffsetConverter
                    || converter is LenientEnumConverterFactory)
                    options.Converters.RemoveAt(i);
            }
            return ReadOnly(options);
        }

        private static JsonSerializerOptions ReadOnly(JsonSerializerOptions options)
        {
            options.MakeReadOnly();
            return options;
        }

        // The member order Newtonsoft used, unless the type orders its members itself. Kept after
        // Newtonsoft was removed (I45): the configuration hashes of design 7.2 depend on the order.
        private static void ApplyMemberOrder(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object || typeInfo.Properties.Count < 2 || typeInfo.Properties.Any(p => p.Order != 0))
                return;

            var order = NewtonsoftMemberOrder(typeInfo.Type);
            foreach (var property in typeInfo.Properties)
                property.Order = property.AttributeProvider is MemberInfo member && order.TryGetValue((member.DeclaringType, member.Name), out var rank)
                    ? rank
                    : order.Count;
        }

        // Newtonsoft (ReflectionUtils.GetFieldsAndProperties) listed fields before properties, each as
        // reflection returns them for the type, followed by base types' private ones.
        private static Dictionary<(Type, string), int> NewtonsoftMemberOrder(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var members = new List<MemberInfo>(type.GetFields(flags));
            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
                members.AddRange(baseType.GetFields(flags).Where(f => f.IsPrivate));
            members.AddRange(type.GetProperties(flags));
            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
                members.AddRange(baseType.GetProperties(flags).Where(p => p.GetAccessors(true).All(a => a.IsPrivate)));

            var order = new Dictionary<(Type, string), int>();
            foreach (var member in members)
                if (!order.ContainsKey((member.DeclaringType, member.Name)))
                    order[(member.DeclaringType, member.Name)] = order.Count;
            return order;
        }
    }
}
