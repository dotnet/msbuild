// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Build.Evaluation;

internal static class IntrinsicFunctionOverload
{
    private static readonly string[] s_knownOverloadName = { "Add", "Subtract", "Multiply", "Divide", "Modulo", };

    // Order by the TypeCode of the first parameter.
    // When change wave is enabled, order long before double.
    // Otherwise preserve prior behavior of double before long.
    // For reuse, the comparer is cached in a non-generic type.
    // Both comparer instances can be cached to support change wave testing.
    private static IComparer<MemberInfo>? s_comparerLongBeforeDouble;

    internal static IComparer<MemberInfo> IntrinsicFunctionOverloadMethodComparer => LongBeforeDoubleComparer;

    private static IComparer<MemberInfo> LongBeforeDoubleComparer => s_comparerLongBeforeDouble ??= Comparer<MemberInfo>.Create((key0, key1) => SelectTypeOfFirstParameter(key0).CompareTo(SelectTypeOfFirstParameter(key1)));

    internal static bool IsKnownOverloadMethodName(string methodName) => s_knownOverloadName.Any(name => string.Equals(name, methodName, StringComparison.OrdinalIgnoreCase));

    private static TypeCode SelectTypeOfFirstParameter(MemberInfo member)
    {
        MethodBase? method = member as MethodBase;
        if (method == null)
        {
            return TypeCode.Empty;
        }

        ParameterInfo[] parameters = method.GetParameters();
        return parameters.Length > 0
            ? Type.GetTypeCode(parameters[0].ParameterType)
            : TypeCode.Empty;
    }
}
