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
            CliFolderPathCalculator cliFolderPathCalculator = null,
            bool hasSuperUserAccess = false,
            IEnvironmentProvider environmentProvider = null)
        {
            if (cliFolderPathCalculator == null)
            {
                cliFolderPathCalculator = new CliFolderPathCalculator();
            }

            if (environmentProvider == null)
            {
                environmentProvider = new EnvironmentProvider();
            }

            IEnvironmentPath environmentPath = new DoNothingEnvironmentPath();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                environmentPath = new WindowsEnvironmentPath(
                    cliFolderPathCalculator.ExecutablePackagesPath,
                    Reporter.Output);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && hasSuperUserAccess)
            {
                environmentPath = new LinuxEnvironmentPath(
                    cliFolderPathCalculator.ExecutablePackagesPathInUnix,
                    Reporter.Output,
                    environmentProvider, new FileWrapper());
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && hasSuperUserAccess)
            {
                environmentPath = new OSXEnvironmentPath(
                    executablePath: cliFolderPathCalculator.ExecutablePackagesPathInUnix,
                    reporter: Reporter.Output,
                    environmentProvider: environmentProvider,
                    fileSystem: new FileWrapper());
            }

            return environmentPath;
        }

        public static IEnvironmentPathInstruction CreateEnvironmentPathInstruction(
            CliFolderPathCalculator cliFolderPathCalculator = null,
            IEnvironmentProvider environmentProvider = null)
        {
            return CreateEnvironmentPath(cliFolderPathCalculator, true, environmentProvider);
        }
    }
}
