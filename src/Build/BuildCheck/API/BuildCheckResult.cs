// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Representation of a single report of a single finding from a Check
/// Each rule has upfront known message format - so only the concrete arguments are added
/// Optionally a location is attached - in the near future we might need to support multiple locations
///  (for 2 cases - a) grouped result for multiple occurrences; b) a single report for a finding resulting from combination of multiple locations).
/// </summary>
public sealed class BuildCheckResult : IBuildCheckResult
{
    public static BuildCheckResult Create(CheckRule rule, IMSBuildElementLocation location, params string[] messageArgs) => new BuildCheckResult(rule, location, messageArgs);

    internal static BuildCheckResult CreateBuiltIn(CheckRule rule, IMSBuildElementLocation location,
        params string[] messageArgs) => new BuildCheckResult(rule, location, messageArgs) { _isBuiltIn = true };

    public BuildCheckResult(CheckRule checkConfig, IMSBuildElementLocation location, string[] messageArgs)
    {
        CheckRule = checkConfig;
        Location = location;
        MessageArgs = messageArgs;
    }

    internal BuildEventArgs ToEventArgs(CheckResultSeverity severity)
        => severity switch
        {
            CheckResultSeverity.Suggestion => new BuildCheckResultMessage(this),
            CheckResultSeverity.Warning => new BuildCheckResultWarning(this),
            CheckResultSeverity.Error => new BuildCheckResultError(this),
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };

    public CheckRule CheckRule { get; }

    public string Code => CheckRule.Id;

    /// <summary>
    /// Optional location of the finding (in near future we might need to support multiple locations).
    /// </summary>
    public IMSBuildElementLocation Location { get; }

    public string LocationString => Location.LocationString;

    public string[] MessageArgs { get; }

    public string MessageFormat => CheckRule.MessageFormat;

    public string FormatMessage() =>
        _message ??= _isBuiltIn
            // Builtin rules get unified helplink.
            ? $"https://aka.ms/buildcheck/codes#{CheckRule.Id} - {string.Format(CheckRule.MessageFormat, MessageArgs)}"
            // Custom rules can provide their own helplink.
            : (!string.IsNullOrEmpty(CheckRule.HelpLinkUri) ? $"{CheckRule.HelpLinkUri} - " : null) +
              string.Format(CheckRule.MessageFormat, MessageArgs);

    private string? _message;
    private bool _isBuiltIn;
}
