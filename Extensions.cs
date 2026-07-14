using RIoT2.Core.Utils;
using System;
using System.Collections.Generic;

namespace RIoT2.Core
{
    /// <summary>
    /// Provides extension methods for JSON conversion, epoch/date handling, and array operations
    /// used across the RIoT2 solution.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Serializes the object to its JSON representation.
        /// </summary>
        /// <param name="o">The object to serialize.</param>
        /// <returns>The JSON representation of the object.</returns>
        public static string ToJson(this object o)
        {
            return Json.Serialize(o);
        }

        /// <summary>
        /// Deserializes the JSON string into an object of the specified type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        public static T ToObj<T>(this string json)
        {
            return Json.Deserialize<T>(json);
        }

        /// <summary>
        /// Converts the JSON string into a nested dictionary.
        /// </summary>
        /// <param name="json">The JSON string to convert.</param>
        /// <returns>A dictionary representing the JSON object.</returns>
        public static IDictionary<string, object> ToDict(this string json)
        {
            return Json.ToDictionary(json);
        }

        /// <summary>
        /// Finds a value in the JSON string using a JSONPath expression.
        /// </summary>
        /// <param name="json">The JSON string to search.</param>
        /// <param name="tokenPath">The JSONPath expression identifying the value.</param>
        /// <returns>The matched value as a string, or <c>null</c> if not found.</returns>
        public static string FindValue(this string json, string tokenPath)
        {
            return Json.FindValue(json, tokenPath);
        }

        /// <summary>
        /// Sets a value in the JSON string at the location identified by a JSONPath expression.
        /// </summary>
        /// <param name="json">The JSON string to modify.</param>
        /// <param name="tokenPath">The JSONPath expression identifying the target.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>The updated JSON string.</returns>
        public static string SetValue(this string json, string tokenPath, object value)
        {
            return SetValue(json, tokenPath, value);
        }

        /// <summary>
        /// Converts a Unix epoch time (in seconds) to a UTC <see cref="DateTime"/>.
        /// </summary>
        /// <param name="epochTime">The number of seconds since the Unix epoch.</param>
        /// <returns>The corresponding UTC <see cref="DateTime"/>.</returns>
        public static DateTime FromEpoch(this long epochTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(epochTime);
        }

        /// <summary>
        /// Converts a <see cref="DateTime"/> to Unix epoch time (in seconds).
        /// </summary>
        /// <param name="date">The date to convert.</param>
        /// <returns>The number of seconds since the Unix epoch.</returns>
        public static long ToEpoch(this DateTime date)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt64((date - epoch).TotalSeconds);
        }

        /// <summary>
        /// Returns a sub-array of the specified array.
        /// </summary>
        /// <typeparam name="T">The element type of the array.</typeparam>
        /// <param name="data">The source array.</param>
        /// <param name="index">The zero-based starting index of the sub-array.</param>
        /// <param name="length">The number of elements to copy; when 0, copies to the end of the array.</param>
        /// <returns>The extracted sub-array, or an empty array if <paramref name="data"/> is <c>null</c>.</returns>
        public static T[] SubArray<T>(this T[] data, int index, int length = 0)
        {
            if (data == null)
                return new T[0];

            if (length == 0)
                length = data.Length - index;

            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
    }
}
