// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.TemplateEngine.Abstractions;
using NuGet.Packaging;

namespace Microsoft.TemplateEngine.Cli
{
    internal static class NuGetUtils
    {
        /// <summary>
        /// Gets ID and version of NuGet package.
        /// </summary>
        /// <param name="engineEnvironmentSettings">environment settings.</param>
        /// <param name="packageLocation">file path to NuGet package.</param>
        /// <returns>ID and version of NuGet package; or default in case path is not a valid NuGet package.</returns>
        internal static (string Id, string Version) GetNuGetPackageInfo(IEngineEnvironmentSettings engineEnvironmentSettings, string packageLocation)
        {
            if (!engineEnvironmentSettings.Host.FileSystem.FileExists(packageLocation))
            {
                //packageLocation is not a file
                return default;
            }

            try
            {
                using Stream inputStream = engineEnvironmentSettings.Host.FileSystem.OpenRead(packageLocation);
                using PackageArchiveReader reader = new PackageArchiveReader(inputStream);

                NuspecReader nuspec = reader.NuspecReader;

                return new(nuspec.GetId(), nuspec.GetVersion().ToNormalizedString());
            }
            catch (Exception)
            {
                return default;
            }
        }
    }
}
