// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using NuGet.Configuration;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class NuGetGlobalPackagesFolder
    {
        public static string GetLocation()
        {
            ISettings nugetSetting = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());

            return SettingsUtility.GetGlobalPackagesFolder(nugetSetting);
        }
    }
}
