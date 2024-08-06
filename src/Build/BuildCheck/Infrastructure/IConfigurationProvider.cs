// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;

namespace Microsoft.Build.BuildCheck.Infrastructure;
internal interface IConfigurationProvider
{
    BuildAnalyzerConfiguration[] GetUserConfigurations(
        string projectFullPath,
        IReadOnlyList<string> ruleIds);

    BuildAnalyzerConfigurationEffective[] GetMergedConfigurations(
        string projectFullPath,
        BuildAnalyzer analyzer);

    BuildAnalyzerConfigurationEffective[] GetMergedConfigurations(
        BuildAnalyzerConfiguration[] userConfigs,
        BuildAnalyzer analyzer);

    CustomConfigurationData[] GetCustomConfigurations(
        string projectFullPath,
        IReadOnlyList<string> ruleIds);

    void CheckCustomConfigurationDataValidity(string projectFullPath, string ruleId);
}
