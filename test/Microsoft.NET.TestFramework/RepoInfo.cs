// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Utils;

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

        public static string BinPath
        {
            get
            {
                return Path.Combine(RepoRoot, "bin");
            }
        }

        public static string NuGetFallbackFolder
        {
            get
            {
                return Path.Combine(BinPath, "NuGetFallbackFolder");
            }
        }

        public static string TestsFolder
        {
            get
            {
                return Path.Combine(BinPath, Configuration, "Tests");
            }
        }

        public static string PackagesPath
        {
            get { return Path.Combine(BinPath, Configuration, "Packages"); }
        }

        public static string NuGetCachePath
        {
            get { return Path.Combine(RepoRoot, "packages"); }
        }


        public static string SdksPath
        {
            get { return Path.Combine(BinPath, Configuration, "Sdks"); }
        }

        public static string BuildExtensionsSdkPath
        {
            get { return Path.Combine(SdksPath, "Microsoft.NET.Build.Extensions"); }
        }

        public static string BuildExtensionsMSBuildPath
        {
            get { return Path.Combine(BuildExtensionsSdkPath, "msbuildExtensions", "Microsoft", "Microsoft.NET.Build.Extensions"); }
        }

        public static string CliSdkPath
        {
            get { return Path.Combine(RepoRoot, ".dotnet_cli", "sdk", CliVersion); }
        }

        public static string CliVersion { get; }
            = File.ReadAllText(Path.Combine(RepoRoot, "DotnetCLIVersion.txt")).Trim();

        public static string DotNetHostPath
        {
            get
            {
                return Path.Combine(RepoRoot, ".dotnet_cli", $"dotnet{Constants.ExeSuffix}");
            }
        }

        public static string NuGetExePath
        {
            get
            {
                return Path.Combine(RepoRoot, ".nuget", $"nuget{Constants.ExeSuffix}");
            }
        }

        private static string FindConfigurationInBasePath()
        {
            // assumes tests are always executed from the "bin/$Configuration/Tests" directory
            return new DirectoryInfo(GetBaseDirectory()).Parent.Name;
        }

        // For test purposes, override the implicit .NETCoreApp version for self-contained apps that to builds thare 
        //  (1) different from the fixed framework-dependent defaults (1.0.5, 1.1.2, 2.0.0)
        //  (2) currently available on nuget.org
        //
        // This allows bumping the versions before builds without causing tests to fail.
        public const string ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0 = "1.0.4";
        public const string ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1 = "1.1.1";
        public const string ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp2_0 = "2.0.0-preview2-25407-01";

        public static string GetBaseDirectory()
        {
#if NET451
            string directory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string directory = AppContext.BaseDirectory;
#endif

            return directory;
        }

        public static ICommand AddTestEnvironmentVariables(ICommand command)
        {
            //  Set NUGET_PACKAGES environment variable to match value from build.ps1
            command = command.EnvironmentVariable("NUGET_PACKAGES", RepoInfo.NuGetCachePath);

            command = command.EnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0");

            command = command.EnvironmentVariable("MSBuildSDKsPath", RepoInfo.SdksPath);
            command = command.EnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR", RepoInfo.SdksPath);

            command = command.EnvironmentVariable("NETCoreSdkBundledVersionsProps", Path.Combine(RepoInfo.CliSdkPath, "Microsoft.NETCoreSdk.BundledVersions.props"));

            // The following line can be removed once this file is integrated into MSBuild
            command = command.EnvironmentVariable("CustomAfterMicrosoftCommonTargets", Path.Combine(RepoInfo.BuildExtensionsSdkPath, 
                "msbuildExtensions-ver", "Microsoft.Common.targets", "ImportAfter", "Microsoft.NET.Build.Extensions.targets"));
            command = command.EnvironmentVariable("MicrosoftNETBuildExtensionsTargets", Path.Combine(RepoInfo.BuildExtensionsMSBuildPath, "Microsoft.NET.Build.Extensions.targets"));

            command = command
                .EnvironmentVariable(nameof(ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0), ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0)
                .EnvironmentVariable(nameof(ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1), ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1)
                .EnvironmentVariable(nameof(ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp2_0), ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp2_0);

            return command;
        }
    }
}