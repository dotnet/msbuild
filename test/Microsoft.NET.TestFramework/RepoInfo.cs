// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.NET.TestFramework
{
    public class RepoInfo
    {
        private static string s_repoRoot;

        private static string s_configuration;

        public static string RepoRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(s_repoRoot))
                {
                    return s_repoRoot;
                }

                string directory = GetBaseDirectory();

                while (!Directory.Exists(Path.Combine(directory, ".git")) && directory != null)
                {
                    directory = Directory.GetParent(directory).FullName;
                }

                if (directory == null)
                {
                    throw new DirectoryNotFoundException("Cannot find the git repository root");
                }

                s_repoRoot = directory;
                return s_repoRoot;
            }
        }

        private static string Configuration
        {
            get
            {
                if (string.IsNullOrEmpty(s_configuration))
                {
                    s_configuration = FindConfigurationInBasePath();
                }

                return s_configuration;
            }
        }

        private static string BinPath
        {
            get
            {
                return Path.Combine(RepoRoot, "bin");
            }
        }

        public static string NuGetExePath
        {
            get
            {
                return Path.Combine(RepoRoot, ".nuget", $"nuget{Constants.ExeSuffix}");
            }
        }

        private static ToolsetInfo _toolsetUnderTest;
        public static ToolsetInfo ToolsetUnderTest
        {
            get
            {
                if (_toolsetUnderTest == null)
                {
                    _toolsetUnderTest = new ToolsetInfo();
                    _toolsetUnderTest.CliVersion = File.ReadAllText(Path.Combine(RepoRoot, "DotnetCLIVersion.txt")).Trim();
                    _toolsetUnderTest.DotNetHostPath = Path.Combine(RepoRoot, ".dotnet_cli", $"dotnet{Constants.ExeSuffix}");
                    _toolsetUnderTest.SdksPath = Path.Combine(BinPath, Configuration, "Sdks");
                    _toolsetUnderTest.BuildExtensionsSdkPath = Path.Combine(_toolsetUnderTest.SdksPath, "Microsoft.NET.Build.Extensions");
                    _toolsetUnderTest.BuildExtensionsMSBuildPath = Path.Combine(_toolsetUnderTest.BuildExtensionsSdkPath, "msbuildExtensions", "Microsoft", "Microsoft.NET.Build.Extensions");
                }
                return _toolsetUnderTest;
            }
        }

        private static TestContext _testExecutionInfo;
        public static TestContext TestExecutionInfo
        {
            get
            {
                if (_testExecutionInfo == null)
                {
                    _testExecutionInfo = new TestContext();
                    _testExecutionInfo.TestExecutionDirectory = AppContext.BaseDirectory;
                    _testExecutionInfo.TestAssetsDirectory = Path.Combine(RepoRoot, "TestAssets");
                    _testExecutionInfo.NuGetCachePath = Path.Combine(RepoRoot, "packages");
                    _testExecutionInfo.NuGetFallbackFolder = Path.Combine(BinPath, "NuGetFallbackFolder");
                }
                return _testExecutionInfo;
            }
        }

        private static string FindConfigurationInBasePath()
        {
            // assumes tests are always executed from the "bin/$Configuration/Tests" directory
            return new DirectoryInfo(GetBaseDirectory()).Parent.Name;
        }

        public static string GetBaseDirectory()
        {
#if NET451
            string directory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string directory = AppContext.BaseDirectory;
#endif

            return directory;
        }
    }
}
