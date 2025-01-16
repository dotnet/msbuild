// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Evaluation.Expander
{
    internal class ArgumentParser
    {
        internal static bool TryGetArgs(object[] args, out string? arg0, out string? arg1, bool enforceLength = true)
        {
            arg0 = null;
            arg1 = null;

            if (enforceLength && args.Length != 2)
            {
                return false;
            }

            if (args[0] is string value0 &&
                args[1] is string value1)
            {
                arg0 = value0;
                arg1 = value1;

                return true;
            }

            return false;
        }

        internal static bool TryGetArgs(object[] args, out string? arg0, out string? arg1, out string? arg2)
        {
            arg0 = null;
            arg1 = null;
            arg2 = null;

            if (args.Length != 3)
            {
                return false;
            }

            if (args[0] is string value0 &&
                args[1] is string value1 &&
                args[2] is string value2)
            {
                arg0 = value0;
                arg1 = value1;
                arg2 = value2;

                return true;
            }

            return false;
        }

        internal static bool TryGetArgs(object[] args, out string? arg0, out string? arg1, out string? arg2, out string? arg3)
        {
            arg0 = null;
            arg1 = null;
            arg2 = null;
            arg3 = null;

            if (args.Length != 4)
            {
                return false;
            }

            if (args[0] is string value0 &&
                args[1] is string value1 &&
                args[2] is string value2 &&
                args[3] is string value3)
            {
                arg0 = value0;
                arg1 = value1;
                arg2 = value2;
                arg3 = value3;

                return true;
            }

            return false;
        }

        internal static bool TryGetArgs(object[] args, out string? arg0, out string? arg1)
        {
            arg0 = null;
            arg1 = null;

            if (args.Length != 2)
            {
                return false;
            }

            if (args[0] is string value0 &&
                args[1] is string value1)
            {
                arg0 = value0;
                arg1 = value1;

                return true;
            }

            return false;
        }

        internal static bool TryGetArgs(object[] args, out string? arg0, out int arg1, out int arg2)
        {
            arg0 = null;
            arg1 = 0;
            arg2 = 0;

            if (args.Length != 3)
            {
                return false;
            }

            var value1 = args[1] as string;
            var value2 = args[2] as string;
            arg0 = args[0] as string;
            if (value1 != null &&
                value2 != null &&
                arg0 != null &&
                int.TryParse(value1, out arg1) &&
                int.TryParse(value2, out arg2))
            {
                return true;
            }

            return false;
        }

        internal static bool TryGetArg(object[] args, out int arg0)
        {
            if (args.Length != 1)
            {
                arg0 = 0;
                return false;
            }

            return TryConvertToInt(args[0], out arg0);
        }

        internal static bool TryGetArg(object[] args, out Version? arg0)
        {
            if (args.Length != 1)
            {
                arg0 = default;
                return false;
            }

            return TryConvertToVersion(args[0], out arg0);
        }

        internal static bool TryConvertToVersion(object value, out Version? arg0)
        {
            string? val = value as string;

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
        internal static bool TryConvertToInt(object? value, out int arg)
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
        internal static bool TryConvertToLong(object? value, out long arg)
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
        internal static bool TryConvertToDouble(object? value, out double arg)
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

        internal static bool TryGetArg(object[] args, out string? arg0)
        {
            if (args.Length != 1)
            {
                arg0 = null;
                return false;
            }

            arg0 = args[0] as string;
            return arg0 != null;
        }

        internal static bool TryGetArgs(object[] args, out string? arg0, out StringComparison arg1)
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

        internal static bool TryGetArgs(object[] args, out int arg0)
        {
            arg0 = 0;

            if (args.Length != 1)
            {
                return false;
            }

            return TryConvertToInt(args[0], out arg0);
        }

        internal static bool TryGetArgs(object[] args, out int arg0, out int arg1)
        {
            arg0 = 0;
            arg1 = 0;

            if (args.Length != 2)
            {
                return false;
            }

            return TryConvertToInt(args[0], out arg0) &&
                   TryConvertToInt(args[1], out arg1);
        }

        internal static bool TryGetArgs(object[] args, out double arg0, out double arg1)
        {
            arg0 = 0;
            arg1 = 0;

            if (args.Length != 2)
            {
                return false;
            }

            return TryConvertToDouble(args[0], out arg0) &&
                   TryConvertToDouble(args[1], out arg1);
        }

        internal static bool TryGetArgs(object[] args, out int arg0, out string? arg1)
        {
            arg0 = 0;
            arg1 = null;

            if (args.Length != 2)
            {
                return false;
            }

            arg1 = args[1] as string;
            if (arg1 == null && args[1] is char ch)
            {
                arg1 = ch.ToString();
            }

            if (TryConvertToInt(args[0], out arg0) &&
                arg1 != null)
            {
                return true;
            }

            return false;
        }

        internal static bool TryGetArgs(object[] args, out string? arg0, out int arg1)
        {
            arg0 = null;
            arg1 = 0;

            if (args.Length != 2)
            {
                return false;
            }

            var value1 = args[1] as string;
            arg0 = args[0] as string;
            if (value1 != null &&
                arg0 != null &&
                int.TryParse(value1, out arg1))
            {
                return true;
            }

            return false;
        }

        internal static bool IsFloatingPointRepresentation(object value)
        {
            return value is double || (value is string str && double.TryParse(str, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double _));
        }

        internal static bool TryExecuteArithmeticOverload(object[] args, Func<long, long, long> integerOperation, Func<double, double, double> realOperation, out object? resultValue)
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
