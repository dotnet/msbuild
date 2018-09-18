// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Graph.UnitTests
{
    public class ProjectGraphTests
    {
        [Fact]
        public void TestGraphWithSingleNode()
        {
            string projectContents = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <PropertyGroup>
                    <TestProperty>value</TestProperty>
                  </PropertyGroup>

                  <Target Name='Build'>
                    <Message Text='Building test project'/>
                  </Target>
                </Project>
                ";
            using (var env = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles testProject =
                    env.CreateTestProjectWithFiles(projectContents, Array.Empty<string>());
                var projectGraph = new ProjectGraph(testProject.ProjectFile);
                projectGraph.ProjectNodes.Count.ShouldBe(1);
                Project projectNode = projectGraph.ProjectNodes.First().Project;
                projectNode.FullPath.ShouldBe(testProject.ProjectFile);
            }
        }
    }

}
