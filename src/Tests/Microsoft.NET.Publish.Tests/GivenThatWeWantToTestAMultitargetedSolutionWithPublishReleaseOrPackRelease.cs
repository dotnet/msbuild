// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{

    public class GivenThatWeWantToTestAMultitargetedSolutionWithPublishReleaseOrPackRelease : SdkTest
    {

        public GivenThatWeWantToTestAMultitargetedSolutionWithPublishReleaseOrPackRelease(ITestOutputHelper log) : base(log)
        {

        }

        internal Tuple<TestSolution, List<TestProject>> Setup(ITestOutputHelper log, string firstProjTfm, string secondProjTfm, string PReleaseProperty, string firstProjPReleaseValue, string secondProjPReleaseValue, [CallerMemberName] string testPath = "")
        {
            // Project Setup
            List<TestProject> testProjects = new List<TestProject>();
            var _testProject = new TestProject("TestProject")
            {
                TargetFrameworks = firstProjTfm,
                IsExe = true
            };
            _testProject.RecordProperties("Configuration", "Optimize", PReleaseProperty);
            _testProject.AdditionalProperties[PReleaseProperty] = firstProjPReleaseValue;
            var mainProject = _testAssetsManager.CreateTestProject(_testProject, testPath);

            var _referencedProject = new TestProject("ReferencedProject")
            {
                TargetFrameworks = secondProjTfm,
                IsExe = false
            };
            _referencedProject.RecordProperties("Configuration", "Optimize", PReleaseProperty);
            _testProject.AdditionalProperties[PReleaseProperty] = secondProjPReleaseValue;
            var secondProject = _testAssetsManager.CreateTestProject(_referencedProject, testPath);

            List<TestAsset> projects = new List<TestAsset> { mainProject, secondProject };

            // Solution Setup
            var sln = new TestSolution(log, testPath, projects);
            testProjects.Add(_testProject);
            testProjects.Add(_referencedProject);
            return new Tuple<TestSolution, List<TestProject>>(sln, testProjects);
        }

        [Theory]
        ["PublishRelease"]
        ["PackRelease"]
        public void ItUsesReleaseWithNet8PlusAndNet7WhereNoneDefineTheReleaseProperty(string releaseProperty)
        {
            var solutionAndProjects = Setup(Log, "net7.0", ToolsetInfo.CurrentTargetFramework, releaseProperty, "", "");
            var sln = solutionAndProjects.Item1;
            var testProjects = solutionAndProjects.Item2;

            var dotnetCommand = new DotnetCommand(Log, releaseProperty == "PublishRelease" ? "publish" : "pack");
            dotnetCommand.Execute(sln.SolutionPath)
                .Should()
                .Pass();

            // Check properties

        }

        [Theory]
        ["PublishRelease"]
        ["PackRelease"]
        public void ItUsesDebugIfNet8PlusDefinesFalseAndNet7DefinesNothing(string releaseProperty)
        {

        }

        [Theory]
        ["PublishRelease"]
        ["PackRelease"]
        public void ItFailsIfNet7DefinesReleasePropertyFalseButNet8PlusDefinesNone(string releaseProperty)
        {

        }

        [Theory]
        ["PublishRelease"]
        ["PackRelease"]
        public void ItUsesReleaseIfNet7DefinesReleasePropertyAndNet8PlusDfinesNothing(string releaseProperty)
        {

        }
    }
}
