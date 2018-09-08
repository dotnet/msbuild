// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
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
            string testDirectory = Path.GetTempPath();
            string projectPath = Path.Combine(testDirectory, "ProjectGraphTest.proj");
            File.WriteAllText(projectPath, projectContents);
            var projectGraph = new ProjectGraph(projectPath);
            
            Assert.True(projectGraph.ProjectNodes.Count == 1);
            Project projectNode = projectGraph.ProjectNodes.First().Project;
            Assert.True(projectNode.FullPath == projectPath);
        }
    }

}
