// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Configuration;

namespace Microsoft.NET.TestFramework.Utilities
{
    public static class NuGetGlobalPackagesFolder
    {
        public static string GetLocation()
        {
            return NugetGlobalPackagesFolder.Value;
        }

        // This call could take about 00.050s. So cache it can help
        private static readonly Lazy<string> NugetGlobalPackagesFolder = new(() =>
        {
            ISettings nugetSetting = Settings.LoadDefaultSettings(
                root: Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());

            return SettingsUtility.GetGlobalPackagesFolder(nugetSetting);
        });
    }
}
