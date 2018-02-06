// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Transactions;
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
            _pathToPlaceShim = pathToPlaceShim ?? throw new ArgumentNullException(nameof(pathToPlaceShim));
        }

        public void CreateShim(FilePath packageExecutable, string shellCommandName)
        {
            var createShimTransaction = new CreateShimTransaction(
                createShim: locationOfShimDuringTransaction =>
                {
                    EnsureCommandNameUniqueness(shellCommandName);
                    PlaceShim(packageExecutable, shellCommandName, locationOfShimDuringTransaction);
                },
                rollback: locationOfShimDuringTransaction =>
                {
                    foreach (FilePath f in locationOfShimDuringTransaction)
                    {
                        if (File.Exists(f.Value))
                        {
                            File.Delete(f.Value);
                        }
                    }
                });

            using (var transactionScope = new TransactionScope())
            {
                Transaction.Current.EnlistVolatile(createShimTransaction, EnlistmentOptions.None);
                createShimTransaction.CreateShim();

                transactionScope.Complete();
            }
        }

        private void PlaceShim(FilePath packageExecutable, string shellCommandName, List<FilePath> locationOfShimDuringTransaction)
        {
            FilePath shimPath = GetShimPath(shellCommandName);

            if (!Directory.Exists(shimPath.GetDirectoryPath().Value))
            {
                Directory.CreateDirectory(shimPath.GetDirectoryPath().Value);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FilePath windowsConfig = GetWindowsConfigPath(shellCommandName);
                CreateConfigFile(
                    windowsConfig,
                    entryPoint: packageExecutable,
                    runner: "dotnet");

                locationOfShimDuringTransaction.Add(windowsConfig);

                using (var shim = File.Create(shimPath.Value))
                using (var exe = typeof(ShellShimMaker).Assembly.GetManifestResourceStream(LauncherExeResourceName))
                {
                    exe.CopyTo(shim);
                }
                locationOfShimDuringTransaction.Add(shimPath);
            }
            else
            {
                var script = new StringBuilder();
                script.AppendLine("#!/bin/sh");
                script.AppendLine($"dotnet {packageExecutable.ToQuotedString()} \"$@\"");

                File.WriteAllText(shimPath.Value, script.ToString());
                locationOfShimDuringTransaction.Add(shimPath);

                SetUserExecutionPermissionToShimFile(shimPath);
            }
        }

        public void EnsureCommandNameUniqueness(string shellCommandName)
        {
            if (File.Exists(GetShimPath(shellCommandName).Value))
            {
                throw new GracefulException(
                    string.Format(CommonLocalizableStrings.FailInstallToolSameName,
                        shellCommandName));
            }
        }

        internal void CreateConfigFile(FilePath outputPath, FilePath entryPoint, string runner)
        {
            XDocument config;
            using(var resource = typeof(ShellShimMaker).Assembly.GetManifestResourceStream(LauncherConfigResourceName))
            {
                config = XDocument.Load(resource);
            }

            var appSettings = config.Descendants("appSettings").First();
            appSettings.Add(new XElement("add", new XAttribute("key", "entryPoint"), new XAttribute("value", entryPoint.Value)));
            appSettings.Add(new XElement("add", new XAttribute("key", "runner"), new XAttribute("value", runner ?? string.Empty)));
            config.Save(outputPath.Value);
        }

        public void Remove(string shellCommandName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.Delete(GetWindowsConfigPath(shellCommandName).Value);
            }

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

        private FilePath GetWindowsConfigPath(string shellCommandName)
        {
            return new FilePath(GetShimPath(shellCommandName).Value + ".config");
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
