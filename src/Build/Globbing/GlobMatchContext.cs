// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Globbing;

/// <summary>
/// Reuses path normalization within a single composite match without retaining input paths.
/// </summary>
internal struct GlobMatchContext(string stringToMatch)
{
    private string? _globRoot;
    private string? _normalizedInput;

    internal string StringToMatch { get; } = stringToMatch;

    internal string GetNormalizedInput(string globRoot)
    {
        if (_normalizedInput is null || !string.Equals(_globRoot, globRoot, StringComparison.Ordinal))
        {
            _normalizedInput = MSBuildGlob.NormalizeMatchInput(globRoot, StringToMatch);
            _globRoot = globRoot;
        }

        return _normalizedInput;
    }

    internal bool IsMatch(IMSBuildGlob glob)
    {
        if (glob is IContextAwareGlob contextAwareGlob)
        {
            return contextAwareGlob.IsMatch(ref this);
        }

        // Custom matchers may change ambient path resolution.
        _normalizedInput = null;
        return glob.IsMatch(StringToMatch);
    }
}
