using System;
using System.Collections.Generic;
using System.Linq;
using Stj = System.Text.Json.Serialization;

namespace RIoT2.Core.Models
{
    /// <summary>The windows a forecast query takes (<see cref="ForecastWindow.TryResolve"/>, Core 2.3.0).</summary>
    public static class ForecastWindows
    {
        /// <summary>The whole series.</summary>
        public const string All = "all";

        /// <summary>From local midnight today until local midnight tomorrow.</summary>
        public const string Today = "today";

        /// <summary>From local midnight tomorrow until the midnight after.</summary>
        public const string Tomorrow = "tomorrow";

        /// <summary>From now for the given hours (24 by default).</summary>
        public const string Next = "next";

        /// <summary>From a time until a time.</summary>
        public const string Range = "range";

        public static readonly IReadOnlyList<string> Names = new[] { All, Today, Tomorrow, Next, Range };
    }

    /// <summary>A part of a <see cref="ForecastSeries"/> and its figures (<see cref="ForecastWindow.Summarize"/>, Core 2.3.0).</summary>
    public class ForecastSummary
    {
        /// <summary>The report id the series belongs to.</summary>
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }

        /// <summary>When the source made the series.</summary>
        public DateTimeOffset IssuedAt { get; set; }

        /// <summary><see cref="ForecastSeries.ForecastKind"/> or <see cref="ForecastSeries.ConfirmedKind"/>.</summary>
        public string Kind { get; set; } = ForecastSeries.ForecastKind;

        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string? Unit { get; set; }

        /// <summary>The start of the window (inclusive).</summary>
        public DateTimeOffset From { get; set; }

        /// <summary>The end of the window (exclusive).</summary>
        public DateTimeOffset To { get; set; }

        /// <summary>Whether points with values cover the whole window, without gaps.</summary>
        public bool Complete { get; set; }

        /// <summary>The points with values in the window.</summary>
        public int Count { get; set; }

        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public double? Min { get; set; }

        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public double? Max { get; set; }

        /// <summary>The average weighted by how long each point lies in the window.</summary>
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public double? Average { get; set; }

        /// <summary>The sum of the points' values, for example millimetres of rain from hourly amounts.</summary>
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public double? Sum { get; set; }

        /// <summary>The point that applies now, when it is in the window.</summary>
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public ForecastPoint? Current { get; set; }

        /// <summary>The points that overlap the window, in time order.</summary>
        public List<ForecastPoint> Points { get; set; } = new List<ForecastPoint>();

        /// <summary>The asked number of points with the lowest values, lowest first.</summary>
        public List<ForecastPoint> Lowest { get; set; } = new List<ForecastPoint>();

