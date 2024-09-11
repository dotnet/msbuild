// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Experimental.BuildCheck;
internal static class CherResultSeverityExtensions
{
    public static DiagnosticSeverity? ToDiagnosticSeverity(this CheckResultSeverity? severity)
    {
        if (severity == null)
        {
            return null;
        }

        return ToDiagnosticSeverity(severity.Value);
    }

    public static DiagnosticSeverity ToDiagnosticSeverity(this CheckResultSeverity severity)
    {
        return severity switch
        {
            CheckResultSeverity.Default => DiagnosticSeverity.Default,
            CheckResultSeverity.None => DiagnosticSeverity.None,
            CheckResultSeverity.Suggestion => DiagnosticSeverity.Suggestion,
            CheckResultSeverity.Warning => DiagnosticSeverity.Warning,
            CheckResultSeverity.Error => DiagnosticSeverity.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };
    }
}
