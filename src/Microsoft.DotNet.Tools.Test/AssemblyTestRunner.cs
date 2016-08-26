// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
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
            var assembly = new FileInfo(dotnetTestParams.ProjectOrAssemblyPath);
            var publishDirectory = assembly.Directory.FullName;
            var applicationName = Path.GetFileNameWithoutExtension(dotnetTestParams.ProjectOrAssemblyPath);

            var commandFactory = new PublishedPathCommandFactory(publishDirectory, applicationName);

            var assemblyUnderTest = dotnetTestParams.ProjectOrAssemblyPath;

            return _nextRunner(commandFactory, assemblyUnderTest).RunTests(dotnetTestParams);
        }
    }
}