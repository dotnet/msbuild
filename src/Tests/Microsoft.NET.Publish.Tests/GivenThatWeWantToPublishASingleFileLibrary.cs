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
        TestProject _testProject;
        TestProject _referencedProject;

        public GivenThatWeWantToPublishASingleFileLibrary(ITestOutputHelper log) : base(log)
        {
            _referencedProject = new TestProject("Library")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false
            };

            _testProject = new TestProject("MainProject")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };
            _testProject.ReferencedProjects.Add(_referencedProject);

            _testProject.RecordProperties("RuntimeIdentifier");
            _referencedProject.RecordProperties("RuntimeIdentifier");
        }

        TestAsset Publish(List<string> args, [CallerMemberName] string callingMethod = "", string testIdentifierName = "")
        {
            var testAsset = _testAssetsManager.CreateTestProject(_testProject, callingMethod: callingMethod, identifier: testIdentifierName);

            new PublishCommand(testAsset)
                .Execute(args.ToArray())
                .Should()
                .Pass();

            return testAsset;
        }

        [WindowsOnlyFact]
        // Tests regression on https://github.com/dotnet/sdk/pull/28484
        public void ItPublishesSuccessfullyWithRIDAndPublishSingleFileLibrary()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            string rid = "win-x86";

            List<string> args = new List<string>{"/p:PublishSingleFile=true", $"/p:RuntimeIdenitifer={rid}"};
            var testAsset = Publish(args, testIdentifierName: "PublishSingleFileLibrary");

            var assetFolder = testAsset.TestRoot;
            var ridlessLibraryDllPath = Path.Combine(assetFolder, "Library", "bin", "Debug", targetFramework, "Library.dll");
            Assert.True(File.Exists(ridlessLibraryDllPath));

            var properties = _referencedProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework);
            Assert.True(properties["RuntimeIdentifier"] == "");
        }
    }

}
