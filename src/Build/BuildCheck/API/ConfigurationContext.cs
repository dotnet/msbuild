// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Holder of an optional configuration from .editorconfig file (not recognized by the infrastructure)
/// </summary>
public class ConfigurationContext
{
    private ConfigurationContext(CustomConfigurationData[] customConfigurationData, BuildAnalyzerConfiguration[] buildAnalyzerConfig)
    {
        CustomConfigurationData = customConfigurationData;
        BuildAnalyzerConfig = buildAnalyzerConfig;
    }

    internal static ConfigurationContext FromDataEnumeration(CustomConfigurationData[] customConfigurationData, BuildAnalyzerConfiguration[] buildAnalyzerConfig)
    {
        return new ConfigurationContext(customConfigurationData, buildAnalyzerConfig);
    }

    /// <summary>
    /// Custom configuration data - per each rule that has some specified.
    /// </summary>
    public IReadOnlyList<CustomConfigurationData> CustomConfigurationData { get; init; }

    // TODO: severity / enablement
    // Severiy model item is connected to this
    public BuildAnalyzerConfiguration[] BuildAnalyzerConfig { get; init; }
}
