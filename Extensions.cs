using RIoT2.Core.Utils;
using System;
using System.Collections.Generic;

namespace RIoT2.Core
{
    public static class Extensions
    {
        public static string ToJson(this object o)
        {
            return Json.Serialize(o);
        }

        public static T ToObj<T>(this string json)
        {
            return Json.Deserialize<T>(json);
        }

        public static IDictionary<string, object> ToDict(this string json)
        {
            return Json.ToDictionary(json);
        }

        public static string FindValue(this string json, string tokenPath)
        {
            return Json.FindValue(json, tokenPath);
        }

        public static string SetValue(this string json, string tokenPath, object value)
        {
            return SetValue(json, tokenPath, value);
        }

        public static DateTime FromEpoch(this long epochTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(epochTime);
        }

        public static long ToEpoch(this DateTime date)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt64((date - epoch).TotalSeconds);
        }

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
