// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public class DotnetTestRunnerResolverFactory
    {
        private readonly IProjectReader _projectReader;

        public DotnetTestRunnerResolverFactory(IProjectReader projectReader)
        {
            _projectReader = projectReader;
        }

        public ITestRunnerNameResolver Create(DotnetTestParams dotnetTestParams)
        {
            var testRunnerResolver = dotnetTestParams.IsTestingAssembly ?
                GetAssemblyTestRunnerResolver(dotnetTestParams) :
                GetProjectJsonTestRunnerResolver(dotnetTestParams);

            return testRunnerResolver;
        }

        private ITestRunnerNameResolver GetAssemblyTestRunnerResolver(DotnetTestParams dotnetTestParams)
        {
            ITestRunnerNameResolver testRunnerNameResolver = null;
            if (dotnetTestParams.HasTestRunner)
            {
                testRunnerNameResolver = new ParameterTestRunnerNameResolver(dotnetTestParams.TestRunner);
            }
            else
            {
                testRunnerNameResolver = new AssemblyTestRunnerNameResolver(dotnetTestParams.ProjectOrAssemblyPath);
            }

            return testRunnerNameResolver;
        }

        private ITestRunnerNameResolver GetProjectJsonTestRunnerResolver(DotnetTestParams dotnetTestParams)
        {
            var project = _projectReader.ReadProject(dotnetTestParams.ProjectOrAssemblyPath);
            return new ProjectJsonTestRunnerNameResolver(project);
        }
    }
}