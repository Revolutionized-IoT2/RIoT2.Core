using RIoT2.Core.Utils;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Stj = System.Text.Json.Serialization;

namespace RIoT2.Core.Models
{
    /// <summary>
    /// Values over time ahead, such as a weather forecast or day-ahead electricity prices (Core 2.3.0): the value of a
    /// report whose template's schema has the semantic <see cref="ValueSemantics.Forecast"/>
    /// (<see cref="ValueSchema.Forecast"/>). A device publishes the whole series again when it changes; the orchestrator
    /// keeps the latest one and answers window queries (<see cref="ForecastWindow"/>).
    /// </summary>
    public class ForecastSeries
    {
        /// <summary><see cref="Kind"/>: the values are predicted and may change, like a weather forecast.</summary>
        public const string ForecastKind = "forecast";

        /// <summary><see cref="Kind"/>: the values are fixed, like published day-ahead prices.</summary>
        public const string ConfirmedKind = "confirmed";

        /// <summary>The most points a series may have.</summary>
        public const int MaxPoints = 1000;

        /// <summary>When the source made the series.</summary>
        public DateTimeOffset IssuedAt { get; set; }

        /// <summary><see cref="ForecastKind"/> or <see cref="ConfirmedKind"/>.</summary>
        public string Kind { get; set; } = ForecastKind;

        /// <summary>The unit of the values, for example <c>°C</c> or <c>c/kWh</c>.</summary>
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string? Unit { get; set; }

        /// <summary>The usual length of a point as an ISO 8601 duration, for example <c>PT1H</c> or <c>PT15M</c>.</summary>
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string? Resolution { get; set; }

        /// <summary>The values, in time order, without overlaps.</summary>
        public List<ForecastPoint> Points { get; set; } = new List<ForecastPoint>();

        /// <summary>
        /// Reads a series from a report's value: an object with <c>issuedAt</c> and a <c>points</c> array. <c>null</c> when the
        /// value doesn't have that shape or can't be read; call <see cref="Validate"/> on the result.
        /// </summary>
        public static ForecastSeries? From(ValueModel? value)
        {
            if (value == null || value.Type != ValueType.Entity)
                return null;
            return Create(value.ToJson());
        }

        /// <summary>Reads a series from JSON; <c>null</c> when it isn't one.</summary>
        public static ForecastSeries? Create(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                using (var document = JsonDocument.Parse(json!))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object
                        || !root.TryGetProperty("issuedAt", out _)
                        || !root.TryGetProperty("points", out var points) || points.ValueKind != JsonValueKind.Array)
                        return null;
                }
                return Json.Deserialize<ForecastSeries>(json);
            }
            catch (Exception x) when (x is JsonException || x is FormatException || x is InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>The first problem with the series, or <c>null</c> when it is valid.</summary>
        public string? Validate()
        {
            if (Kind != ForecastKind && Kind != ConfirmedKind)
                return $"The kind must be '{ForecastKind}' or '{ConfirmedKind}'.";
            if (Points == null)
                return "The points are missing.";
            if (Points.Count > MaxPoints)
                return $"A series has at most {MaxPoints} points.";
            DateTimeOffset? previousEnd = null;
            for (var i = 0; i < Points.Count; i++)
            {
                var point = Points[i];
                if (point == null)
                    return $"Point {i} is missing.";
                if (point.End <= point.Start)
                    return $"Point {i} ends before it starts.";
                if (previousEnd != null && point.Start < previousEnd.Value)
                    return $"Point {i} starts before the previous point ends; the points must be in time order without overlaps.";
                if (point.Value is double number && (double.IsNaN(number) || double.IsInfinity(number)))
                    return $"Point {i} is not a number.";
                previousEnd = point.End;
            }
            return null;
        }

        /// <summary>The series as a report value.</summary>
        public ValueModel ToValue() => new ValueModel(Json.Serialize(this));
    }

    /// <summary>One value of a <see cref="ForecastSeries"/>, valid from <see cref="Start"/> until <see cref="End"/>.</summary>
    public class ForecastPoint
    {
        public ForecastPoint() { }

        public ForecastPoint(DateTimeOffset start, DateTimeOffset end, double? value)
        {
            Start = start;
            End = end;
            Value = value;
        }

        /// <summary>When the value starts to apply (inclusive).</summary>
        public DateTimeOffset Start { get; set; }

        /// <summary>When the value stops applying (exclusive).</summary>
        public DateTimeOffset End { get; set; }

        /// <summary>The value; <c>null</c> when the source has none for this time.</summary>
        public double? Value { get; set; }
    }
}
