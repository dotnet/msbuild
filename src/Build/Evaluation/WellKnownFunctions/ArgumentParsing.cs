// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


#nullable disable

namespace Microsoft.Build.Evaluation.WellKnownFunctions
{
    internal class ArgumentParsing
    {

        private static Func<int, int> s_castInt = i => i;
        private static Func<double, double> s_castDouble = d => d;
        private static Func<long, long> s_castLong = l => l;
        private static Func<string, string> s_castString = s => s;
        private static Func<Version, Version> s_castVersion = s => s;
        private static Type s_type = typeof(string);
        private static Type v_type = typeof(Version);

        // We cast delegates to avoid boxing/unboxing of the values.
        internal static bool TryGetArgument<T>(object arg, out T arg0)
        {
            arg0 = default;
            switch (arg0)
            {              
                case int:
                    var result = TryConvertToInt(arg, out int i);
                    arg0 = ((Func<int, T>)(object)(s_castInt))(i);
                    return result;
                case long:
                    var result1 = TryConvertToLong(arg, out long l);
                    arg0 = ((Func<long, T>)(object)s_castLong)(l);
                    return result1;
                case double:
                    var result2 = TryConvertToDouble(arg, out double d);
                    arg0 = ((Func<double, T>)(object)s_castDouble)(d);
                    return result2;

                // This appears to be necessary due to the fact that default string is null and thus skips the case string: jump that I wanted to take.
                // Same goes for the Version.
                default:
                    if (typeof(T) == s_type)
                    {
                        // Note: one of the functions was doing char -> string conversion which I ignored here because it should be redundant.
                        // This is due to MSBuild loading even one char strings as a string instead of as a char.
                        string s = arg as string;
                        if (s != null)
                        {
                            arg0 = ((Func<string, T>)(object)s_castString)(s);
                        }
                        return arg0 != null;
                    }
                    else if (typeof(T) == v_type)
                    {
                        var result3 = TryConvertToVersion(arg, out Version v);
                        ((Func<Version, T>)(object)s_castVersion)(v);
                        return result3;
                    }
                    return false;
            }
        }

        internal static bool TryGetArg<T1>(object[] args, out T1 arg0)
        {
            if (args.Length != 1)
            {
                arg0 = default;
                return false;
            }
            return TryGetArgument(args[0], out arg0);
        }

        internal static bool TryGetArgs<T1, T2>(object[] args, out T1 arg0, out T2 arg1, bool enforceLength = true)
        {

            if ((enforceLength && args.Length != 2) || args.Length < 2)
            {
                arg0 = default;
                arg1 = default;
                return false;
            }

            if (TryGetArgument(args[0], out arg0) &&
                TryGetArgument(args[1], out arg1))
            {
                return true;
            }
            else
            {
                // this has to happen here, otherwise we could set 
                arg0 = default;
                arg1 = default;
                return false;
            }
        }

        internal static bool TryGetArgs<T1, T2, T3>(object[] args, out T1 arg0, out T2 arg1, out T3 arg2)
        {
            if (args.Length != 3)
            {
                arg0 = default;
                arg1 = default;
                arg2 = default;
                return false;
            }
            if (TryGetArgument(args[0], out arg0) &&
                TryGetArgument(args[1], out arg1) &&
                TryGetArgument(args[2], out arg2))
            {
                return true;
            }
            else
            {
                // this has to happen here, otherwise we could set 
                arg0 = default;
                arg1 = default;
                arg2 = default;
                return false;
            }
        }

        internal static bool TryGetArgs<T1, T2, T3, T4>(object[] args, out T1 arg0, out T2 arg1, out T3 arg2, out T4 arg3)
        {
            if (args.Length != 4)
            {
                arg0 = default;
                arg1 = default;
                arg2 = default;
                arg3 = default;
                return false;
            }
            if (TryGetArgument(args[0], out arg0) &&
                TryGetArgument(args[1], out arg1) &&
                TryGetArgument(args[2], out arg2) &&
                TryGetArgument(args[3], out arg3))
            {
                return true;
            }
            else
            {
                // this has to happen here, otherwise we could set 
                arg0 = default;
                arg1 = default;
                arg2 = default;
                arg3 = default;
                return false;
            }
        }

