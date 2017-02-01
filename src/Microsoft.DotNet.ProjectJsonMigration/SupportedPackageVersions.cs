// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class PackageDependencyInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string PrivateAssets { get; set; }

        public bool IsMetaPackage
        {
            get
            {
                return !string.IsNullOrEmpty(Name) && 
                    (Name.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase) ||
                     Name.Equals("NETStandard.Library", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    internal class SupportedPackageVersions
    {
        public const string SdkPackageName = "Microsoft.NET.Sdk";
        public const string WebSdkPackageName = "Microsoft.NET.Sdk.Web";
        public const string TestSdkPackageName = "Microsoft.NET.Test.Sdk";
        public const string XUnitPackageName = "xunit";
        public const string XUnitRunnerPackageName = "xunit.runner.visualstudio";
        public const string MstestTestAdapterName = "MSTest.TestAdapter";
        public const string MstestTestFrameworkName = "MSTest.TestFramework";
        public const string NetStandardPackageName = "NETStandard.Library";
        public const string NetStandardPackageVersion = "1.6.0";
        public const string DotnetTestXunit = "dotnet-test-xunit";
        public const string DotnetTestMSTest = "dotnet-test-mstest";

        public readonly IDictionary<PackageDependencyInfo, PackageDependencyInfo> ProjectDependencyPackages;

        public static readonly IDictionary<PackageDependencyInfo, PackageDependencyInfo> ProjectToolPackages =
            new Dictionary<PackageDependencyInfo, PackageDependencyInfo> {
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.Tools",
                        Version = "[1.0.0-*,)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.Tools.DotNet",
                        Version = ConstantPackageVersions.AspNetToolsVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Razor.Tools",
                        Version = "[1.0.0-*,)"
                    },
                    null
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.Razor.ViewCompilation.Tools",
                        Version = "[1.0.0-*,)"
                    },
                    null
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.VisualStudio.Web.CodeGeneration.Tools",
                        Version = "[1.0.0-*,)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.VisualStudio.Web.CodeGeneration.Tools",
                        Version = ConstantPackageVersions.AspNetToolsVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.DotNet.Watcher.Tools",
                        Version = "[1.0.0-*,)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.DotNet.Watcher.Tools",
                        Version = ConstantPackageVersions.AspNetToolsVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.Extensions.SecretManager.Tools",
                        Version = "[1.0.0-*,)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.Extensions.SecretManager.Tools",
                        Version = ConstantPackageVersions.AspNetToolsVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Server.IISIntegration.Tools",
                        Version = "[1.0.0-*,)"
                    },
                    null
                },
                {
                    new PackageDependencyInfo{
                        Name = "BundlerMinifier.Core",
                        Version = "[1.0.0-*,)"
                    },
                    new PackageDependencyInfo {
                        Name = "BundlerMinifier.Core",
                        Version = ConstantPackageVersions.BundleMinifierToolVersion
                    }
                }
        };

        public SupportedPackageVersions()
        {
            ProjectDependencyPackages =
                new Dictionary<PackageDependencyInfo, PackageDependencyInfo> {
                    {
                        new PackageDependencyInfo {
                            Name = "Microsoft.EntityFrameworkCore.Tools",
                            Version = "[1.0.0-*,)"
                        },
                        new PackageDependencyInfo {
                            Name = "Microsoft.EntityFrameworkCore.Tools",
                            Version = ConstantPackageVersions.AspNetToolsVersion }
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = "Microsoft.AspNetCore.Razor.Tools",
                            Version = "[1.0.0-*,)"
                        },
                        null
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = "Microsoft.AspNetCore.Razor.Design",
                            Version = "[1.0.0-*,)"
                        },
                        null
                    },
                    // I hate to do this, but ordering here matters. The higher version needs to come first, otherwise
                    // the lower version mapping will match to it.
                    {
                        new PackageDependencyInfo
                        {
                            Name = "Microsoft.VisualStudio.Web.CodeGenerators.Mvc",
                            Version = "[1.1.0-*,)"
                        },
                        new PackageDependencyInfo
                        {
                            Name = "Microsoft.VisualStudio.Web.CodeGeneration.Design",
                            Version = ConstantPackageVersions.AspNet110ToolsVersion
                        }
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = "Microsoft.VisualStudio.Web.CodeGenerators.Mvc",
                            Version = "[1.0.0-*,1.1.0)"
                        },
                        new PackageDependencyInfo
                        {
                            Name = "Microsoft.VisualStudio.Web.CodeGeneration.Design",
                            Version = ConstantPackageVersions.AspNetToolsVersion
                        }
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = "Microsoft.AspNetCore.Mvc.Razor.ViewCompilation.Design",
                            Version = "[1.0.0-*,)"
                        },
                        new PackageDependencyInfo {
                            Name = "Microsoft.AspNetCore.Mvc.Razor.ViewCompilation",
                            Version = ConstantPackageVersions.AspNet110ToolsVersion
                        }
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = "Microsoft.VisualStudio.Web.CodeGeneration.Tools",
                            Version = "[1.0.0-*,)"
                        },
                        null
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = TestSdkPackageName,
                            Version = "[1.0.0-*,)"
                        },
                        new PackageDependencyInfo
                        {
                            Name = TestSdkPackageName,
                            Version = ConstantPackageVersions.TestSdkPackageVersion
                        }
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = XUnitPackageName,
                            Version = "[1.0.0-*,)"
                        },
                        new PackageDependencyInfo
                        {
                            Name = XUnitPackageName,
                            Version = ConstantPackageVersions.XUnitPackageVersion
                        }
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = XUnitRunnerPackageName,
                            Version = "[1.0.0-*,)"
                        },
                        new PackageDependencyInfo {
                            Name = XUnitRunnerPackageName,
                            Version = ConstantPackageVersions.XUnitRunnerPackageVersion
                        }
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = MstestTestAdapterName,
                            Version = "[1.0.0-*,)"
                        },
                        new PackageDependencyInfo
                        {
                            Name = MstestTestAdapterName,
                            Version = ConstantPackageVersions.MstestTestAdapterVersion
                        }
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = MstestTestFrameworkName,
                            Version = "[1.0.0-*,)"
                        },
                        new PackageDependencyInfo {
                            Name = MstestTestFrameworkName,
                            Version = ConstantPackageVersions.MstestTestFrameworkVersion
                        }
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = DotnetTestXunit,
                            Version = "[1.0.0-*,)"
                        },
                        null
                    },
                    {
                        new PackageDependencyInfo
                        {
                            Name = DotnetTestMSTest,
                            Version = "[1.0.0-*,)"
                        },
                        null
                    }
            };

            new DotnetSupportedPackageVersionsCsvProvider()
                .AddDotnetSupportedPackageVersions(ProjectDependencyPackages);
        }
    }
}