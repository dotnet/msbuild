// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public class ConsoleTestRunner : BaseDotnetTestRunner
    {
        internal override int DoRunTests(ProjectContext projectContext, DotnetTestParams dotnetTestParams)
        {
            var commandFactory =
                new ProjectDependenciesCommandFactory(
                    projectContext.TargetFramework,
                    dotnetTestParams.Config,
                    dotnetTestParams.Output,
                    dotnetTestParams.BuildBasePath,
                    projectContext.ProjectDirectory);

            return commandFactory.Create(
                    GetCommandName(projectContext.ProjectFile.TestRunner),
                    GetCommandArgs(projectContext, dotnetTestParams),
                    projectContext.TargetFramework,
                    dotnetTestParams.Config)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute()
                .ExitCode;
        }

        private IEnumerable<string> GetCommandArgs(ProjectContext projectContext, DotnetTestParams dotnetTestParams)
        {
            var commandArgs = new List<string>
            {
                new AssemblyUnderTest(projectContext, dotnetTestParams).Path
            };

            commandArgs.AddRange(dotnetTestParams.RemainingArguments);

            return commandArgs;
        }

        private static string GetCommandName(string testRunner)
        {
            return $"dotnet-test-{testRunner}";
        }
    }
}
