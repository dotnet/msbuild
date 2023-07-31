// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            if (OperatingSystem.IsWindows())
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
            else if (OperatingSystem.IsLinux() && isDotnetBeingInvokedFromNativeInstaller)
            {
                environmentPath = new LinuxEnvironmentPath(
                    CliFolderPathCalculator.ToolsShimPathInUnix,
                    Reporter.Output,
                    environmentProvider,
                    new FileWrapper());
            }
            else if (OperatingSystem.IsMacOS() && isDotnetBeingInvokedFromNativeInstaller)
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

            if (OperatingSystem.IsMacOS() && ZshDetector.IsZshTheUsersShell(environmentProvider))
            {
                return new OsxZshEnvironmentPathInstruction(
                    executablePath: CliFolderPathCalculator.ToolsShimPathInUnix,
                    reporter: Reporter.Output,
                    environmentProvider: environmentProvider);
            }

			if (OperatingSystem.IsWindows())
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
