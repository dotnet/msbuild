// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Graph.UnitTests
{
    public class Investigate_CI
    {
        private readonly ITestOutputHelper _testOutput;

        public Investigate_CI(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public void FailingTest()
        {
            try
            {
                EnvironmentWriter.OutputWriter = s => _testOutput.WriteLine(s);

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
            finally
            {
                EnvironmentWriter.OutputWriter = null;
            }
        }
    }
}