        /// <summary>The asked number of points with the highest values, highest first.</summary>
        public List<ForecastPoint> Highest { get; set; } = new List<ForecastPoint>();
    }

    /// <summary>Selects a window of a <see cref="ForecastSeries"/> and works out its figures (Core 2.3.0).</summary>
    public static class ForecastWindow
    {
        /// <summary>The longest window, in hours: 16 days.</summary>
        public const int MaxHours = 384;

        /// <summary>The default of <see cref="ForecastWindows.Next"/>.</summary>
        public const int DefaultHours = 24;

        /// <summary>The most points <see cref="ForecastSummary.Lowest"/> and <see cref="ForecastSummary.Highest"/> hold.</summary>
        public const int MaxRank = 100;

        /// <summary>
        /// Works out the window: <c>null</c> bounds for <see cref="ForecastWindows.All"/> (the series' own), local days
        /// in <paramref name="zone"/> for today and tomorrow (23 or 25 hours on a daylight saving day). Returns the problem,
        /// or <c>null</c> when the window is valid.
        /// </summary>
        public static string? TryResolve(string? window, DateTimeOffset now, TimeZoneInfo zone, int? hours, DateTimeOffset? from, DateTimeOffset? to,
            out DateTimeOffset? start, out DateTimeOffset? end)
        {
            if (zone == null)
                throw new ArgumentNullException(nameof(zone));
            start = null;
            end = null;
            switch (string.IsNullOrWhiteSpace(window) ? ForecastWindows.All : window!.Trim().ToLowerInvariant())
            {
                case ForecastWindows.All:
                    return null;
                case ForecastWindows.Today:
                    start = LocalMidnight(now, zone, 0);
                    end = LocalMidnight(now, zone, 1);
                    return null;
                case ForecastWindows.Tomorrow:
                    start = LocalMidnight(now, zone, 1);
                    end = LocalMidnight(now, zone, 2);
                    return null;
                case ForecastWindows.Next:
                    var length = hours ?? DefaultHours;
                    if (length < 1 || length > MaxHours)
                        return $"The hours must be 1-{MaxHours}.";
                    start = now;
                    end = now.AddHours(length);
                    return null;
                case ForecastWindows.Range:
                    if (from == null || to == null)
                        return "A range needs from and to.";
                    if (to.Value <= from.Value)
                        return "The range must end after it starts.";
                    if ((to.Value - from.Value).TotalHours > MaxHours)
                        return $"A range is at most {MaxHours} hours.";
                    start = from;
                    end = to;
                    return null;
                default:
                    return $"The window must be one of {string.Join(", ", ForecastWindows.Names)}.";
            }
        }

        /// <summary>
        /// The part of <paramref name="series"/> between <paramref name="from"/> and <paramref name="to"/> (the series' own bounds
        /// when <c>null</c>), its figures, and the <paramref name="rank"/> lowest and highest points (0 to <see cref="MaxRank"/>).
        /// </summary>
        public static ForecastSummary Summarize(ForecastSeries series, DateTimeOffset? from, DateTimeOffset? to, int rank = 0, DateTimeOffset? now = null)
        {
            if (series == null)
                throw new ArgumentNullException(nameof(series));
            if (rank < 0 || rank > MaxRank)
                throw new ArgumentOutOfRangeException(nameof(rank), $"The rank must be 0-{MaxRank}.");

            var all = (series.Points ?? new List<ForecastPoint>()).Where(x => x != null).OrderBy(x => x.Start).ToList();
            var start = from ?? (all.Count > 0 ? all[0].Start : series.IssuedAt);
            var end = to ?? (all.Count > 0 ? all.Max(x => x.End) : series.IssuedAt);
            var points = all.Where(x => x.End > start && x.Start < end).ToList();
            var valued = points.Where(x => x.Value != null).ToList();

            var summary = new ForecastSummary
            {
                IssuedAt = series.IssuedAt,
                Kind = series.Kind,
                Unit = series.Unit,
                From = start,
                To = end,
                Count = valued.Count,
                Points = points.Select(Copy).ToList(),
                Complete = Covers(valued, start, end)
            };
            if (valued.Count > 0)
            {
                summary.Min = valued.Min(x => x.Value!.Value);
                summary.Max = valued.Max(x => x.Value!.Value);
                summary.Sum = Round(valued.Sum(x => x.Value!.Value));
                double weight = 0, total = 0;
                foreach (var point in valued)
                {
                    var overlap = (Min(point.End, end) - Max(point.Start, start)).TotalSeconds;
                    weight += overlap;
                    total += overlap * point.Value!.Value;
                }
                summary.Average = weight > 0 ? Round(total / weight) : (double?)null;
                summary.Lowest = valued.OrderBy(x => x.Value).ThenBy(x => x.Start).Take(rank).Select(Copy).ToList();
                summary.Highest = valued.OrderByDescending(x => x.Value).ThenBy(x => x.Start).Take(rank).Select(Copy).ToList();
            }
            if (now != null && now.Value >= start && now.Value < end)
            {
                var current = points.FirstOrDefault(x => x.Start <= now.Value && now.Value < x.End);
                summary.Current = current == null ? null : Copy(current);
            }
            return summary;
        }

        private static bool Covers(List<ForecastPoint> valued, DateTimeOffset start, DateTimeOffset end)
        {
            if (valued.Count == 0 || end <= start)
                return false;
            var reached = start;
            foreach (var point in valued)
            {
                if (point.Start > reached)
                    return false;
                if (point.End > reached)
                    reached = point.End;
                if (reached >= end)
                    return true;
            }
            return reached >= end;
        }

        private static DateTimeOffset LocalMidnight(DateTimeOffset now, TimeZoneInfo zone, int days)
        {
            var local = TimeZoneInfo.ConvertTime(now, zone).Date.AddDays(days);
            // A zone that changes its clocks at midnight skips it: the day then starts at the first valid time.
            while (zone.IsInvalidTime(local))
                local = local.AddMinutes(30);
            var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
            return new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), offset).ToUniversalTime();
        }

        private static ForecastPoint Copy(ForecastPoint point) => new ForecastPoint(point.Start, point.End, point.Value);

        private static double Round(double value) => Math.Round(value, 6);

        private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

        private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
    }
}
