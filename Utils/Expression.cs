using System;

namespace RIoT2.Core.Utils
{
    public static class Expression
    {
        public static T Evaluate<T>(string s)
        {
            if (typeof(T) == typeof(bool) && Boolean.TryParse(s, out var val))
                return (T)(object)val;

            NCalc.Expression e = new NCalc.Expression(s);
            return (T)e.Evaluate();
        }
    }
}
