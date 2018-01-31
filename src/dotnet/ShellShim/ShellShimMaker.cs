// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    public class ShellShimMaker : IShellShimMaker
    {
        private const string LauncherExeResourceName = "Microsoft.DotNet.Tools.Launcher.Executable";
        private const string LauncherConfigResourceName = "Microsoft.DotNet.Tools.Launcher.Config";

        private readonly string _pathToPlaceShim;

        public ShellShimMaker(string pathToPlaceShim)
        {
            _pathToPlaceShim =
                pathToPlaceShim ?? throw new ArgumentNullException(nameof(pathToPlaceShim));
        }

        public void CreateShim(FilePath packageExecutable, string shellCommandName)
        {
            FilePath shimPath = GetShimPath(shellCommandName);

            if (!Directory.Exists(shimPath.GetDirectoryPath().Value))
            {
                Directory.CreateDirectory(shimPath.GetDirectoryPath().Value);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CreateConfigFile(shimPath.Value + ".config", entryPoint: packageExecutable, runner: "dotnet");
                using (var shim = File.Create(shimPath.Value))
                using (var exe = typeof(ShellShimMaker).Assembly.GetManifestResourceStream(LauncherExeResourceName))
                {
                    exe.CopyTo(shim);
                }
            }
            else
            {
                var script = new StringBuilder();
                script.AppendLine("#!/bin/sh");
                script.AppendLine($"dotnet {packageExecutable.ToQuotedString()} \"$@\"");

                File.WriteAllText(shimPath.Value, script.ToString());

                SetUserExecutionPermissionToShimFile(shimPath);
            }
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

        internal void CreateConfigFile(string outputPath, FilePath entryPoint, string runner)
        {
            XDocument config;
            using (var resource = typeof(ShellShimMaker).Assembly.GetManifestResourceStream(LauncherConfigResourceName))
            {
                config = XDocument.Load(resource);
            }

            var appSettings = config.Descendants("appSettings").First();
            appSettings.Add(new XElement("add", new XAttribute("key", "entryPoint"), new XAttribute("value", entryPoint.Value)));
            appSettings.Add(new XElement("add", new XAttribute("key", "runner"), new XAttribute("value", runner ?? string.Empty)));
            config.Save(outputPath);
        }

        public void Remove(string shellCommandName)
        {
            File.Delete(GetShimPath(shellCommandName).Value);
        }

        private FilePath GetShimPath(string shellCommandName)
        {
            var scriptPath = Path.Combine(_pathToPlaceShim, shellCommandName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                scriptPath += ".exe";
            }

            return new FilePath(scriptPath);
        }

        private static void SetUserExecutionPermissionToShimFile(FilePath scriptPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            CommandResult result = new CommandFactory()
                .Create("chmod", new[] { "u+x", scriptPath.Value })
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