        internal static bool TryConvertToVersion(object value, out Version arg0)
        {
            string val = value as string;

            if (string.IsNullOrEmpty(val) || !Version.TryParse(val, out arg0))
            {
                arg0 = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Try to convert value to int.
        /// </summary>
        internal static bool TryConvertToInt(object value, out int arg)
        {
            switch (value)
            {
                case double d:
                    if (d >= int.MinValue && d <= int.MaxValue)
                    {
                        arg = Convert.ToInt32(d);
                        if (Math.Abs(arg - d) == 0)
                        {
                            return true;
                        }
                    }

                    break;
                case long l:
                    if (l >= int.MinValue && l <= int.MaxValue)
                    {
                        arg = Convert.ToInt32(l);
                        return true;
                    }

                    break;
                case int i:
                    arg = i;
                    return true;
                case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out arg):
                    return true;
            }

            arg = 0;
            return false;
        }

        /// <summary>
        /// Try to convert value to long.
        /// </summary>
        internal static bool TryConvertToLong(object value, out long arg)
        {
            switch (value)
            {
                case double d:
                    if (d >= long.MinValue && d <= long.MaxValue)
                    {
                        arg = (long)d;
                        if (Math.Abs(arg - d) == 0)
                        {
                            return true;
                        }
                    }

                    break;
                case long l:
                    arg = l;
                    return true;
                case int i:
                    arg = i;
                    return true;
                case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out arg):
                    return true;
            }

            arg = 0;
            return false;
        }

        /// <summary>
        /// Try to convert value to double.
        /// </summary>
        internal static bool TryConvertToDouble(object value, out double arg)
        {
            switch (value)
            {
                case double unboxed:
                    arg = unboxed;
                    return true;
                case long l:
                    arg = l;
                    return true;
                case int i:
                    arg = i;
                    return true;
                case string str when double.TryParse(str, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out arg):
                    return true;
                default:
                    arg = 0;
                    return false;
            }
        }

        internal static bool TryGetArgs(object[] args, out string arg0, out StringComparison arg1)
        {
            if (args.Length != 2)
            {
                arg0 = null;
                arg1 = default;

                return false;
            }

            arg0 = args[0] as string;

            // reject enums as ints. In C# this would require a cast, which is not supported in msbuild expressions
            if (arg0 == null || !(args[1] is string comparisonTypeName) || int.TryParse(comparisonTypeName, out _))
            {
                arg1 = default;
                return false;
            }

            // Allow fully-qualified enum, e.g. "System.StringComparison.OrdinalIgnoreCase"
            if (comparisonTypeName.Contains('.'))
            {
                comparisonTypeName = comparisonTypeName.Replace("System.StringComparison.", "").Replace("StringComparison.", "");
            }

            return Enum.TryParse(comparisonTypeName, out arg1);
        }

        internal static bool IsFloatingPointRepresentation(object value)
        {
            return value is double || (value is string str && double.TryParse(str, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double _));
        }

        internal static bool TryExecuteArithmeticOverload(object[] args, Func<long, long, long> integerOperation, Func<double, double, double> realOperation, out object resultValue)
        {
            resultValue = null;

            if (args.Length != 2)
            {
                return false;
            }

            if (TryConvertToLong(args[0], out long argLong0) && TryConvertToLong(args[1], out long argLong1))
            {
                resultValue = integerOperation(argLong0, argLong1);
                return true;
            }

            if (TryConvertToDouble(args[0], out double argDouble0) && TryConvertToDouble(args[1], out double argDouble1))
            {
                resultValue = realOperation(argDouble0, argDouble1);
                return true;
            }

            return false;
        }
    }
}
#nullable enable
