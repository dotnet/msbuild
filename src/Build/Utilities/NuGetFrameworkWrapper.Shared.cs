// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Helpers in this partial are independent of how <c>NuGet.Frameworks</c> is loaded
/// (direct reference on .NET, reflection on .NET Framework) and so are shared between
/// the two implementations.
/// </summary>
internal sealed partial class NuGetFrameworkWrapper
{
    /// <summary>
    /// Formats <paramref name="version"/> with at least <paramref name="minVersionPartCount"/>
    /// components, trimming any trailing zero parts beyond that minimum.
    /// </summary>
    private static string GetNonZeroVersionParts(Version version, int minVersionPartCount)
    {
        var nonZeroVersionParts = version.Revision == 0 ? version.Build == 0 ? version.Minor == 0 ? 1 : 2 : 3 : 4;
        return version.ToString(Math.Max(nonZeroVersionParts, minVersionPartCount));
    }
}
