// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BuildCheck.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Holder of an optional configuration from .editorconfig file (not recognized by the infrastructure)
/// </summary>
public class ConfigurationContext
{
    private ConfigurationContext(CustomConfigurationData[] customConfigurationData)
    {
        CustomConfigurationData = customConfigurationData;
    }

    internal static ConfigurationContext FromDataEnumeration(CustomConfigurationData[] customConfigurationData)
    {
        if (!customConfigurationData.Any(BuildCheck.CustomConfigurationData.NotNull))
        {
            return Null;
        }

        return new ConfigurationContext(
            customConfigurationData
                .Where(BuildCheck.CustomConfigurationData.NotNull)
                .ToArray());
    }

    internal static ConfigurationContext Null { get; } = new(Array.Empty<CustomConfigurationData>());

    /// <summary>
    /// Custom configuration data - per each rule that has some specified.
    /// </summary>
    public IReadOnlyList<CustomConfigurationData> CustomConfigurationData { get; init; }
}
