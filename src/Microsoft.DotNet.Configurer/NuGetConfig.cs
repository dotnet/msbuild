// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Common;
using NuGet.Configuration;
using System.Linq;

namespace Microsoft.DotNet.Configurer
{
    public class NuGetConfig : INuGetConfig
    {
        public const string FallbackPackageFolders = "fallbackPackageFolders";

        private ISettings _settings;

        public NuGetConfig(CLIFallbackFolderPathCalculator cliFallbackFolderPathCalculator)
        {
            _settings = new Settings(cliFallbackFolderPathCalculator.NuGetUserSettingsDirectory);
        }

        internal NuGetConfig(ISettings settings)
        {
            _settings = settings;
        }

        public void AddCLIFallbackFolder(string fallbackFolderPath)
        {
            if (!IsCLIFallbackFolderSet(fallbackFolderPath))
            {
                _settings.SetValue(FallbackPackageFolders, "CLIFallbackFolder", fallbackFolderPath);
            }
        }

        private bool IsCLIFallbackFolderSet(string fallbackFolderPath)
        {
            return _settings.GetSettingValues(FallbackPackageFolders).Any(s => s.Value == fallbackFolderPath);
        }
    }
}
