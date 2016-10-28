// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    public class NuGetCachePrimer : INuGetCachePrimer
    {
        private readonly ICommandFactory _commandFactory;
        private readonly IDirectory _directory;
        private readonly IFile _file;
        private readonly INuGetPackagesArchiver _nugetPackagesArchiver;
        private readonly INuGetCacheSentinel _nuGetCacheSentinel;

        public NuGetCachePrimer(
            ICommandFactory commandFactory,
            INuGetPackagesArchiver nugetPackagesArchiver,
            INuGetCacheSentinel nuGetCacheSentinel)
            : this(commandFactory,
                nugetPackagesArchiver,
                nuGetCacheSentinel,
                FileSystemWrapper.Default.Directory,
                FileSystemWrapper.Default.File)
        {
        }

        internal NuGetCachePrimer(
            ICommandFactory commandFactory,
            INuGetPackagesArchiver nugetPackagesArchiver,
            INuGetCacheSentinel nuGetCacheSentinel,
            IDirectory directory,
            IFile file)
        {
            _commandFactory = commandFactory;
            _directory = directory;
            _nugetPackagesArchiver = nugetPackagesArchiver;
            _nuGetCacheSentinel = nuGetCacheSentinel;
            _file = file;
        }

        public void PrimeCache()
        {
            if(SkipPrimingTheCache())
            {
                return;
            }

            var extractedPackagesArchiveDirectory = _nugetPackagesArchiver.ExtractArchive();

            PrimeCacheUsingArchive(extractedPackagesArchiveDirectory);
        }

        private bool SkipPrimingTheCache()
        {
            return !_file.Exists(_nugetPackagesArchiver.NuGetPackagesArchive);
        }

        private void PrimeCacheUsingArchive(string extractedPackagesArchiveDirectory)
        {
            using (var temporaryDotnetNewDirectory = _directory.CreateTemporaryDirectory())
            {
                var workingDirectory = temporaryDotnetNewDirectory.DirectoryPath;
                var createProjectSucceeded = CreateTemporaryProject(workingDirectory);

                if (createProjectSucceeded)
                {
                    var restoreProjectSucceeded =
                        RestoreTemporaryProject(extractedPackagesArchiveDirectory, workingDirectory);
                    if (restoreProjectSucceeded)
                    {
                        _nuGetCacheSentinel.CreateIfNotExists();
                    }
                }
            }
        }

        private bool CreateTemporaryProject(string workingDirectory)
        {
            return RunCommand("new", Enumerable.Empty<string>(), workingDirectory);
        }

        private bool RestoreTemporaryProject(string extractedPackagesArchiveDirectory, string workingDirectory)
        {
            return RunCommand(
                "restore",
                new[] {"-s", extractedPackagesArchiveDirectory},
                workingDirectory);
        }

        private bool RunCommand(string commandToExecute, IEnumerable<string> args, string workingDirectory)
        {
            var command = _commandFactory
                .Create(commandToExecute, args)
                .WorkingDirectory(workingDirectory)
                .CaptureStdOut()
                .CaptureStdErr();

            var commandResult = command.Execute();

            if (commandResult.ExitCode != 0)
            {
                Reporter.Verbose.WriteLine(commandResult.StdErr);
                Reporter.Error.WriteLine(
                    $"Failed to create prime the NuGet cache. {commandToExecute} failed with: {commandResult.ExitCode}");
            }

            return commandResult.ExitCode == 0;
        }
    }
}
