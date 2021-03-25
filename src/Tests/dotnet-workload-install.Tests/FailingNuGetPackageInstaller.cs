// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageInstaller
{
    internal class FailingNuGetPackageInstaller : INuGetPackageInstaller
    {
        public readonly string MockPackageDir;

        public FailingNuGetPackageInstaller(string testDir)
        {
            MockPackageDir = Path.Combine(testDir, "MockPackages");
            Directory.CreateDirectory(MockPackageDir);
        }

        public Task<string> InstallPackageAsync(PackageId packageId, NuGetVersion packageVersion)
        {
            var mockPackagePath = Path.Combine(MockPackageDir, $"{packageId}.{packageVersion}.nupkg");
            File.WriteAllText(mockPackagePath, string.Empty);
            return Task.FromResult(mockPackagePath);
        }

        public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);
            File.WriteAllText(Path.Combine(targetFolder, "testfile.txt"), string.Empty);
            throw new Exception("Test Failure");
        }
    }
}
