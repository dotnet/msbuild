// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Xsl;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    internal static class EnvironmentPathFactory
    {
        public static IEnvironmentPath CreateEnvironmentPath(
            bool isDotnetBeingInvokedFromNativeInstaller = false,
            IEnvironmentProvider environmentProvider = null)
        {
            if (environmentProvider == null)
            {
                environmentProvider = new EnvironmentProvider();
            }

            IEnvironmentPath environmentPath = new DoNothingEnvironmentPath();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (isDotnetBeingInvokedFromNativeInstaller)
                {
                    // On Windows MSI will in charge of appending ToolShimPath
                    environmentPath = new DoNothingEnvironmentPath();
                }
                else
                {
                    environmentPath = new WindowsEnvironmentPath(
                        CliFolderPathCalculator.ToolsShimPath,
                        CliFolderPathCalculator.WindowsNonExpandedToolsShimPath,
                        environmentProvider,
                        new WindowsRegistryEnvironmentPathEditor(),
                        Reporter.Output);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && isDotnetBeingInvokedFromNativeInstaller)
            {
                environmentPath = new LinuxEnvironmentPath(
                    CliFolderPathCalculator.ToolsShimPathInUnix,
                    Reporter.Output,
                    environmentProvider,
                    new FileWrapper());
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && isDotnetBeingInvokedFromNativeInstaller)
            {
                environmentPath = new OsxBashEnvironmentPath(
                    executablePath: CliFolderPathCalculator.ToolsShimPathInUnix,
                    reporter: Reporter.Output,
                    environmentProvider: environmentProvider,
                    fileSystem: new FileWrapper());
            }

            return environmentPath;
        }

        public static IEnvironmentPathInstruction CreateEnvironmentPathInstruction(
            IEnvironmentProvider environmentProvider = null)
        {
            if (environmentProvider == null)
            {
                environmentProvider = new EnvironmentProvider();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && ZshDetector.IsZshTheUsersShell(environmentProvider))
            {
                return new OsxZshEnvironmentPathInstruction(
                    executablePath: CliFolderPathCalculator.ToolsShimPathInUnix,
                    reporter: Reporter.Output,
                    environmentProvider: environmentProvider);
            }

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsEnvironmentPath(
                    CliFolderPathCalculator.ToolsShimPath,
                    nonExpandedPackageExecutablePath: CliFolderPathCalculator.WindowsNonExpandedToolsShimPath,
                    expandedEnvironmentReader: environmentProvider,
                    environmentPathEditor: new WindowsRegistryEnvironmentPathEditor(),
                    reporter: Reporter.Output);
            }

            return CreateEnvironmentPath(true, environmentProvider);
        }
    }
}
