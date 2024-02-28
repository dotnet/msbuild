// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BuildCop.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCop;

/// <summary>
/// Holder of an optional configuration from .editorconfig file (not recognized by the infrastructure)
/// </summary>
public class ConfigurationContext
{
    private ConfigurationContext(CustomConfigurationData[] customConfigurationData)
    {
        CustomConfigurationData = customConfigurationData;
    }

    public static ConfigurationContext FromDataEnumeration(CustomConfigurationData[] customConfigurationData)
    {
        if (!customConfigurationData.Any(BuildCop.CustomConfigurationData.NotNull))
        {
            return Null;
        }

        return new ConfigurationContext(
            customConfigurationData
                .Where(BuildCop.CustomConfigurationData.NotNull)
                .ToArray());
    }

    public static ConfigurationContext Null { get; } = new(Array.Empty<CustomConfigurationData>());

    /// <summary>
    /// Custom configuration data - per each rule that has some specified.
    /// </summary>
    public CustomConfigurationData[] CustomConfigurationData { get; init; }
}
