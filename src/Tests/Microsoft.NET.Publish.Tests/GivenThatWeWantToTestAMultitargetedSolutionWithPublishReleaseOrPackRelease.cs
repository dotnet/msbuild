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
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
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
            var mainProject = _testAssetsManager.CreateTestProject(_testProject, callingMethod: testPath, identifier: PReleaseProperty);

            var _referencedProject = new TestProject("ReferencedProject")
            {
                TargetFrameworks = secondProjTfm,
                IsExe = false
            };
            _referencedProject.RecordProperties("Configuration", "Optimize", PReleaseProperty);
            _testProject.AdditionalProperties[PReleaseProperty] = secondProjPReleaseValue;
            var secondProject = _testAssetsManager.CreateTestProject(_referencedProject, callingMethod: testPath, identifier: PReleaseProperty);

            List<TestAsset> projects = new List<TestAsset> { mainProject, secondProject };

            // Solution Setup
            var sln = new TestSolution(log, mainProject.TestRoot, projects);
            testProjects.Add(_testProject);
            testProjects.Add(_referencedProject);
            return new Tuple<TestSolution, List<TestProject>>(sln, testProjects);
        }


        [InlineData("PublishRelease")]
        [Theory]
        public void ItUsesReleaseWithNet8PlusAndNet7WhereNoneDefineTheReleaseProperty(string releaseProperty)
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework;
            var expectedConfiguration = "Release";

            var solutionAndProjects = Setup(Log, firstProjectTfm, secondProjectTfm, releaseProperty, "", "");
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log, releaseProperty == "PublishRelease" ? "publish" : "pack");
            dotnetCommand
                .Execute(sln.SolutionPath)
                .Should()
                .Pass();

            var finalPropertyResults = sln.ProjectProperties(new List<Tuple<string, string>>()
               {
                   new Tuple<string, string>(firstProjectTfm, expectedConfiguration),
                   new Tuple<string, string>(secondProjectTfm, expectedConfiguration),
               });

            Assert.Equal("true", finalPropertyResults.First()["Optimize"]);
            Assert.Equal(expectedConfiguration, finalPropertyResults.First()["Configuration"]);

            Assert.Equal("true", finalPropertyResults.ElementAt(1)["Optimize"]);
            Assert.Equal(expectedConfiguration, finalPropertyResults.ElementAt(1)["Configuration"]);
        }

        
        [Fact]
        public void ItWorksWithASpecificSolutionPathGiven()
        {

        }

        [InlineData("-f netx.0")]
        [InlineData("-p:TargetFramework=netx.0")]
        [Theory]
        public void ItPublishesReleaseWithATargetFrameworkOptionNet8ForNet6AndNet7MultitargetingProjectWithPublishReleaseUndefined(string args)
        {
            Console.WriteLine(args);
        }

        [InlineData("-f netx.0")]
        [InlineData("-p:TargetFramework=netx.0")]
        [Theory]
        public void ItPublishesDebugWithATargetFrameworkOptionNet7ForNet8Net7ProjectAndNet7Net6ProjectSolutionWithPublishReleaseUndefined(string args)
        {
            Console.WriteLine(args);
        }

        [Fact]
        public void ItPacksDebugWithMutlitargetingWhereNet8AndNet7ProjectDefinePackReleaseFalse()
        {

        }

        [Fact]
        public void ItPacksDebugWithSolutionWithNet8ProjectAndNet8tNet6ProjectThatDefinePackReleaseFalse()
        {

        }

        [Fact]
        public void ItFailsToPackWithMultiProjectSolutionWithConflictingPackRelease()
        {

        }

        [Fact]
        public void ItPacksReleaseWithANet8ProjectAndNet7ProjectSolutionWherePackReleaseUndefined()
        {

        }

        [Fact]
        public void ItPacksReleaseWithNet8Net7MultitargetProjectWithPackReleaseUndefined()
        {

        }

        [InlineData("PackRelease")]
        [InlineData("PublishRelease")]
        [Theory]
        public void ItDoesntErrorIfEnvironmentVariableOptOutEnabledButReleaseConflicts(string releaseProperty)
        {
            Console.WriteLine(releaseProperty);
        }

        [InlineData("PackRelease")]
        [InlineData("PublishRelease")]
        [Theory]
        public void ItDoesntErrorIfReleaseConflictsInVisualStudio(string releaseProperty)
        {
            Console.WriteLine(releaseProperty);

        }

        [Fact]
        public void ItPublishesWithReleaseWhenNet8ProjectWithNothingAndNet7ProjectWithPublishReleaseDefinitionUsed()
        {

        }

        [InlineData("true")]
        [InlineData("false")]
        [Theory]
        public void ItFailsWhenNet8ProjectWithPublishReleaseDefinedButNet7ProjectDoesNotDefine(string publishReleaseValue)
        {
            Console.WriteLine(publishReleaseValue);

        }


        [Fact]
        public void ItDoesNotErrorWhenNet7AndNet6ProjectDontDefinePublishRelease()
        {

        }

        [InlineData("true")]
        [InlineData("false")]
        [InlineData("")]
        [Theory]
        public void ItFailsWithNet8ProjectAndNet7ProjectSolutionWithPublishReleaseUndefined(string publishReleaseValue)
        {
            Console.WriteLine(publishReleaseValue);

        }

        [Fact]
        public void ItFailsIfNet7DefinesPublishReleaseFalseButNet8PlusDefinesNone()
        {

        }

        [Fact]
        public void ItPublishesReleaseIfNet7DefinesPublishReleaseTrueNet8PlusDfinesNothing()
        {

        }
        
    }
}
