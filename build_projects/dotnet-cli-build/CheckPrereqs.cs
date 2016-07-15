// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public class CheckPrereqs : Task
    {
        public override bool Execute()
        {
            Run(s => Log.LogMessage(s));

            return true;
        }

        public static void Run(Action<string> logInfo)
        {
            CheckCoreclrPlatformDependencies();
            CheckInstallerBuildPlatformDependencies();

            LocateStage0(logInfo);
        }

        private static void CheckCoreclrPlatformDependencies()
        {
            CheckUbuntuCoreclrAndCoreFxDependencies();
            CheckCentOSCoreclrAndCoreFxDependencies();
        }

        private static void CheckInstallerBuildPlatformDependencies()
        {
            CheckUbuntuDebianPackageBuildDependencies();
        }

        private static void CheckUbuntuCoreclrAndCoreFxDependencies()
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Ubuntu, "14.04"))
            {
                var errorMessageBuilder = new StringBuilder();
                var stage0 = DotNetCli.Stage0.BinPath;

                foreach (var package in PackageDependencies.UbuntuCoreclrAndCoreFxDependencies)
                {
                    if (!AptDependencyUtility.PackageIsInstalled(package))
                    {
                        errorMessageBuilder.Append($"Error: Coreclr package dependency {package} missing.");
                        errorMessageBuilder.Append(Environment.NewLine);
                        errorMessageBuilder.Append($"-> install with apt-get install {package}");
                        errorMessageBuilder.Append(Environment.NewLine);
                    }
                }

                if (errorMessageBuilder.Length > 0)
                {
                    throw new BuildFailureException(errorMessageBuilder.ToString());
                }
            }
        }

        private static void CheckCentOSCoreclrAndCoreFxDependencies()
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.CentOS))
            {
                var errorMessageBuilder = new StringBuilder();

                foreach (var package in PackageDependencies.CentosCoreclrAndCoreFxDependencies)
                {
                    if (!YumDependencyUtility.PackageIsInstalled(package))
                    {
                        errorMessageBuilder.Append($"Error: Coreclr package dependency {package} missing.");
                        errorMessageBuilder.Append(Environment.NewLine);
                        errorMessageBuilder.Append($"-> install with yum install {package}");
                        errorMessageBuilder.Append(Environment.NewLine);
                    }
                }

                if (errorMessageBuilder.Length > 0)
                {
                    throw new BuildFailureException(errorMessageBuilder.ToString());
                }
            }
        }

        private static void CheckUbuntuDebianPackageBuildDependencies()
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Ubuntu, "14.04"))
            {
                var messageBuilder = new StringBuilder();
                var aptDependencyUtility = new AptDependencyUtility();


                foreach (var package in PackageDependencies.DebianPackageBuildDependencies)
                {
                    if (!AptDependencyUtility.PackageIsInstalled(package))
                    {
                        messageBuilder.Append($"Error: Debian package build dependency {package} missing.");
                        messageBuilder.Append(Environment.NewLine);
                        messageBuilder.Append($"-> install with apt-get install {package}");
                        messageBuilder.Append(Environment.NewLine);
                    }
                }

                if (messageBuilder.Length > 0)
                {
                    throw new BuildFailureException(messageBuilder.ToString());
                }
            }
        }

        private static void LocateStage0(Action<string> logInfo)
        {
            // We should have been run in the repo root, so locate the stage 0 relative to current directory
            var stage0 = DotNetCli.Stage0.BinPath;

            if (!Directory.Exists(stage0))
            {
                throw new BuildFailureException($"Stage 0 directory does not exist: {stage0}");
            }

            // Identify the version
            string versionFile = Directory.GetFiles(stage0, ".version", SearchOption.AllDirectories).FirstOrDefault();

            if (string.IsNullOrEmpty(versionFile))
            {
                throw new Exception($"'.version' file not found in '{stage0}' folder");
            }

            var version = File.ReadAllLines(versionFile);
            logInfo($"Using Stage 0 Version: {version[1]}");
        }
    }
}

