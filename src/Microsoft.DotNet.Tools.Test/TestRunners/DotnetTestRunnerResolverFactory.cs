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

        public ITestRunnerResolver Create(DotnetTestParams dotnetTestParams)
        {
            var testRunnerResolver = dotnetTestParams.IsTestingAssembly ?
                GetAssemblyTestRunnerResolver(dotnetTestParams) :
                GetProjectJsonTestRunnerResolver(dotnetTestParams);

            return testRunnerResolver;
        }

        private ITestRunnerResolver GetAssemblyTestRunnerResolver(DotnetTestParams dotnetTestParams)
        {
            ITestRunnerResolver testRunnerResolver = null;
            if (dotnetTestParams.HasTestRunner)
            {
                testRunnerResolver = new ParameterTestRunnerResolver(dotnetTestParams.TestRunner);
            }
            else
            {
                testRunnerResolver = new AssemblyTestRunnerResolver(dotnetTestParams.ProjectOrAssemblyPath);
            }

            return testRunnerResolver;
        }

        private ITestRunnerResolver GetProjectJsonTestRunnerResolver(DotnetTestParams dotnetTestParams)
        {
            var project = _projectReader.ReadProject(dotnetTestParams.ProjectOrAssemblyPath);
            return new ProjectJsonTestRunnerResolver(project);
        }
    }
}