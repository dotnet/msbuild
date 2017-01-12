// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class PackageDependencyInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string PrivateAssets { get; set; }
    }

    internal class PackageConstants
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

        public static readonly IDictionary<PackageDependencyInfo, PackageDependencyInfo> ProjectDependencyPackages =
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
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.NETCore.App",
                        Version = "[,1.0.3)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.NETCore.App",
                        Version = "1.0.3"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "NETStandard.Library",
                        Version = "[,1.6.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "NETStandard.Library",
                        Version = "1.6.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Antiforgery",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Antiforgery",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.Abstractions",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.Abstractions",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.ApiExplorer",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.ApiExplorer",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.Core",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.Core",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.Cors",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.Cors",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.DataAnnotations",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.DataAnnotations",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.Formatters.Json",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.Formatters.Json",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.Formatters.Xml",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.Formatters.Xml",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.Localization",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.Localization",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.Razor",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.Razor",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.Razor.Host",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.Razor.Host",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.TagHelpers",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.TagHelpers",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.ViewFeatures",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.ViewFeatures",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Mvc.WebApiCompatShim",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Mvc.WebApiCompatShim",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Routing",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Routing",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Routing.Abstractions",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Routing.Abstractions",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Server.Kestrel",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Server.Kestrel",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.AspNetCore.Server.Kestrel.Https",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.AspNetCore.Server.Kestrel.Https",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.InMemory",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.InMemory",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.Relational",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.Relational",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.Relational.Design",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.Relational.Design",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.Relational.Design.Specification.Tests",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.Relational.Design.Specification.Tests",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.Relational.Specification.Tests",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.Relational.Specification.Tests",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.Specification.Tests",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.Specification.Tests",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.Sqlite",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.Sqlite",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.Sqlite.Design",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.Sqlite.Design",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.SqlServer",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.SqlServer",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.EntityFrameworkCore.SqlServer.Design",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.EntityFrameworkCore.SqlServer.Design",
                        Version =ConstantPackageVersions.EntityFramework101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.NETCore.JIT",
                        Version = "[,1.0.5)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.NETCore.JIT",
                        Version = "1.0.5"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.NETCore.Runtime.CoreCLR",
                        Version = "[,1.0.5)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.NETCore.Runtime.CoreCLR",
                        Version = "1.0.5"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.NETCore.DotNetHost",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.NETCore.DotNetHost",
                        Version = "1.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.NETCore.DotNetHostPolicy",
                        Version = "[,1.0.3)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.NETCore.DotNetHostPolicy",
                        Version = "1.0.3"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.NETCore.DotNetHostResolver",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.NETCore.DotNetHostResolver",
                        Version = "1.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.NETCore.Platforms",
                        Version = "[,1.0.2)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.NETCore.Platforms",
                        Version = "1.0.2"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.NETCore.Targets",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.NETCore.Targets",
                        Version = "1.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.NETCore.Windows.ApiSets",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.NETCore.Windows.ApiSets",
                        Version = "1.0.1"
                    }
                },
                                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Net.Http",
                        Version = "[,4.1.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Net.Http",
                        Version = "4.1.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.AppContext",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.AppContext",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Buffers",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Buffers",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Collections",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Collections",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Collections.Concurrent",
                        Version = "[,4.0.12)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Collections.Concurrent",
                        Version = "4.0.12"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Collections.Immutable",
                        Version = "[,1.2.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Collections.Immutable",
                        Version = "1.2.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.ComponentModel",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.ComponentModel",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.ComponentModel.Annotations",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.ComponentModel.Annotations",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Console",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Console",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Diagnostics.Debug",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Diagnostics.Debug",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Diagnostics.DiagnosticSource",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Diagnostics.DiagnosticSource",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Diagnostics.FileVersionInfo",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Diagnostics.FileVersionInfo",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Diagnostics.Process",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Diagnostics.Process",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Diagnostics.StackTrace",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Diagnostics.StackTrace",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Diagnostics.Tools",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Diagnostics.Tools",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Diagnostics.Tracing",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Diagnostics.Tracing",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Dynamic.Runtime",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Dynamic.Runtime",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Globalization",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Globalization",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Globalization.Calendars",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Globalization.Calendars",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Globalization.Extensions",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Globalization.Extensions",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.IO",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.IO",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.IO.Compression",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.IO.Compression",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.IO.Compression.ZipFile",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.IO.Compression.ZipFile",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.IO.MemoryMappedFiles",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.IO.MemoryMappedFiles",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.IO.UnmanagedMemoryStream",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.IO.UnmanagedMemoryStream",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Linq",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Linq",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Linq.Expressions",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Linq.Expressions",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Linq.Parallel",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Linq.Parallel",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Linq.Queryable",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Linq.Queryable",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Net.NameResolution",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Net.NameResolution",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Net.Primitives",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Net.Primitives",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Net.Requests",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Net.Requests",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Net.Security",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Net.Security",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Net.Sockets",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Net.Sockets",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Net.WebHeaderCollection",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Net.WebHeaderCollection",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Numerics.Vectors",
                        Version = "[,4.1.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Numerics.Vectors",
                        Version = "4.1.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.ObjectModel",
                        Version = "[,4.0.12)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.ObjectModel",
                        Version = "4.0.12"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Reflection",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Reflection",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Reflection.DispatchProxy",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Reflection.DispatchProxy",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Reflection.Emit",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Reflection.Emit",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Reflection.Emit.ILGeneration",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Reflection.Emit.ILGeneration",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Reflection.Emit.Lightweight",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Reflection.Emit.Lightweight",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Reflection.Extensions",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Reflection.Extensions",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Reflection.Metadata",
                        Version = "[,1.3.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Reflection.Metadata",
                        Version = "1.3.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Reflection.Primitives",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Reflection.Primitives",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Reflection.TypeExtensions",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Reflection.TypeExtensions",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Resources.Reader",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Resources.Reader",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Resources.ResourceManager",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Resources.ResourceManager",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Runtime",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Runtime",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Runtime.Extensions",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Runtime.Extensions",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Runtime.Handles",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Runtime.Handles",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Runtime.InteropServices",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Runtime.InteropServices",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Runtime.InteropServices.RuntimeInformation",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Runtime.InteropServices.RuntimeInformation",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Runtime.Loader",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Runtime.Loader",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Runtime.Numerics",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Runtime.Numerics",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Claims",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Claims",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Cryptography.Algorithms",
                        Version = "[,4.2.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Cryptography.Algorithms",
                        Version = "4.2.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Cryptography.Cng",
                        Version = "[,4.2.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Cryptography.Cng",
                        Version = "4.2.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Cryptography.Csp",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Cryptography.Csp",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Cryptography.Encoding",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Cryptography.Encoding",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Cryptography.OpenSsl",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Cryptography.OpenSsl",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Cryptography.Primitives",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Cryptography.Primitives",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Cryptography.X509Certificates",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Cryptography.X509Certificates",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Principal",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Principal",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Security.Principal.Windows",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Security.Principal.Windows",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Text.Encoding",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Text.Encoding",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Text.Encoding.CodePages",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Text.Encoding.CodePages",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Text.Encoding.Extensions",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Text.Encoding.Extensions",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Text.RegularExpressions",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Text.RegularExpressions",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Threading",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Threading",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Threading.Overlapped",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Threading.Overlapped",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Threading.Tasks",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Threading.Tasks",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Threading.Tasks.Dataflow",
                        Version = "[,4.6.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Threading.Tasks.Dataflow",
                        Version = "4.6.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Threading.Tasks.Extensions",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Threading.Tasks.Extensions",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Threading.Tasks.Parallel",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Threading.Tasks.Parallel",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Threading.Thread",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Threading.Thread",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Threading.ThreadPool",
                        Version = "[,4.0.10)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Threading.ThreadPool",
                        Version = "4.0.10"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Threading.Timer",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Threading.Timer",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Xml.ReaderWriter",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Xml.ReaderWriter",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Xml.XDocument",
                        Version = "[,4.0.11)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Xml.XDocument",
                        Version = "4.0.11"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Xml.XmlDocument",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Xml.XmlDocument",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Xml.XPath",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Xml.XPath",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.Xml.XPath.XmlDocument",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.Xml.XPath.XmlDocument",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "runtime.native.System",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "runtime.native.System",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "runtime.native.System.IO.Compression",
                        Version = "[,4.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "runtime.native.System.IO.Compression",
                        Version = "4.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "runtime.native.System.Net.Http",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "runtime.native.System.Net.Http",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "runtime.native.System.Net.Security",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "runtime.native.System.Net.Security",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "runtime.native.System.Security.Cryptography",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "runtime.native.System.Security.Cryptography",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Libuv",
                        Version = "[,1.9.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Libuv",
                        Version = "1.9.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.CodeAnalysis.Analyzers",
                        Version = "[,1.1.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.CodeAnalysis.Analyzers",
                        Version = "1.1.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.CodeAnalysis.Common",
                        Version = "[,1.3.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.CodeAnalysis.Common",
                        Version = "1.3.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.CodeAnalysis.CSharp",
                        Version = "[,1.3.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.CodeAnalysis.CSharp",
                        Version = "1.3.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.CodeAnalysis.VisualBasic",
                        Version = "[,1.3.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.CodeAnalysis.VisualBasic",
                        Version = "1.3.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.CSharp",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.CSharp",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.VisualBasic",
                        Version = "[,10.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.VisualBasic",
                        Version = "10.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.Win32.Primitives",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.Win32.Primitives",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.Win32.Registry",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.Win32.Registry",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.IO.FileSystem",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.IO.FileSystem",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.IO.FileSystem.Primitives",
                        Version = "[,4.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.IO.FileSystem.Primitives",
                        Version = "4.0.1"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "System.IO.FileSystem.Watcher",
                        Version = "[,4.0.0)"
                    },
                    new PackageDependencyInfo {
                        Name = "System.IO.FileSystem.Watcher",
                        Version = "4.0.0"
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.Extensions.Logging",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.Extensions.Logging",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.Extensions.Logging.Console",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.Extensions.Logging.Console",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.Extensions.Logging.Debug",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.Extensions.Logging.Debug",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.Extensions.Configuration.Json",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.Extensions.Configuration.Json",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
                {
                    new PackageDependencyInfo
                    {
                        Name = "Microsoft.Extensions.Configuration.UserSecrets",
                        Version = "[,1.0.1)"
                    },
                    new PackageDependencyInfo {
                        Name = "Microsoft.Extensions.Configuration.UserSecrets",
                        Version =ConstantPackageVersions.AspNet101PackagesVersion
                    }
                },
        };

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
    }
}