// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class DotnetSupportedPackageVersionsCsvProvider : IDotnetSupportedPackageVersionsProvider
    {
        public void AddDotnetSupportedPackageVersions(
            IDictionary<PackageDependencyInfo, PackageDependencyInfo> projectDependenciesPackages)
        {
            var dotnetSupportedPackageVersionsPath =
                Path.Combine(AppContext.BaseDirectory, "dotnet-supported-package-versions.csv");
            using (var reader = new StreamReader(File.OpenRead(dotnetSupportedPackageVersionsPath)))
            {
                SkipHeader(reader);
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    var packageName = values[0];
                    var ltsVersion = values[1];
                    var ftsVersion = values[2];

                    if (HasVersion(ltsVersion))
                    {
                        projectDependenciesPackages.Add(
                            new PackageDependencyInfo
                            {
                                Name = packageName,
                                Version = $"[,{ltsVersion})"
                            },
                            new PackageDependencyInfo
                            {
                                Name = packageName,
                                Version = ltsVersion
                            });
                    }

                    if(HasVersion(ftsVersion))
                    {
                        var version = HasVersion(ltsVersion) ? $"({ltsVersion},{ftsVersion})" : $"[,{ftsVersion})";
                        projectDependenciesPackages.Add(
                            new PackageDependencyInfo
                            {
                                Name = packageName,
                                Version = version
                            },
                            new PackageDependencyInfo
                            {
                                Name = packageName,
                                Version = ftsVersion
                            });
                    }
                }
            }
        }

        private void SkipHeader(StreamReader reader)
        {
            reader.ReadLine();
        }

        private bool HasVersion(string version)
        {
            return !string.IsNullOrEmpty(version);
        }
    }
}
