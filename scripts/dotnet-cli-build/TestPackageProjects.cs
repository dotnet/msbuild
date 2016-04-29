using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;

using static Microsoft.DotNet.Cli.Build.FS;
using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;
using static Microsoft.DotNet.Cli.Build.Utils;

namespace Microsoft.DotNet.Cli.Build
{
    public static class TestPackageProjects
    {
        public class TestPackageProject
        {
            public string Name { get; set; }
            public bool IsTool { get; set; }
            public string Path { get; set; }
            public bool IsApplicable { get; set; }
            public string VersionSuffix { get; set; }
            public bool Clean { get; set; }
            public string[] Frameworks { get; set; }
        }

        private static string s_testPackageBuildVersionSuffix = "<buildversion>";

        public static string TestPackageBuildVersionSuffix
        {
            get
            {
                return s_testPackageBuildVersionSuffix;
            }
        }

        public static readonly TestPackageProject[] Projects = new[]
        {
            new TestPackageProject()
            {
                Name = "PackageWithFakeNativeDep",
                IsTool = false,
                Path = "TestAssets/TestPackages/PackageWithFakeNativeDep",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true,
                Frameworks = new [] { "net45" }
            },
            new TestPackageProject()
            {
                Name = "dotnet-dependency-context-test",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-dependency-context-test",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true,
                Frameworks = new [] { "netcoreapp1.0" }
            },
            new TestPackageProject()
            {
                Name = "dotnet-dependency-tool-invoker",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-dependency-tool-invoker",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true,
                Frameworks = new [] { "netcoreapp1.0" }
            },
            new TestPackageProject()
            {
                Name = "dotnet-desktop-and-portable",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-desktop-and-portable",
                IsApplicable = CurrentPlatform.IsWindows,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true,
                Frameworks = new [] { "net451", "netcoreapp1.0" }
            },
            new TestPackageProject()
            {
                Name = "dotnet-desktop-binding-redirects",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-desktop-binding-redirects",
                IsApplicable = CurrentPlatform.IsWindows,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true,
                Frameworks = new [] { "net451" }
            },
            new TestPackageProject()
            {
                Name = "dotnet-hello",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-hello/v1/dotnet-hello",
                IsApplicable =true,
                VersionSuffix = string.Empty,
                Clean = true,
                Frameworks = new [] { "netcoreapp1.0" }
            },
            new TestPackageProject()
            {
                Name = "dotnet-hello",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-hello/v2/dotnet-hello",
                IsApplicable = true,
                VersionSuffix = string.Empty,
                Clean = true,
                Frameworks = new [] { "netcoreapp1.0" }
            },
            new TestPackageProject()
            {
                Name = "dotnet-portable",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-portable",
                IsApplicable = true,
                VersionSuffix = string.Empty,
                Clean = true,
                Frameworks = new [] { "netcoreapp1.0" }
            },
            new TestPackageProject()
            {
                Name = "ToolWithOutputName",
                IsTool = true,
                Path = "TestAssets/TestPackages/ToolWithOutputName",
                IsApplicable = true,
                VersionSuffix = string.Empty,
                Clean = true,
                Frameworks = new [] { "netcoreapp1.0" }
            },
            new TestPackageProject()
            {
                Name = "Microsoft.DotNet.Cli.Utils",
                IsTool = true,
                Path = "src/Microsoft.DotNet.Cli.Utils",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false,
                Frameworks = new [] { "net451", "netstandard1.5" }
            },
            new TestPackageProject()
            {
                Name = "Microsoft.DotNet.ProjectModel",
                IsTool = true,
                Path = "src/Microsoft.DotNet.ProjectModel",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false,
                Frameworks = new [] { "net451", "netstandard1.5" }
            },
            new TestPackageProject()
            {
                Name = "Microsoft.DotNet.ProjectModel.Loader",
                IsTool = true,
                Path = "src/Microsoft.DotNet.ProjectModel.Loader",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false,
                Frameworks = new [] { "netstandard1.5" }
            },
            new TestPackageProject()
            {
                Name = "Microsoft.DotNet.ProjectModel.Workspaces",
                IsTool = true,
                Path = "src/Microsoft.DotNet.ProjectModel.Workspaces",
                IsApplicable =true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false,
                Frameworks = new [] { "netstandard1.5" }
            },
            new TestPackageProject()
            {
                Name = "Microsoft.DotNet.InternalAbstractions",
                IsTool = true,
                Path = "src/Microsoft.DotNet.InternalAbstractions",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false,
                Frameworks = new [] { "net451", "netstandard1.3" }
            },
            new TestPackageProject()
            {
                Name = "Microsoft.Extensions.DependencyModel",
                IsTool = true,
                Path = "src/Microsoft.Extensions.DependencyModel",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false,
                Frameworks = new [] { "net451", "netstandard1.5" }
            },
            new TestPackageProject()
            {
                Name = "Microsoft.Extensions.Testing.Abstractions",
                IsTool = true,
                Path = "src/Microsoft.Extensions.Testing.Abstractions",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false,
                Frameworks = new [] { "net451", "netstandard1.5" }
            },
            new TestPackageProject()
            {
                Name = "Microsoft.DotNet.Compiler.Common",
                IsTool = true,
                Path = "src/Microsoft.DotNet.Compiler.Common",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false,
                Frameworks = new [] { "netstandard1.5" }
            },
            new TestPackageProject()
            {
                Name = "Microsoft.DotNet.Files",
                IsTool = true,
                Path = "src/Microsoft.DotNet.Files",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false,
                Frameworks = new [] { "netstandard1.5" }
            },
            new TestPackageProject()
            {
                Name = "dotnet-compile-fsc",
                IsTool = true,
                Path = "src/dotnet-compile-fsc",
                IsApplicable = true,
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true,
                Frameworks = new [] { "netcoreapp1.0" }
            }
        };
    }
}