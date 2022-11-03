// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            TestProject _testProject;
            TestProject _referencedProject;
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            _referencedProject = new TestProject("Library")
            {
                TargetFrameworks = targetFramework,
                IsExe = false
            };

            _testProject = new TestProject("MainProject")
            {
                TargetFrameworks = targetFramework,
                IsExe = true
            };
            _testProject.ReferencedProjects.Add(_referencedProject);
            _referencedProject.AdditionalProperties["AppendRuntimeIdentifierToOutputPath"] = "false";
            _testProject.RecordProperties("RuntimeIdentifier");
            _referencedProject.RecordProperties("RuntimeIdentifier");

            string rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            List<string> args = new List<string>{"/p:PublishSingleFile=true", $"/p:RuntimeIdentifier={rid}", "-bl:C:\\users\\noahgilson\\why_doesnt_this_fail.binlog" };

            var testAsset = _testAssetsManager.CreateTestProject(_testProject);
            new PublishCommand(testAsset)
                .Execute(args.ToArray())
                .Should()
                .Pass();

            var referencedProjProperties = _referencedProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework);
            var mainProjProperties = _testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework);
            Assert.True(mainProjProperties["RuntimeIdentifier"] == rid);
            Assert.True(referencedProjProperties["RuntimeIdentifier"] == "");
        }
    }

}
