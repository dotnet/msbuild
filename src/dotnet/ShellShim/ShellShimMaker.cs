// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    public class ShellShimMaker
    {
        private readonly string _pathToPlaceShim;

        public ShellShimMaker(string pathToPlaceShim)
        {
            _pathToPlaceShim =
                pathToPlaceShim ?? throw new ArgumentNullException(nameof(pathToPlaceShim));
        }

        public void CreateShim(string packageExecutablePath, string shellCommandName)
        {
            var packageExecutable = new FilePath(packageExecutablePath);

            var script = new StringBuilder();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                script.AppendLine("@echo off");
                script.AppendLine($"dotnet {packageExecutable.ToQuotedString()} %*");
            }
            else
            {
                script.AppendLine("#!/bin/sh");
                script.AppendLine($"dotnet {packageExecutable.ToQuotedString()} \"$@\"");
            }

            FilePath scriptPath = GetScriptPath(shellCommandName);
            File.WriteAllText(scriptPath.Value, script.ToString());

            SetUserExecutionPermissionToShimFile(scriptPath);
        }

        public void EnsureCommandNameUniqueness(string shellCommandName)
        {
            if (File.Exists(Path.Combine(_pathToPlaceShim, shellCommandName)))
            {
                throw new GracefulException(
                    string.Format(CommonLocalizableStrings.FailInstallToolSameName,
                        shellCommandName));
            }
        }

        public void Remove(string shellCommandName)
        {
            File.Delete(GetScriptPath(shellCommandName).Value);
        }

        private FilePath GetScriptPath(string shellCommandName)
        {
            var scriptPath = Path.Combine(_pathToPlaceShim, shellCommandName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                scriptPath += ".cmd";
            }

            return new FilePath(scriptPath);
        }

        private static void SetUserExecutionPermissionToShimFile(FilePath scriptPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            CommandResult result = new CommandFactory()
                .Create("chmod", new[] {"u+x", scriptPath.Value})
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute();

            if (result.ExitCode != 0)
            {
                throw new GracefulException(
                    string.Format(CommonLocalizableStrings.FailInstallToolPermission, result.StdErr,
                        result.StdOut));
            }
        }
    }
}
