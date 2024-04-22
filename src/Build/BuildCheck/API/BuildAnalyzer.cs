// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.BuildCheck.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Base class for build analyzers.
/// Same base will be used for custom and built-in analyzers.
/// <see cref="BuildAnalyzer"/> is a unit of build analysis execution, but it can contain multiple rules - each representing a distinct violation.
/// </summary>
public abstract class BuildAnalyzer : IDisposable
{
    /// <summary>
    /// Friendly name of the analyzer.
    /// Should be unique - as it will be used in the tracing stats, infrastructure error messages, etc.
    /// </summary>
    public abstract string FriendlyName { get; }

    /// <summary>
    /// Single or multiple rules supported by the analyzer.
    /// </summary>
    public abstract IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; }

    /// <summary>
    /// Optional initialization of the analyzer.
    /// </summary>
    /// <param name="configurationContext">
    /// Custom data (not recognized by the infrastructure) passed from .editorconfig
    /// Currently the custom data has to be identical for all rules in the analyzer and all projects.
    /// </param>
    public abstract void Initialize(ConfigurationContext configurationContext);

    /// <summary>
    /// Used by the implementors to subscribe to data and events they are interested in.
    /// </summary>
    /// <param name="registrationContext">
    /// The context that enables subscriptions for data pumping from the infrastructure.
    /// </param>
    public abstract void RegisterActions(IBuildCheckRegistrationContext registrationContext);

    public virtual void Dispose()
    { }
}
