// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

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

    /// <summary>
    /// Tiny adapter the shared <see cref="FilterTargetFrameworks{TParsed, TAdapter}"/> helper
    /// uses to read TFM properties without committing to a concrete NuGet.Frameworks type.
    /// The Core branch supplies a strongly-typed adapter over <c>NuGetFramework</c>; the
    /// .NET Framework branch supplies a reflection-based adapter over <c>object</c>.
    /// </summary>
    private interface ITfmAdapter<TParsed>
    {
        TParsed Parse(string tfm);
        string GetFramework(TParsed parsed);
        bool GetAllFrameworkVersions(TParsed parsed);
        Version GetVersion(TParsed parsed);
    }

    /// <summary>
    /// Filters <paramref name="incoming"/> down to the entries compatible with any entry in
    /// <paramref name="filter"/>, where "compatible" means same framework identifier (case
    /// insensitive) and either both sides match all versions or the parsed versions are equal.
    /// Constraining <typeparamref name="TAdapter"/> to a value type lets the JIT specialize and
    /// devirtualize the adapter calls, so the shared body has the same runtime profile as the
    /// inlined per-target implementation it replaced.
    /// </summary>
    private static string FilterTargetFrameworks<TParsed, TAdapter>(string incoming, string filter, TAdapter adapter)
        where TAdapter : struct, ITfmAdapter<TParsed>
    {
        IEnumerable<(string originalTfm, TParsed parsedTfm)> incomingFrameworks = ParseTfms(incoming, adapter);
        IEnumerable<(string originalTfm, TParsed parsedTfm)> filterFrameworks = [..ParseTfms(filter, adapter)];
        StringBuilder tfmList = new StringBuilder();

        // An incoming target framework from 'incoming' is kept if it is compatible with any of the desired target frameworks on 'filter'.
        foreach (var l in incomingFrameworks)
        {
            bool keep = false;
            foreach (var r in filterFrameworks)
            {
                if (adapter.GetFramework(l.parsedTfm).Equals(adapter.GetFramework(r.parsedTfm), StringComparison.OrdinalIgnoreCase) &&
                    ((adapter.GetAllFrameworkVersions(l.parsedTfm) && adapter.GetAllFrameworkVersions(r.parsedTfm)) ||
                     adapter.GetVersion(l.parsedTfm) == adapter.GetVersion(r.parsedTfm)))
                {
                    keep = true;
                    break;
                }
            }

            if (keep)
            {
                if (tfmList.Length > 0)
                {
                    tfmList.Append(';');
                }

                tfmList.Append(l.originalTfm);
            }
        }

        return tfmList.ToString();

        static IEnumerable<(string originalTfm, TParsed parsedTfm)> ParseTfms(string desiredTargetFrameworks, TAdapter adapter)
        {
            foreach (string tfm in desiredTargetFrameworks.Split([';'], StringSplitOptions.RemoveEmptyEntries))
            {
                yield return (tfm, adapter.Parse(tfm));
            }
        }
    }
}

