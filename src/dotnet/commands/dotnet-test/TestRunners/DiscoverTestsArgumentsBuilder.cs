// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Test
{
    public class DiscoverTestsArgumentsBuilder : ITestRunnerArgumentsBuilder
    {
        private readonly string _assemblyUnderTest;
        private readonly int _port;

        public DiscoverTestsArgumentsBuilder(string assemblyUnderTest, int port)
        {
            _assemblyUnderTest = assemblyUnderTest;
            _port = port;
        }

        public IEnumerable<string> BuildArguments()
        {
            var commandArgs = new List<string>
            {
                _assemblyUnderTest,
                "--list",
                "--designtime",
                "--port",
                $"{_port}"
            };

            return commandArgs;
        }
    }
}
