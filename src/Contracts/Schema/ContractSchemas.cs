using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;

namespace RIoT2.Core.Schema
{
    /// <summary>
    /// The JSON Schema (draft 2020-12) of the RIoT2 wire contract, exported from the contract types
    /// with System.Text.Json's <c>JsonSchemaExporter</c> (plan M1 step 5). Every contract type is a
    /// definition under <c>$defs</c>, named after the C# type. The UI generates its TypeScript
    /// models from it; <c>RIoT2.Core/contracts/schemas/</c> holds the committed copy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The schema describes what <see cref="RIoT2JsonOptions.Default"/> writes: camelCase names,
    /// enums as numbers. A member is <c>required</c> when it is always written: not when it is left
    /// out while <c>null</c> (<c>[JsonIgnore(Condition = WhenWritingNull)]</c>), and only when it
    /// can't be <c>null</c> in the types that are also written without nulls (the MQTT messages,
    /// the stored variables and dashboards, and the types nested in them). Reference
    /// types may be <c>null</c> unless they are collection elements. <c>ValueModel</c> and
    /// <c>object</c> members are the <see cref="JsonValueDefinition"/> definition: any JSON value.
    /// </para>
    /// <para>
    /// Objects are not closed (<c>additionalProperties</c> is not set): a reader must accept
    /// members it doesn't know, which is how the contract grows (ADR 0005).
    /// </para>
    /// </remarks>
    public static class ContractSchemas
    {
        /// <summary>The file name of the exported schema.</summary>
        public const string FileName = "riot2-contracts.schema.json";

        /// <summary>The definition used for <c>ValueModel</c> and <c>object</c> members: any JSON value.</summary>
        public const string JsonValueDefinition = "JsonValue";

        /// <summary>
        /// The exported types. Every contract type they contain (templates, dashboard parts, Matter
        /// templates, enums) becomes a definition too.
        /// </summary>
        public static IReadOnlyList<Type> RootTypes { get; } = new[]
        {
            typeof(Report),
            typeof(Command),
            typeof(CommandResult),
            typeof(ConfigurationCommand),
            typeof(NodeOnlineMessage),
            typeof(NodeStatusMessage),
            typeof(NodeDeviceConfiguration),
            typeof(NodeReportTemplate),
            typeof(NodeCommandTemplate),
            typeof(Variable),
            typeof(DashboardConfiguration),
            typeof(DeviceStatus),
            typeof(SecurityInfo)
        };

        // Written without nulls somewhere: the MQTT messages (Json.SerializeIgnoreNulls) and the
        // stored variables and dashboards (the orchestrator's StoredObjectJson), with the types nested
        // in them. A member of these that can be null may be missing.
        private static readonly HashSet<Type> WrittenWithoutNulls = new HashSet<Type>
        {
            typeof(Report), typeof(Command), typeof(ConfigurationCommand), typeof(NodeOnlineMessage),
            typeof(CommandResult), typeof(NodeStatusMessage), typeof(FailedDevice), typeof(PackageManifest),
            typeof(Variable), typeof(DashboardConfiguration), typeof(DashboardPage), typeof(DashboardComponent),
            typeof(DashboardElement), typeof(ReportTemplate), typeof(CommandTemplate)
        };

        /// <summary>Exports the schema of <see cref="RootTypes"/>.</summary>
        public static JsonObject Export()
        {
            var options = RIoT2JsonOptions.CreateForSchema();
            var definitions = new SortedDictionary<string, (Type Type, JsonNode Schema)>(StringComparer.Ordinal);
            var exporterOptions = new JsonSchemaExporterOptions
            {
                TransformSchemaNode = (context, node) => Transform(context, node, definitions)
            };

            foreach (var type in RootTypes)
                options.GetJsonSchemaAsNode(type, exporterOptions);

            var defs = new JsonObject
            {
                [JsonValueDefinition] = new JsonObject
                {
                    ["title"] = JsonValueDefinition,
                    ["description"] = "Any JSON value: a report, command or variable value, or a template's default model."
                }
            };
            foreach (var definition in definitions)
                defs[definition.Key] = definition.Value.Schema;

            return new JsonObject
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                ["$id"] = "https://github.com/Revolutionized-IoT2/RIoT2.Core/blob/main/contracts/schemas/" + FileName,
                ["title"] = "RIoT2 contracts",
                ["description"] = "The RIoT2 MQTT messages and HTTP models, exported from RIoT2.Core.Contracts (ContractSchemas). Generated; do not edit.",
                ["x-contractVersion"] = Contract.Version,
                ["$defs"] = defs
            };
        }

