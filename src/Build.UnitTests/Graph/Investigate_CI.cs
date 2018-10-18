// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Xunit;

namespace Microsoft.Build.Graph.UnitTests
{
    public class Investigate_CI
    {
        [Fact]
        public void FailingTest()
        {
            using (var env = TestEnvironment.Create())
            using (var buildManager = new BuildManager())
            {
                var projectFile = env.CreateFile()
                    .Path;

                File.WriteAllText(projectFile, @"
                <Project>
                    <Target Name='SelfTarget'>
                    </Target>
                </Project>");

                var buildParameters = new BuildParameters();

                var rootRequest = new BuildRequestData(
                    projectFile,
                    new Dictionary<string, string>(),
                    MSBuildConstants.CurrentToolsVersion,
                    new[] {"SelfTarget"},
                    null);

                try
                {
                    buildManager.BeginBuild(buildParameters);

                    buildManager.BuildRequest(rootRequest);
                }
                finally
                {
                    buildManager.EndBuild();
                }
            }
        }
    }
}
