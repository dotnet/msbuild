// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Tools;
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
        private const string PublishRelease = nameof(PublishRelease);
        private const string PackRelease = nameof(PackRelease);
        private const string publish = nameof(publish);
        private const string pack = nameof(pack);
        private const string Optimize = nameof(Optimize);
        private const string Configuration = nameof(Configuration);
        private const string Release = nameof(Release);
        private const string Debug = nameof(Debug);

        public GivenThatWeWantToTestAMultitargetedSolutionWithPublishReleaseOrPackRelease(ITestOutputHelper log) : base(log)
        {

        }

        /// <summary>
        /// Create a solution with 2 projects, one an exe, the other a library.
        /// </summary>
        /// <param name="log"></param>
        /// <param name="exeProjTfms">A string of TFMs separated by ; for the exe project.</param>
        /// <param name="libraryProjTfms">A string of TFMs separated by ; for the library project.</param>
        /// <param name="PReleaseProperty">The value of the property to set, PublishRelease or PackRelease in this case.</param>
        /// <param name="exePReleaseValue">If "", the property will not be added. This does not undefine the property.</param>
        /// <param name="libraryPReleaseValue">If "", the property will not be added. This does not undefine the property.</param>
        /// <param name="testPath">Use to set a unique folder name for the test, like other test infrastructure code.</param>
        /// <returns></returns>
        internal Tuple<TestSolution, List<TestProject>> Setup(ITestOutputHelper log, List<string> exeProjTfms, List<string> libraryProjTfms, string PReleaseProperty, string exePReleaseValue, string libraryPReleaseValue, [CallerMemberName] string testPath = "")
        {
            // Project Setup
            List<TestProject> testProjects = new List<TestProject>();
            var testProject = new TestProject("TestProject")
            {
                TargetFrameworks = String.Join(";", exeProjTfms),
                IsExe = true
            };
            testProject.RecordProperties("Configuration", "Optimize", PReleaseProperty);
            if (exePReleaseValue != "")
            {
                testProject.AdditionalProperties[PReleaseProperty] = exePReleaseValue;
            }
            var mainProject = _testAssetsManager.CreateTestProject(testProject, callingMethod: testPath, identifier: string.Join("", exeProjTfms) + PReleaseProperty);

            var referencedProject = new TestProject("ReferencedProject")
            {
                TargetFrameworks = String.Join(";", libraryProjTfms),
                IsExe = false
            };
            referencedProject.RecordProperties("Configuration", "Optimize", PReleaseProperty);
            if (libraryPReleaseValue != "")
            {
                referencedProject.AdditionalProperties[PReleaseProperty] = libraryPReleaseValue;
            }
            var secondProject = _testAssetsManager.CreateTestProject(referencedProject, callingMethod: testPath, identifier: string.Join("", libraryProjTfms) + PReleaseProperty);

            List<TestAsset> projects = new List<TestAsset> { mainProject, secondProject };

            // Solution Setup
            var sln = new TestSolution(log, mainProject.TestRoot, projects);
            testProjects.Add(testProject);
            testProjects.Add(referencedProject);
            return new Tuple<TestSolution, List<TestProject>>(sln, testProjects);
        }



        [InlineData(PublishRelease, "net8.0", true)]
        [InlineData(PublishRelease, "-p:TargetFramework=net8.0", false)]
        [Theory]
        public void ItUsesReleaseWithATargetFrameworkOptionNet8ForNet6AndNet7MultitargetingProjectWithPReleaseUndefined(string releaseProperty, string args, bool passDashF)
        {
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework; // Net8 here is a 'net 8+' project
            var expectedConfiguration = Release;
            var expectedTfm = "net8.0";

            var solutionAndProjects = Setup(Log, new List<string> { "net6.0", "net7.0", "net8.0" }, new List<string> { secondProjectTfm }, releaseProperty, "", "");
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log, releaseProperty == PublishRelease ? publish : pack);
            dotnetCommand
                .Execute(passDashF ? "-f" : "", args, sln.SolutionPath)
                .Should()
                .Pass();

            var finalPropertyResults = sln.ProjectProperties(new List<Tuple<string, string>>()
               {
                   new Tuple<string, string>(expectedTfm, expectedConfiguration),
                   new Tuple<string, string>(expectedTfm, expectedConfiguration),
               });

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }

        [Fact]
        public void ItPacksDebugWithSolutionWithNet8ProjectAndNet8tNet7ProjectThatDefinePackReleaseFalse()
        {
            var expectedConfiguration = Debug;

            var solutionAndProjects = Setup(Log, new List<string> { "net8.0" }, new List<string> { "net7.0", "net8.0" }, PackRelease, "false", "false");
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log, pack);
            dotnetCommand
                .Execute(sln.SolutionPath)
                .Should()
                .Pass();

            var finalPropertyResults = sln.ProjectProperties(new List<Tuple<string, string>>()
               {
                   new Tuple<string, string>("net8.0", expectedConfiguration),
                   new Tuple<string, string>("net8.0", expectedConfiguration),
               });

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }

        [Fact]
        public void ItPacksReleaseWithANet8ProjectAndNet7ProjectSolutionWherePackReleaseUndefined()
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework;
            var expectedConfiguration = Release;

            var solutionAndProjects = Setup(Log, new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PackRelease, "", "");
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log, pack);
            dotnetCommand
                .Execute(sln.SolutionPath)
                .Should()
                .Pass();

            var finalPropertyResults = sln.ProjectProperties(new List<Tuple<string, string>>()
               {
                   new Tuple<string, string>(firstProjectTfm, expectedConfiguration),
                   new Tuple<string, string>(secondProjectTfm, expectedConfiguration),
               });

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }

        [InlineData("net7.0", true)]
        [InlineData("-p:TargetFramework=net7.0", false)]
        [Theory]
        public void ItPublishesDebugWithATargetFrameworkOptionNet7ForNet8Net7ProjectAndNet7Net6ProjectSolutionWithPublishReleaseUndefined(string args, bool passDashF)
        {
            var expectedTfm = "net7.0";
            var expectedConfiguration = Debug;

            var solutionAndProjects = Setup(Log, new List<string> { "net6.0", "net7.0" }, new List<string> { "net7.0", "net8.0" }, PublishRelease, "", "");
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(passDashF ? "-f" : "", args, sln.SolutionPath)
                .Should()
                .Pass();

            var finalPropertyResults = sln.ProjectProperties(new List<Tuple<string, string>>()
               {
                   new Tuple<string, string>(expectedTfm, expectedConfiguration),
                   new Tuple<string, string>(expectedTfm, expectedConfiguration),
               });

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }

        [Fact]
        public void ItPublishesReleaseIfNet7DefinesPublishReleaseTrueNet8PlusDefinesNothing()
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework;
            var expectedConfiguration = Release;

            var solutionAndProjects = Setup(Log, new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PublishRelease, "true", "");
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(sln.SolutionPath)
                .Should()
                .Pass();

            var finalPropertyResults = sln.ProjectProperties(new List<Tuple<string, string>>()
               {
                   new Tuple<string, string>(firstProjectTfm, expectedConfiguration),
                   new Tuple<string, string>(secondProjectTfm, expectedConfiguration),
               });

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }


        [InlineData("true", PublishRelease)]
        [InlineData("false", PublishRelease)]
        [InlineData("", PublishRelease)]
        [InlineData("true", PackRelease)]
        // [InlineData("false", PackRelease)] This case we would expect to fail as PackRelease is enabled regardless of TFM.
        [InlineData("", PackRelease)]
        [Theory]
        public void ItPassesWithNet8ProjectAndNet7ProjectSolutionWithPublishReleaseOrPackReleaseUndefined(string releasePropertyValue, string property)
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework; // This should work for Net8+, test name is for brevity

            var expectedConfiguration = Release;
            if (releasePropertyValue == "false" && property == PublishRelease)
            {
                expectedConfiguration = Debug;
            }

            var solutionAndProjects = Setup(Log, new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, property, "", releasePropertyValue);
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log);
            dotnetCommand
                .Execute(property == PublishRelease ? "publish" : "pack", sln.SolutionPath)
                .Should()
                .Pass();

            var finalPropertyResults = sln.ProjectProperties(new List<Tuple<string, string>>()
               {
                   new Tuple<string, string>(firstProjectTfm, expectedConfiguration),
                   new Tuple<string, string>(secondProjectTfm, expectedConfiguration),
               });

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }

        [InlineData("true")]
        [InlineData("false")]
        [InlineData("")]
        [Theory]
        public void ItFailsWithLazyEnvironmentVariableNet8ProjectAndNet7ProjectSolutionWithPublishReleaseUndefined(string publishReleaseValue)
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework; // This should work for Net8+, test name is for brevity

            var solutionAndProjects = Setup(Log, new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PublishRelease, "", publishReleaseValue);
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetPublishCommand(Log);
            dotnetCommand
                .WithEnvironmentVariable("DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS", "true")
                .Execute(sln.SolutionPath)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(String.Format(CommonLocalizableStrings.SolutionProjectConfigurationsConflict, PublishRelease, ""));
        }

        [Fact]
        public void ItFailsIfNet7DefinesPublishReleaseFalseButNet8PlusDefinesNone()
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework; // This should work for Net8+, test name is for brevity

            var solutionAndProjects = Setup(Log, new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PublishRelease, "false", "");
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(sln.SolutionPath)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(String.Format(CommonLocalizableStrings.SolutionProjectConfigurationsConflict, PublishRelease, ""));;
        }

        [Fact]
        public void ItDoesNotErrorWithLegacyNet7ProjectAndNet6ProjectSolutionWithNoPublishRelease()
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = "net6.0";

            var solutionAndProjects = Setup(Log, new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PublishRelease, "", "");
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(sln.SolutionPath)
                .Should()
                .Pass();
        }

        private void VerifyCorrectConfiguration(List<Dictionary<string, string>> finalProperties, string expectedConfiguration)
        {
            string expectedBooleanValue = "true";
            if(expectedConfiguration != "Release")
            {
                expectedBooleanValue = "false";
            }


            Assert.Equal(expectedBooleanValue, finalProperties.First()[Optimize]);
            Assert.Equal(expectedConfiguration, finalProperties.First()[Configuration]);

            Assert.Equal(expectedBooleanValue, finalProperties.ElementAt(1)[Optimize]);
            Assert.Equal(expectedConfiguration, finalProperties.ElementAt(1)[Configuration]);
        }
    }
}
