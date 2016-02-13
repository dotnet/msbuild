// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestRunnerFactory : ITestRunnerFactory
    {
        private readonly string _testRunner;
        private readonly ICommandFactory _commandFactory;

        public TestRunnerFactory(string testRunner, ICommandFactory commandFactory)
        {
            _testRunner = testRunner;
            _commandFactory = commandFactory;
        }

        public ITestRunner CreateTestRunner(ITestRunnerArgumentsBuilder argumentsBuilder)
        {
            return new TestRunner(_testRunner, _commandFactory, argumentsBuilder);
        }
    }
}
