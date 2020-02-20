// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.CommandFactory;
using System.Linq;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    /// <summary>
    /// Use stage 2 (the build result) to run restore instead of stage 0 (the last version of SDK) 
    /// to have more coverage
    /// 
    /// ProjectRestorer will stage0, so cannot be covered in tests. Try to hit the same code path of ProjectRestorer as possible.
    /// </summary>
    internal class Stage2ProjectRestorer : IProjectRestorer
    {
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;
        private readonly IEnumerable<string> _additionalRestoreArguments;
        private readonly ITestOutputHelper _log;

        public Stage2ProjectRestorer(ITestOutputHelper log, IReporter reporter = null,
            IEnumerable<string> additionalRestoreArguments = null)
        {
            _log = log;
            _additionalRestoreArguments = additionalRestoreArguments;
            _reporter = reporter ?? Reporter.Output;
            _errorReporter = reporter ?? Reporter.Error;
        }

        public void Restore(FilePath project,
            PackageLocation packageLocation,
            string verbosity = null)
        {
            var argsToPassToRestore = new List<string>();

            argsToPassToRestore.Add(project.Value);
            if (packageLocation.NugetConfig != null)
            {
                argsToPassToRestore.Add("--configfile");
                argsToPassToRestore.Add(packageLocation.NugetConfig.Value.Value);
            }

            argsToPassToRestore.AddRange(new List<string>
            {
                "--runtime",
                Constants.AnyRid,
                "-v:quiet"
            });

            if (_additionalRestoreArguments != null)
            {
                argsToPassToRestore.AddRange(_additionalRestoreArguments);
            }

            var command =
                new DotnetRestoreCommand(_log).
                Execute(argsToPassToRestore);

            if (!string.IsNullOrWhiteSpace(command.StdOut) && (_reporter != null))
            {
                ProjectRestorer.WriteLine(_reporter, command.StdOut, project);
            }

            if (!string.IsNullOrWhiteSpace(command.StdErr) && (_reporter != null))
            {
                ProjectRestorer.WriteLine(_errorReporter, command.StdErr, project);
            }

            if (command.ExitCode != 0)
            {
                throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
            }
        }
    }
}
