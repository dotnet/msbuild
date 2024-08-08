// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Holder of configuration from .editorconfig file (not recognized by the infrastructure)
/// </summary>
public class ConfigurationContext
{
    private ConfigurationContext(CustomConfigurationData[] customConfigurationData, CheckConfigurationEffective[] checkConfig)
    {
        CustomConfigurationData = customConfigurationData;
        CheckConfig = checkConfig;
    }

    internal static ConfigurationContext FromDataEnumeration(CustomConfigurationData[] customConfigurationData, CheckConfigurationEffective[] checkConfig)
    {
        return new ConfigurationContext(customConfigurationData, checkConfig);
    }

    /// <summary>
    /// Custom configuration data - per each rule that has some specified.
    /// </summary>
    public IReadOnlyList<CustomConfigurationData> CustomConfigurationData { get; init; }

    /// <summary>
    /// Infrastructure configuration data
    /// </summary>
    public IReadOnlyList<CheckConfigurationEffective> CheckConfig { get; init; }
}
