// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
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
        private static readonly Lazy<string> NugetGlobalPackagesFolder = new Lazy<string>(() =>
        {
            ISettings nugetSetting = Settings.LoadDefaultSettings(
                root: Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());

            return SettingsUtility.GetGlobalPackagesFolder(nugetSetting);
        });
    }
}
