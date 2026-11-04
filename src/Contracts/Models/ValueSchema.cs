using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RIoT2.Core.Models
{
    /// <summary>
    /// Describes the value of a report or command template (Core 2.2.0): a JSON-Schema-compatible subset with
    /// the same keywords, plus <see cref="Unit"/> and <see cref="Semantic"/>. Every member is optional and omitted
    /// when <c>null</c>. Declared on <see cref="ReportTemplate.Schema"/> and <see cref="CommandTemplate.Schema"/>.
    /// </summary>
    /// <remarks>
    /// Dictionary keys are written camelCase, like every RIoT2 dictionary: use camelCase member names in
    /// <see cref="Properties"/> (and in entity values) so they match.
    /// </remarks>
    public class ValueSchema
    {
        /// <summary>
        /// The JSON Schema type: <c>boolean</c>, <c>number</c>, <c>integer</c>, <c>string</c>, <c>object</c> (an
        /// entity) or <c>array</c>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; }

        /// <summary>A short label for the value.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Title { get; set; }

        /// <summary>Help text for the value.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        /// <summary>The unit, for example <c>°C</c>, <c>%</c>, <c>W</c>, <c>kWh</c> or <c>lx</c>.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Unit { get; set; }

        /// <summary>
        /// What the value means, an open set (constants in <see cref="ValueSemantics"/>): <c>temperature</c>,
        /// <c>humidity</c>, <c>pressure</c>, <c>illuminance</c>, <c>power</c>, <c>energy</c>, <c>voltage</c>,
        /// <c>current</c>, <c>battery</c>, <c>co2</c>, <c>motion</c>, <c>occupancy</c>, <c>contact</c>, <c>door</c>,
        /// <c>window</c>, <c>light</c>, <c>switch</c>, <c>brightness</c>, <c>color</c>, <c>colorTemperature</c>,
        /// <c>image</c>, <c>text</c> or <c>forecast</c> (a <see cref="ForecastSeries"/>, 2.3.0). Readers ignore a meaning they
        /// don't know.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Semantic { get; set; }

        /// <summary>The smallest allowed number (inclusive).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Minimum { get; set; }

        /// <summary>The largest allowed number (inclusive).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Maximum { get; set; }

        /// <summary>The step: a valid number is a multiple of it.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? MultipleOf { get; set; }

        /// <summary>The allowed values.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<object>? Enum { get; set; }

        /// <summary>The members of an entity value (<c>object</c>), by name.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, ValueSchema>? Properties { get; set; }

        /// <summary>The items of an <c>array</c> value.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ValueSchema? Items { get; set; }

        /// <summary>The value can't be changed: the UI shows it, but offers no input for it.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? ReadOnly { get; set; }

        /// <summary>A <c>boolean</c> value, for example <see cref="ValueSemantics.Switch"/> or <see cref="ValueSemantics.Motion"/>.</summary>
        public static ValueSchema Boolean(string? semantic = null) =>
            new ValueSchema { Type = "boolean", Semantic = semantic };

        /// <summary>A <c>number</c> value with an optional unit, meaning and range.</summary>
        public static ValueSchema Number(string? unit = null, string? semantic = null, double? minimum = null, double? maximum = null) =>
            new ValueSchema { Type = "number", Unit = unit, Semantic = semantic, Minimum = minimum, Maximum = maximum };

        /// <summary>An <c>integer</c> value with an optional unit, meaning and range.</summary>
        public static ValueSchema Integer(string? unit = null, string? semantic = null, double? minimum = null, double? maximum = null) =>
            new ValueSchema { Type = "integer", Unit = unit, Semantic = semantic, Minimum = minimum, Maximum = maximum };

        /// <summary>A text (<c>string</c>) value.</summary>
        public static ValueSchema Text(string? semantic = null) =>
            new ValueSchema { Type = "string", Semantic = semantic };

        /// <summary>An entity (<c>object</c>) value with the given members.</summary>
        public static ValueSchema Entity(Dictionary<string, ValueSchema> properties, string? semantic = null) =>
            new ValueSchema { Type = "object", Properties = properties, Semantic = semantic };

        /// <summary>An <c>array</c> value whose items are <paramref name="items"/>.</summary>
        public static ValueSchema ArrayOf(ValueSchema items) =>
            new ValueSchema { Type = "array", Items = items };

        /// <summary>
        /// A <see cref="ForecastSeries"/> value (Core 2.3.0) with the semantic <see cref="ValueSemantics.Forecast"/>; the
        /// points' values are <paramref name="item"/>, a number by default, whose unit becomes the series' unit.
        /// </summary>
        public static ValueSchema Forecast(ValueSchema? item = null)
        {
            var value = item ?? Number();
            return new ValueSchema
            {
                Type = "object",
                Semantic = ValueSemantics.Forecast,
                Unit = value.Unit,
                Title = value.Title,
                Properties = new Dictionary<string, ValueSchema>
                {
                    ["issuedAt"] = Text(),
                    ["kind"] = new ValueSchema { Type = "string", Enum = new List<object> { ForecastSeries.ForecastKind, ForecastSeries.ConfirmedKind } },
                    ["unit"] = Text(),
                    ["resolution"] = Text(),
                    ["points"] = ArrayOf(Entity(new Dictionary<string, ValueSchema>
                    {
                        ["start"] = Text(),
                        ["end"] = Text(),
                        ["value"] = value
                    }))
                }
            };
        }
    }

    /// <summary>Well-known values of <see cref="ValueSchema.Semantic"/> (Core 2.2.0). The set is open.</summary>
    public static class ValueSemantics
    {
        public const string Temperature = "temperature";
        public const string Humidity = "humidity";
        public const string Pressure = "pressure";
        public const string Illuminance = "illuminance";
        public const string Power = "power";
        public const string Energy = "energy";
        public const string Voltage = "voltage";
        public const string Current = "current";
        public const string Battery = "battery";
        public const string Co2 = "co2";
        public const string Motion = "motion";
        public const string Occupancy = "occupancy";
        public const string Contact = "contact";
        public const string Door = "door";
        public const string Window = "window";
        public const string Light = "light";
        public const string Switch = "switch";
        public const string Brightness = "brightness";
        public const string Color = "color";
        public const string ColorTemperature = "colorTemperature";
        public const string Image = "image";
        public const string Text = "text";

        /// <summary>A <see cref="ForecastSeries"/>: values over time ahead (Core 2.3.0).</summary>
        public const string Forecast = "forecast";
    }
}
