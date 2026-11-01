using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
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
    /// string is expected (and the reverse), as Newtonsoft accepted it. Newtonsoft's
    /// <c>[JsonProperty]</c> names and null handling and <c>[JsonIgnore]</c> are honoured, so device
    /// plugin types written for Newtonsoft keep their format.
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
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = indented,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { typeInfo => ApplyNewtonsoftConventions(typeInfo, camelCase) } }
            };

            options.Converters.Add(new EmptyStringAsNullConverterFactory());
            options.Converters.Add(new NewtonsoftTokenConverterFactory());
            options.Converters.Add(new ValueModelJsonConverter());
            options.Converters.Add(new LooseObjectConverter());
            options.Converters.Add(new ExpandoObjectJsonConverter());
            options.Converters.Add(new LenientStringConverter());
            options.Converters.Add(new LenientBooleanConverter());
            options.Converters.Add(new NewtonsoftDoubleConverter());
            options.Converters.Add(new NewtonsoftSingleConverter());
            options.Converters.Add(new NewtonsoftDecimalConverter());
            options.Converters.Add(new LenientDateTimeConverter());
            options.Converters.Add(new LenientDateTimeOffsetConverter());
            options.Converters.Add(new LenientEnumConverterFactory());
            options.Converters.Add(new RuntimeTypeConverterFactory());
            return options;
        }

        private static JsonSerializerOptions ReadOnly(JsonSerializerOptions options)
        {
            options.MakeReadOnly();
            return options;
        }

        // What Newtonsoft did and System.Text.Json doesn't, for the types that still rely on it:
        // [JsonIgnore], [JsonProperty] names (camel-cased like every name, as Newtonsoft's camelCase
        // resolver did), [JsonProperty(NullValueHandling = Ignore)], non-public [JsonProperty]
        // members, and public fields before properties. Removed with Newtonsoft (plan M2 step 6, I45).
        private static void ApplyNewtonsoftConventions(JsonTypeInfo typeInfo, bool camelCase)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
                return;

            for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                var property = typeInfo.Properties[i];
                if (!(property.AttributeProvider is MemberInfo member))
                    continue;

                if (member.IsDefined(typeof(Newtonsoft.Json.JsonIgnoreAttribute), true))
                {
                    typeInfo.Properties.RemoveAt(i);
                    continue;
                }

                var attribute = NewtonsoftProperty(member);
                if (attribute == null)
                    continue;

                if (!string.IsNullOrEmpty(attribute.PropertyName) && !member.IsDefined(typeof(JsonPropertyNameAttribute), true))
                    property.Name = Name(attribute.PropertyName, camelCase);

                if (attribute.NullValueHandling == Newtonsoft.Json.NullValueHandling.Ignore && property.ShouldSerialize == null)
                    property.ShouldSerialize = (_, value) => value != null;
            }

            AddNonPublicMembers(typeInfo, camelCase);

            // Newtonsoft's member order, unless the type orders its members itself.
            if (typeInfo.Properties.Count > 1 && typeInfo.Properties.All(p => p.Order == 0))
            {
                var order = NewtonsoftMemberOrder(typeInfo.Type);
                foreach (var property in typeInfo.Properties)
                    property.Order = property.AttributeProvider is MemberInfo member && order.TryGetValue((member.DeclaringType, member.Name), out var rank)
                        ? rank
                        : order.Count;
            }
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

        private static void AddNonPublicMembers(JsonTypeInfo typeInfo, bool camelCase)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var members = typeInfo.Type.GetFields(flags).Cast<MemberInfo>().Concat(typeInfo.Type.GetProperties(flags));
            foreach (var member in members)
            {
                var attribute = NewtonsoftProperty(member);
                if (attribute == null || member.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    continue;

                var name = Name(string.IsNullOrEmpty(attribute.PropertyName) ? member.Name : attribute.PropertyName, camelCase);
                if (typeInfo.Properties.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                JsonPropertyInfo property;
                if (member is FieldInfo field)
                {
                    property = typeInfo.CreateJsonPropertyInfo(field.FieldType, name);
                    property.Get = field.GetValue;
                    if (!field.IsInitOnly)
                        property.Set = field.SetValue;
                    property.AttributeProvider = field;
                }
                else
                {
                    var accessor = (PropertyInfo)member;
                    if (accessor.GetIndexParameters().Length > 0 || accessor.GetGetMethod(true) == null)
                        continue;
                    property = typeInfo.CreateJsonPropertyInfo(accessor.PropertyType, name);
                    property.Get = accessor.GetValue;
                    if (accessor.GetSetMethod(true) != null)
                        property.Set = accessor.SetValue;
                    property.AttributeProvider = accessor;
                }
                if (attribute.NullValueHandling == Newtonsoft.Json.NullValueHandling.Ignore)
                    property.ShouldSerialize = (_, value) => value != null;
                typeInfo.Properties.Add(property);
            }
        }

        private static Newtonsoft.Json.JsonPropertyAttribute NewtonsoftProperty(MemberInfo member) =>
            (Newtonsoft.Json.JsonPropertyAttribute)Attribute.GetCustomAttribute(member, typeof(Newtonsoft.Json.JsonPropertyAttribute), true);

        private static string Name(string name, bool camelCase) =>
            camelCase ? JsonNamingPolicy.CamelCase.ConvertName(name) : name;
    }
}
