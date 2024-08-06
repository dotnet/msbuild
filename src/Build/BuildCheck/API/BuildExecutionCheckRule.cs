﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Represents a rule that is a unit of a build check.
/// <see cref="BuildExecutionCheck"/> is a unit of executing the check, but it can be discovering multiple distinct violation types,
///  for this reason a single <see cref="BuildExecutionCheck"/> can expose multiple <see cref="BuildExecutionCheckRule"/>s.
/// </summary>
public class BuildExecutionCheckRule
{
    public BuildExecutionCheckRule(string id, string title, string description, string messageFormat,
        BuildExecutionCheckConfiguration defaultConfiguration)
    {
        Id = id;
        Title = title;
        Description = description;
        MessageFormat = messageFormat;
        DefaultConfiguration = defaultConfiguration;
    }

    /// <summary>
    /// The identification of the rule.
    ///
    /// Some background on ids:
    ///  * https://github.com/dotnet/roslyn-analyzers/blob/main/src/Utilities/Compiler/DiagnosticCategoryAndIdRanges.txt
    ///  * https://github.com/dotnet/roslyn/issues/40351
    ///
    /// Quick suggestion now - let's force external ids to start with 'X', for ours - avoid 'MSB'
    ///  maybe - BT - build static/styling; BA - build authoring; BE - build execution/environment; BC - build configuration
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The descriptive short summary of the rule.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// More detailed description of the violation the rule can be reporting (with possible suggestions).
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Message format that will be used by the actual reports (<see cref="BuildCheckResult"/>) - those will just supply the actual arguments.
    /// </summary>
    public string MessageFormat { get; }

    /// <summary>
    /// The default configuration - overridable by the user via .editorconfig.
    /// If no user specified configuration is provided, this default will be used.
    /// </summary>
    public BuildExecutionCheckConfiguration DefaultConfiguration { get; }
}
