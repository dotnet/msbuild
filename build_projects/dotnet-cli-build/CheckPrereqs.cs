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
            return CheckCoreclrPlatformDependencies() &&
                   CheckInstallerBuildPlatformDependencies() && 
                   LocateStage0();
        }

        private bool CheckCoreclrPlatformDependencies()
        {
            return CheckUbuntuCoreclrAndCoreFxDependencies() &&
                   CheckCentOSCoreclrAndCoreFxDependencies();
        }

        private bool CheckInstallerBuildPlatformDependencies()
        {
            return CheckUbuntuDebianPackageBuildDependencies();
        }

        private bool CheckUbuntuCoreclrAndCoreFxDependencies()
        {
            bool isSuccessful = true;

            if (CurrentPlatform.IsPlatform(BuildPlatform.Ubuntu, "14.04"))
            {
                var stage0 = DotNetCli.Stage0.BinPath;

                foreach (var package in PackageDependencies.UbuntuCoreclrAndCoreFxDependencies)
                {
                    if (!AptDependencyUtility.PackageIsInstalled(package))
                    {
                        isSuccessful = false;

                        Log.LogError($"Coreclr package dependency {package} missing. Install with `apt-get install {package}`");
                    }
                }
            }

            return isSuccessful;
        }

        private bool CheckCentOSCoreclrAndCoreFxDependencies()
        {
            var isSuccessful = true; 

            if (CurrentPlatform.IsPlatform(BuildPlatform.CentOS))
            {
                foreach (var package in PackageDependencies.CentosCoreclrAndCoreFxDependencies)
                {
                    if (!YumDependencyUtility.PackageIsInstalled(package))
                    {
                        isSuccessful = false;

                        Log.LogError($"Coreclr package dependency {package} missing. Install with yum install {package}");
                    }
                }
            }

            return isSuccessful;
        }

        private bool CheckUbuntuDebianPackageBuildDependencies()
        {
            var isSuccessful = true;

            if (CurrentPlatform.IsPlatform(BuildPlatform.Ubuntu, "14.04"))
            {
                var aptDependencyUtility = new AptDependencyUtility();

                foreach (var package in PackageDependencies.DebianPackageBuildDependencies)
                {
                    if (!AptDependencyUtility.PackageIsInstalled(package))
                    {
                        isSuccessful = false;

                        Log.LogError($"Debian package build dependency {package} missing. Install with apt-get install {package}");
                    }
                }
            }

            return isSuccessful;
        }

        private bool LocateStage0()
        {
            // We should have been run in the repo root, so locate the stage 0 relative to current directory
            var stage0 = DotNetCli.Stage0.BinPath;

            if (!Directory.Exists(stage0))
            {
                Log.LogError($"Stage 0 directory does not exist: {stage0}");

                return false;
            }

            // Identify the version
            string versionFile = Directory.GetFiles(stage0, ".version", SearchOption.AllDirectories).FirstOrDefault();

            if (string.IsNullOrEmpty(versionFile))
            {
                Log.LogError($"'.version' file not found in '{stage0}' folder");

                return false;
            }

            var version = File.ReadAllLines(versionFile);
            
            Log.LogMessage($"Using Stage 0 Version: {version[1]}");

            return true;
        }
    }
}

