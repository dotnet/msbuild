// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishASingleFileLibrary : SdkTest
    {
        public GivenThatWeWantToPublishASingleFileLibrary(ITestOutputHelper log) : base(log)
        {

        }

        [WindowsOnlyFact]
        // Tests regression on https://github.com/dotnet/sdk/pull/28484
        public void ItPublishesSuccessfullyWithRIDAndPublishSingleFileLibrary()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            TestProject referencedProject = new TestProject("Library")
            {
                TargetFrameworks = targetFramework,
                IsExe = false
            };

            TestProject testProject = new TestProject("MainProject")
            {
                TargetFrameworks = targetFramework,
                IsExe = true
            };
            testProject.ReferencedProjects.Add(referencedProject);
            testProject.RecordProperties("RuntimeIdentifier");
            referencedProject.RecordProperties("RuntimeIdentifier");

            string rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            List<string> args = new List<string>{"/p:PublishSingleFile=true", $"/p:RuntimeIdentifier={rid}"};

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            new PublishCommand(testAsset)
                .Execute(args.ToArray())
                .Should()
                .Pass();

            var referencedProjProperties = referencedProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework);
            var mainProjProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework);
            Assert.True(mainProjProperties["RuntimeIdentifier"] == rid);
            Assert.True(referencedProjProperties["RuntimeIdentifier"] == "");
        }
    }

}