        /// <summary>
        /// The exported schema as indented JSON with <c>\n</c> line ends and a final line end, as it
        /// is committed.
        /// </summary>
        public static string ExportJson()
        {
            var json = Export().ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = NewtonsoftJsonEncoder.Instance });
            return json.Replace("\r\n", "\n") + "\n";
        }

        private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode node,
            IDictionary<string, (Type Type, JsonNode Schema)> definitions)
        {
            var type = context.TypeInfo.Type;
            var property = context.PropertyInfo;
            var nullable = property != null && CanBeNull(property.PropertyType);

            if (IsAnyValue(type))
                return Ref(JsonValueDefinition);

            if (type == typeof(ExpandoObject))
                node = new JsonObject { ["type"] = "object", ["additionalProperties"] = Ref(JsonValueDefinition) };
            else if (type.IsEnum && node is JsonObject enumSchema)
                AddEnumValues(type, enumSchema);
            else if (context.TypeInfo.Kind == JsonTypeInfoKind.Object && node is JsonObject objectSchema)
                SetRequired(context.TypeInfo, objectSchema);

            if (!IsContractType(type))
                return nullable ? AllowNull(node) : WithoutNull(node);

            var definition = WithoutNull(node);
            if (definition is JsonObject definitionObject)
            {
                var titled = new JsonObject { ["title"] = type.Name };
                foreach (var member in definitionObject.ToList())
                {
                    definitionObject.Remove(member.Key);
                    if (member.Key != "title")
                        titled[member.Key] = member.Value;
                }
                definition = titled;
            }

            if (definitions.TryGetValue(type.Name, out var existing))
            {
                if (existing.Type != type)
                    throw new InvalidOperationException($"Two contract types are named {type.Name}: {existing.Type.FullName} and {type.FullName}.");
            }
            else
                definitions[type.Name] = (type, definition);

            var reference = Ref(type.Name);
            return nullable ? new JsonObject { ["anyOf"] = new JsonArray(reference, new JsonObject { ["type"] = "null" }) } : reference;
        }

        private static bool IsAnyValue(Type type) =>
            type == typeof(object) || type == typeof(ValueModel)
            || typeof(JsonNode).IsAssignableFrom(type)
            || typeof(Newtonsoft.Json.Linq.JToken).IsAssignableFrom(type)
            || (type.IsInterface && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type));

        private static bool IsContractType(Type type) =>
            type.Namespace != null && type.Namespace.StartsWith("RIoT2.Core", StringComparison.Ordinal)
            && !type.IsGenericType && (type.IsEnum || (type.IsClass && !type.IsAbstract));

        private static bool CanBeNull(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

        private static JsonObject Ref(string definition) => new JsonObject { ["$ref"] = "#/$defs/" + definition };

        private static void AddEnumValues(Type type, JsonObject schema)
        {
            var values = new JsonArray();
            var names = new JsonArray();
            foreach (var value in Enum.GetValues(type))
            {
                values.Add(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                names.Add(Enum.GetName(type, value));
            }
            schema["enum"] = values;
            schema["x-enumNames"] = names;
        }

        private static void SetRequired(JsonTypeInfo typeInfo, JsonObject schema)
        {
            var required = new JsonArray();
            foreach (var property in typeInfo.Properties.OrderBy(p => p.Order))
                if (IsAlwaysWritten(typeInfo.Type, property))
                    required.Add(property.Name);

            schema.Remove("required");
            if (required.Count > 0)
                schema["required"] = required;
        }

        private static bool IsAlwaysWritten(Type declaringType, JsonPropertyInfo property)
        {
            if (property.Get == null || property.ShouldSerialize != null || property.IsExtensionData)
                return false;
            if (property.AttributeProvider is MemberInfo member
                && member.GetCustomAttribute<JsonIgnoreAttribute>(true) is JsonIgnoreAttribute ignore
                && ignore.Condition != JsonIgnoreCondition.Never)
                return false;
            return !(WrittenWithoutNulls.Contains(declaringType) && CanBeNull(property.PropertyType));
        }

        private static JsonNode AllowNull(JsonNode node)
        {
            if (node is JsonObject schema && schema["type"] is JsonValue single && single.TryGetValue(out string? name) && name != "null")
                schema["type"] = new JsonArray(name, "null");
            return node;
        }

        private static JsonNode WithoutNull(JsonNode node)
        {
            if (node is JsonObject schema && schema["type"] is JsonArray types)
            {
                var remaining = types.Select(t => t!.GetValue<string>()).Where(t => t != "null").ToList();
                schema["type"] = remaining.Count == 1 ? (JsonNode)remaining[0] : new JsonArray(remaining.Select(t => (JsonNode)t).ToArray());
            }
            return node;
        }
    }
}
