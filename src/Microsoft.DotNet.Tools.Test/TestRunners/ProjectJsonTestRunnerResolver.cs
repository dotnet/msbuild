// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public class ProjectJsonTestRunnerResolver : ITestRunnerResolver
    {
        private Project _project;

        public ProjectJsonTestRunnerResolver(Project project)
        {
            _project = project;
        }

        public string ResolveTestRunner()
        {
            return string.IsNullOrEmpty(_project.TestRunner) ? null : $"dotnet-test-{_project.TestRunner}";
        }
    }
}
