// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities;

internal static class StringExtensions
{
    /// <summary>
    /// Converts a string to a bool.  We consider "true/false", "on/off", and
    /// "yes/no" to be valid boolean representations in the XML. The '!' prefix for negation is allowed as well.
    /// Unrecognized values lead to exception
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when given argument is unrecognized MSBuild boolean string.</exception>
    public static bool IsMSBuildTrueString(this string? s) =>
        ConversionUtilities.ConvertStringToBool(s, nullOrWhitespaceIsFalse: true);

    /// <summary>
    /// Converts a string to a bool.  We consider "true/false", "on/off", and
    /// "yes/no" to be valid boolean representations in the XML. The '!' prefix for negation is allowed as well.
    /// Unrecognized values lead to exception
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when given argument is unrecognized MSBuild boolean string.</exception>
    public static bool IsMSBuildFalseString(this string? s)
        => !ConversionUtilities.ConvertStringToBool(s, nullOrWhitespaceIsFalse: true);
}
