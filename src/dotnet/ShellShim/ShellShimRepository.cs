// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ShellShim
{
    internal class ShellShimRepository : IShellShimRepository
    {
        private const string ApphostNameWithoutExtension = "apphost";

        private readonly DirectoryPath _shimsDirectory;
        private readonly string _appHostSourceDirectory;

        public ShellShimRepository(DirectoryPath shimsDirectory, string appHostSourcePath = null)
        {
            _shimsDirectory = shimsDirectory;
            _appHostSourceDirectory = appHostSourcePath ?? Path.Combine(ApplicationEnvironment.ApplicationBasePath,
                    "AppHostTemplate");
        }

        public void CreateShim(FilePath targetExecutablePath, string commandName)
        {
            if (string.IsNullOrEmpty(targetExecutablePath.Value))
            {
                throw new ShellShimException(CommonLocalizableStrings.CannotCreateShimForEmptyExecutablePath);
            }
            if (string.IsNullOrEmpty(commandName))
            {
                throw new ShellShimException(CommonLocalizableStrings.CannotCreateShimForEmptyCommand);
            }

            if (ShimExists(commandName))
            {
                throw new ShellShimException(
                    string.Format(
                        CommonLocalizableStrings.ShellShimConflict,
                        commandName));
            }

            TransactionalAction.Run(
                action: () =>
                {
                    try
                    {
                        if (!Directory.Exists(_shimsDirectory.Value))
                        {
                            Directory.CreateDirectory(_shimsDirectory.Value);
                        }

                        CreateApphostShim(
                                   commandName,
                                   entryPoint: targetExecutablePath);

                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            SetUserExecutionPermission(GetShimPath(commandName));
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    {
                        throw new ShellShimException(
                            string.Format(
                                CommonLocalizableStrings.FailedToCreateShellShim,
                                commandName,
                                ex.Message
                            ),
                            ex);
                    }
                },
                rollback: () => {
                    foreach (var file in GetShimFiles(commandName).Where(f => File.Exists(f.Value)))
                    {
                        File.Delete(file.Value);
                    }
                });
        }

        public void RemoveShim(string commandName)
        {
            var files = new Dictionary<string, string>();
            TransactionalAction.Run(
                action: () => {
                    try
                    {
                        foreach (var file in GetShimFiles(commandName).Where(f => File.Exists(f.Value)))
                        {
                            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                            File.Move(file.Value, tempPath);
                            files[file.Value] = tempPath;
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    {
                        throw new ShellShimException(
                            string.Format(
                                CommonLocalizableStrings.FailedToRemoveShellShim,
                                commandName,
                                ex.Message
                            ),
                            ex);
                    }
                },
                commit: () => {
                    foreach (var value in files.Values)
                    {
                        File.Delete(value);
                    }
                },
                rollback: () => {
                    foreach (var kvp in files)
                    {
                        File.Move(kvp.Value, kvp.Key);
                    }
                });
        }

        private void CreateApphostShim(string commandName, FilePath entryPoint)
        {
            string appHostSourcePath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                appHostSourcePath = Path.Combine(_appHostSourceDirectory, ApphostNameWithoutExtension + ".exe");
            }
            else
            {
                appHostSourcePath = Path.Combine(_appHostSourceDirectory, ApphostNameWithoutExtension);
            }

            var appHostDestinationFilePath = GetShimPath(commandName).Value;
            var appBinaryFilePath = PathUtility.GetRelativePath(appHostDestinationFilePath, entryPoint.Value);

            EmbedAppNameInHost.EmbedAndReturnModifiedAppHostPath(
                appHostSourceFilePath: appHostSourcePath,
                appHostDestinationFilePath: appHostDestinationFilePath,
                appBinaryFilePath: appBinaryFilePath);
        }

        private class StartupOptions
        {
            public string appRoot { get; set; }
        }

        private class RootObject
        {
            public StartupOptions startupOptions { get; set; }
        }

        private bool ShimExists(string commandName)
        {
            return GetShimFiles(commandName).Any(p => File.Exists(p.Value));
        }

        private IEnumerable<FilePath> GetShimFiles(string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
            {
                yield break;
            }

            yield return GetShimPath(commandName);
        }

        private FilePath GetShimPath(string commandName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new FilePath(_shimsDirectory.WithFile(commandName).Value +".exe");
            }
            else
            {
                return _shimsDirectory.WithFile(commandName);
            }
        }

        private static void SetUserExecutionPermission(FilePath path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            CommandResult result = new CommandFactory()
                .Create("chmod", new[] { "u+x", path.Value })
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute();

            if (result.ExitCode != 0)
            {
                throw new ShellShimException(
                    string.Format(CommonLocalizableStrings.FailedSettingShimPermissions, result.StdErr));
            }
        }
    }
}
