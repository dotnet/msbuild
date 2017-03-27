// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools.Common;
using NuGet.Packaging;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    public class PackagedCommandSpecFactory : IPackagedCommandSpecFactory
    {
        private const string PackagedCommandSpecFactoryName = "packagedcommandspecfactory";

        private Action<string, IList<string>> _addAdditionalArguments;

        internal PackagedCommandSpecFactory(Action<string, IList<string>> addAdditionalArguments = null)
        {
            _addAdditionalArguments = addAdditionalArguments;
        }

        public CommandSpec CreateCommandSpecFromLibrary(
            LockFileTargetLibrary toolLibrary,
            string commandName,
            IEnumerable<string> commandArguments,
            IEnumerable<string> allowedExtensions,
            string nugetPackagesRoot,
            CommandResolutionStrategy commandResolutionStrategy,
            string depsFilePath,
            string runtimeConfigPath)
        {
            return CreateCommandSpecFromLibrary(
                toolLibrary,
                commandName,
                commandArguments,
                allowedExtensions,
                new List<string> { nugetPackagesRoot },
                commandResolutionStrategy,
                depsFilePath,
                runtimeConfigPath);
        }

        public CommandSpec CreateCommandSpecFromLibrary(
            LockFileTargetLibrary toolLibrary,
            string commandName,
            IEnumerable<string> commandArguments,
            IEnumerable<string> allowedExtensions,
            IEnumerable<string> packageFolders,
            CommandResolutionStrategy commandResolutionStrategy,
            string depsFilePath,
            string runtimeConfigPath)
        {
            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.AttemptingToFindCommand,
                PackagedCommandSpecFactoryName,
                commandName,
                toolLibrary.Name));

            var toolAssembly = toolLibrary?.RuntimeAssemblies
                    .FirstOrDefault(r => Path.GetFileNameWithoutExtension(r.Path) == commandName);

            if (toolAssembly == null)
            {
                Reporter.Verbose.WriteLine(string.Format(
                    LocalizableStrings.FailedToFindToolAssembly,
                    PackagedCommandSpecFactoryName,
                    commandName));

                return null;
            }

            var commandPath = GetCommandFilePath(packageFolders, toolLibrary, toolAssembly);

            if (!File.Exists(commandPath))
            {
                Reporter.Verbose.WriteLine(string.Format(
                    LocalizableStrings.FailedToFindCommandPath,
                    PackagedCommandSpecFactoryName,
                    commandPath));

                return null;
            }

            return CreateCommandSpecWrappingWithMuxerIfDll(
                commandPath,
                commandArguments,
                depsFilePath,
                commandResolutionStrategy,
                packageFolders,
                runtimeConfigPath);
        }

        private string GetCommandFilePath(
            IEnumerable<string> packageFolders,
            LockFileTargetLibrary toolLibrary,
            LockFileItem runtimeAssembly)
        {
            var packageFoldersCount = packageFolders.Count();
            var userPackageFolder = packageFoldersCount == 1 ? string.Empty : packageFolders.First();
            var fallbackPackageFolders = packageFoldersCount > 1 ? packageFolders.Skip(1) : packageFolders;

            var packageDirectory = new FallbackPackagePathResolver(userPackageFolder, fallbackPackageFolders)
                .GetPackageDirectory(toolLibrary.Name, toolLibrary.Version);

            if (packageDirectory == null)
            {
                throw new GracefulException(string.Format(
                    LocalizableStrings.CommandAssembliesNotFound,
                    toolLibrary.Name));
            }

            var filePath = Path.Combine(
                packageDirectory,
                PathUtility.GetPathWithDirectorySeparator(runtimeAssembly.Path));

            return filePath;
        }

        private CommandSpec CreateCommandSpecWrappingWithMuxerIfDll(
            string commandPath,
            IEnumerable<string> commandArguments,
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy,
            IEnumerable<string> packageFolders,
            string runtimeConfigPath)
        {
            var commandExtension = Path.GetExtension(commandPath);

            if (commandExtension == FileNameSuffixes.DotNet.DynamicLib)
            {
                return CreatePackageCommandSpecUsingMuxer(
                    commandPath,
                    commandArguments,
                    depsFilePath,
                    commandResolutionStrategy,
                    packageFolders,
                    runtimeConfigPath);
            }

            return CreateCommandSpec(commandPath, commandArguments, commandResolutionStrategy);
        }

        private CommandSpec CreatePackageCommandSpecUsingMuxer(
            string commandPath,
            IEnumerable<string> commandArguments,
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy,
            IEnumerable<string> packageFolders,
            string runtimeConfigPath)
        {
            var host = string.Empty;
            var arguments = new List<string>();

            var muxer = new Muxer();

            host = muxer.MuxerPath;
            if (host == null)
            {
                throw new Exception(LocalizableStrings.UnableToLocateDotnetMultiplexer);
            }

            arguments.Add("exec");

            if (runtimeConfigPath != null)
            {
                arguments.Add("--runtimeconfig");
                arguments.Add(runtimeConfigPath);
            }

            if (depsFilePath != null)
            {
                arguments.Add("--depsfile");
                arguments.Add(depsFilePath);
            }

            foreach (var packageFolder in packageFolders)
            {
                arguments.Add("--additionalprobingpath");
                arguments.Add(packageFolder);
            }

            if(_addAdditionalArguments != null)
            {
                _addAdditionalArguments(commandPath, arguments);
            }

            arguments.Add(commandPath);
            arguments.AddRange(commandArguments);

            return CreateCommandSpec(host, arguments, commandResolutionStrategy);
        }

        private CommandSpec CreateCommandSpec(
            string commandPath,
            IEnumerable<string> commandArguments,
            CommandResolutionStrategy commandResolutionStrategy)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(commandArguments);

            return new CommandSpec(commandPath, escapedArgs, commandResolutionStrategy);
        }
    }
}
