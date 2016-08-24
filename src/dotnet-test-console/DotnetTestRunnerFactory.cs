// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Test
{
    public class DotnetTestRunnerFactory : IDotnetTestRunnerFactory
    {
        private readonly DotnetTestRunnerResolverFactory _dotnetTestRunnerResolverFactory;

        public DotnetTestRunnerFactory(DotnetTestRunnerResolverFactory dotnetTestRunnerResolverFactory)
        {
            _dotnetTestRunnerResolverFactory = dotnetTestRunnerResolverFactory;
        }

        public IDotnetTestRunner Create(DotnetTestParams dotnetTestParams)
        {
            Func<ICommandFactory, string, NuGetFramework, IDotnetTestRunner> nextTestRunner =
                (commandFactory, assemblyUnderTest, framework) =>
                {
                    var dotnetTestRunnerResolver = _dotnetTestRunnerResolverFactory.Create(dotnetTestParams);

                    IDotnetTestRunner testRunner =
                        new ConsoleTestRunner(dotnetTestRunnerResolver, commandFactory, assemblyUnderTest, framework);
                    if (dotnetTestParams.Port.HasValue)
                    {
                        testRunner = new DesignTimeRunner(dotnetTestRunnerResolver, commandFactory, assemblyUnderTest);
                    }

                    return testRunner;
                };

            return dotnetTestParams.IsTestingAssembly
                ? CreateTestRunnerForAssembly(nextTestRunner)
                : CreateTestRunnerForProjectJson(nextTestRunner);
        }

        private static IDotnetTestRunner CreateTestRunnerForAssembly(
            Func<ICommandFactory, string, NuGetFramework, IDotnetTestRunner> nextTestRunner)
        {
            Func<ICommandFactory, string, IDotnetTestRunner> nextAssemblyTestRunner =
                (commandFactory, assemblyUnderTest) => nextTestRunner(commandFactory, assemblyUnderTest, null);

            return new AssemblyTestRunner(nextAssemblyTestRunner);
        }

        private static IDotnetTestRunner CreateTestRunnerForProjectJson(
            Func<ICommandFactory, string, NuGetFramework, IDotnetTestRunner> nextTestRunner)
        {
            return new ProjectJsonTestRunner(nextTestRunner);
        }
    }
}
