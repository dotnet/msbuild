// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Representation of a single report of a single finding from a BuildAnalyzer
/// Each rule has upfront known message format - so only the concrete arguments are added
/// Optionally a location is attached - in the near future we might need to support multiple locations
///  (for 2 cases - a) grouped result for multiple occurrences; b) a single report for a finding resulting from combination of multiple locations)
/// </summary>
public sealed class BuildCheckResult : IBuildCheckResult
{
    public static BuildCheckResult Create(BuildAnalyzerRule rule, ElementLocation location, params string[] messageArgs)
    {
        return new BuildCheckResult(rule, location, messageArgs);
    }

    public BuildCheckResult(BuildAnalyzerRule buildAnalyzerRule, ElementLocation location, string[] messageArgs)
    {
        BuildAnalyzerRule = buildAnalyzerRule;
        Location = location;
        MessageArgs = messageArgs;
    }

    internal BuildEventArgs ToEventArgs(BuildAnalyzerResultSeverity severity)
        => severity switch
        {
            BuildAnalyzerResultSeverity.Info => new BuildCheckResultMessage(this),
            BuildAnalyzerResultSeverity.Warning => new BuildCheckResultWarning(this),
            BuildAnalyzerResultSeverity.Error => new BuildCheckResultError(this),
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };

    public BuildAnalyzerRule BuildAnalyzerRule { get; }

    /// <summary>
    /// Optional location of the finding (in near future we might need to support multiple locations).
    /// </summary>
    public ElementLocation Location { get; }

    public string LocationString => Location.LocationString;

    public string[] MessageArgs { get; }
    public string MessageFormat => BuildAnalyzerRule.MessageFormat;

    // Here we will provide different link for built-in rules and custom rules - once we have the base classes differentiated.
    public string FormatMessage() =>
        _message ??= $"{(Equals(Location ?? ElementLocation.EmptyLocation, ElementLocation.EmptyLocation) ? string.Empty : (Location!.LocationString + ": "))}{BuildAnalyzerRule.Id}: https://aka.ms/buildcheck/codes#{BuildAnalyzerRule.Id} - {string.Format(BuildAnalyzerRule.MessageFormat, MessageArgs)}";

    private string? _message;
}
