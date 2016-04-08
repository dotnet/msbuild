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
        private static string s_testPackageBuildVersionSuffix = "<buildversion>";

        public static string TestPackageBuildVersionSuffix
        {
            get
            {
                return s_testPackageBuildVersionSuffix;
            }
        }

        public static readonly dynamic[] Projects = new[]
        {
            new
            {
                Name = "dotnet-dependency-context-test",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-dependency-context-test",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true
            },
            new
            {
                Name = "dotnet-dependency-tool-invoker",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-dependency-tool-invoker",
                IsApplicable = new Func<bool>(() => CurrentPlatform.IsWindows),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true
            },
            new
            {
                Name = "dotnet-desktop-and-portable",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-desktop-and-portable",
                IsApplicable = new Func<bool>(() => CurrentPlatform.IsWindows),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true
            },
            new
            {
                Name = "dotnet-hello",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-hello/v1/dotnet-hello",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = string.Empty,
                Clean = true
            },
            new
            {
                Name = "dotnet-hello",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-hello/v2/dotnet-hello",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = string.Empty,
                Clean = true
            },
            new
            {
                Name = "dotnet-portable",
                IsTool = true,
                Path = "TestAssets/TestPackages/dotnet-portable",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = string.Empty,
                Clean = true
            },
            new
            {
                Name = "Microsoft.DotNet.Cli.Utils",
                IsTool = true,
                Path = "src/Microsoft.DotNet.Cli.Utils",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false
            },
            new
            {
                Name = "Microsoft.DotNet.ProjectModel",
                IsTool = true,
                Path = "src/Microsoft.DotNet.ProjectModel",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false
            },
            new
            {
                Name = "Microsoft.DotNet.ProjectModel.Loader",
                IsTool = true,
                Path = "src/Microsoft.DotNet.ProjectModel.Loader",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false
            },
            new
            {
                Name = "Microsoft.DotNet.ProjectModel.Workspaces",
                IsTool = true,
                Path = "src/Microsoft.DotNet.ProjectModel.Workspaces",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false
            },
            new
            {
                Name = "Microsoft.DotNet.InternalAbstractions",
                IsTool = true,
                Path = "src/Microsoft.DotNet.InternalAbstractions",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false
            },
            new
            {
                Name = "Microsoft.Extensions.DependencyModel",
                IsTool = true,
                Path = "src/Microsoft.Extensions.DependencyModel",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false
            },
            new
            {
                Name = "Microsoft.Extensions.Testing.Abstractions",
                IsTool = true,
                Path = "src/Microsoft.Extensions.Testing.Abstractions",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false
            },
            new
            {
                Name = "Microsoft.DotNet.Compiler.Common",
                IsTool = true,
                Path = "src/Microsoft.DotNet.Compiler.Common",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false
            },
            new
            {
                Name = "Microsoft.DotNet.Files",
                IsTool = true,
                Path = "src/Microsoft.DotNet.Files",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = false
            },
            new
            {
                Name = "dotnet-compile-fsc",
                IsTool = true,
                Path = "src/dotnet-compile-fsc",
                IsApplicable = new Func<bool>(() => true),
                VersionSuffix = s_testPackageBuildVersionSuffix,
                Clean = true
            }
        };
    }
}