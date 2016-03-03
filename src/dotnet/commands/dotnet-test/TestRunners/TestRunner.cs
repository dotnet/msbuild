// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestRunner : ITestRunner
    {
        private readonly string _testRunner;
        private readonly ICommandFactory _commandFactory;
        private readonly ITestRunnerArgumentsBuilder _argumentsBuilder;

        public TestRunner(
            string testRunner,
            ICommandFactory commandFactory,
            ITestRunnerArgumentsBuilder argumentsBuilder)
        {
            _testRunner = testRunner;
            _commandFactory = commandFactory;
            _argumentsBuilder = argumentsBuilder;
        }

        public void RunTestCommand()
        {
            ExecuteRunnerCommand();
        }

        public TestStartInfo GetProcessStartInfo()
        {
            var command = CreateTestRunnerCommand();

            return command.ToTestStartInfo();
        }

        private void ExecuteRunnerCommand()
        {
            var result = CreateTestRunnerCommand().Execute();

            if (result.ExitCode != 0)
            {
                throw new TestRunnerOperationFailedException(_testRunner, result.ExitCode);
            }
        }

        private ICommand CreateTestRunnerCommand()
        {
            var commandArgs = _argumentsBuilder.BuildArguments();

            return _commandFactory.Create(
                $"dotnet-{_testRunner}",
                commandArgs,
                null,
                null);
        }
    }
}
