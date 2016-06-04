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
        private const string NUGET_SOURCE_PARAMETER = "-s";
        private readonly ICommandFactory _commandFactory;
        private readonly IDirectory _directory;
        private readonly INuGetPackagesArchiver _nugetPackagesArchiver;

        public NuGetCachePrimer(ICommandFactory commandFactory, INuGetPackagesArchiver nugetPackagesArchiver)
            : this(commandFactory, nugetPackagesArchiver, FileSystemWrapper.Default.Directory)
        {
        }

        internal NuGetCachePrimer(
            ICommandFactory commandFactory,
            INuGetPackagesArchiver nugetPackagesArchiver,
            IDirectory directory)
        {
            _commandFactory = commandFactory;
            _directory = directory;
            _nugetPackagesArchiver = nugetPackagesArchiver;
        }

        public void PrimeCache()
        {
            var pathToPackagesArchive = _nugetPackagesArchiver.ExtractArchive();

            PrimeCacheUsingArchive(pathToPackagesArchive);
        }

        private void PrimeCacheUsingArchive(string pathToPackagesArchive)
        {
            using (var temporaryDotnetNewDirectory = _directory.CreateTemporaryDirectory())
            {
                var workingDirectory = temporaryDotnetNewDirectory.DirectoryPath;
                var dotnetNewSucceeded = CreateTemporaryProject(workingDirectory);

                if (dotnetNewSucceeded)
                {
                    RestoreTemporaryProject(pathToPackagesArchive, workingDirectory);
                }
            }
            // -- PrimeCache(<path to archive>)
            //      (done) Create temporary project under a temporary folder using dotnet new
            //      (done) Restore that project using dotnet restore -s parameter pointing to the <path to archive>
            //      Create sentinel
            //      (done) Delete temporary folder (should be done automatically if using abstraction).
        }

        private bool CreateTemporaryProject(string workingDirectory)
        {
            return RunCommand("dotnet new", Enumerable.Empty<string>(), workingDirectory);
        }

        private bool RestoreTemporaryProject(string pathToPackagesArchive, string workingDirectory)
        {
            return RunCommand(
                "dotnet restore",
                new[] {NUGET_SOURCE_PARAMETER, $"{pathToPackagesArchive}"},
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
