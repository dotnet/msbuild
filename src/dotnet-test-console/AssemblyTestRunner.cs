// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test
{
    public class AssemblyTestRunner : IDotnetTestRunner
    {
        private readonly Func<ICommandFactory, string, IDotnetTestRunner> _nextRunner;

        public AssemblyTestRunner(Func<ICommandFactory, string, IDotnetTestRunner> nextRunner)
        {
            _nextRunner = nextRunner;
        }

        public int RunTests(DotnetTestParams dotnetTestParams)
        {
            var commandFactory =
                new CommandFactory();

            var assemblyUnderTest = dotnetTestParams.ProjectOrAssemblyPath;

            return _nextRunner(commandFactory, assemblyUnderTest).RunTests(dotnetTestParams);
        }
    }
}