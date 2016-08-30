// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class RunTestsArgumentsBuilder : ITestRunnerArgumentsBuilder
    {
        private readonly string _assemblyUnderTest;
        private readonly int _port;
        private readonly Message _message;

        public RunTestsArgumentsBuilder(string assemblyUnderTest, int port, Message message)
        {
            _assemblyUnderTest = assemblyUnderTest;
            _port = port;
            _message = message;
        }

        public IEnumerable<string> BuildArguments()
        {
            var commandArgs = new List<string>
            {
                _assemblyUnderTest,
                "--designtime",
                "--port",
                $"{_port}",
                "--wait-command"
            };

            return commandArgs;
        }
    }
}
