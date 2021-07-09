// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Workloads.Workload.Restore
{
    internal class WorkloadRestoreCommand : CommandBase
    {
        private readonly string _configFilePath;
        private readonly IReporter _errorReporter;
        private readonly IFileSystem _fileSystem;
        private readonly IReporter _reporter;
        private readonly string[] _sources;
        private readonly string _verbosity;

        public WorkloadRestoreCommand(
            ParseResult result,
            IFileSystem fileSystem = null,
            IReporter reporter = null)
            : base(result)
        {
            _fileSystem = fileSystem ?? new FileSystemWrapper();

            _reporter = reporter ?? Reporter.Output;
            _errorReporter = reporter ?? Reporter.Error;

            _configFilePath = result.ValueForOption<string>(WorkloadRestoreCommandParser.ConfigOption);
            _sources = result.ValueForOption<string[]>(WorkloadRestoreCommandParser.SourceOption);
            _verbosity =
                Enum.GetName(result.ValueForOption<VerbosityOptions>(WorkloadRestoreCommandParser.VerbosityOption));
        }

        public override int Execute()
        {
            _reporter.WriteLine("WIP workload restore stub");
            return 0;
        }
    }
}
