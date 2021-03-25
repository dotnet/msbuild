// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageInstaller
{
    internal class MockNuGetPackageInstaller : INuGetPackageInstaller
    {
        public List<(PackageId, NuGetVersion)> InstallCallParams = new List<(PackageId, NuGetVersion)>();

        public List<(string, string)> ExtractCallParams = new List<(string, string)>();

        public Task<string> InstallPackageAsync(PackageId packageId, NuGetVersion packageVersion)
        {
            InstallCallParams.Add((packageId, packageVersion));
            return Task.FromResult("Mock/path");
        }

        public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, string targetFolder)
        {
            ExtractCallParams.Add((packagePath, targetFolder));
            return Task.FromResult(new List<string>() as IEnumerable<string>);
        }
    }
}
