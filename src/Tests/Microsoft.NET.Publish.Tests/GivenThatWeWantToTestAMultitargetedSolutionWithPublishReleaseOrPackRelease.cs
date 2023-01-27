// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
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
        internal (TestSolution testSolution, List<TestProject> testProjects) Setup(ITestOutputHelper log, List<string> exeProjTfms, List<string> libraryProjTfms, string PReleaseProperty, string exePReleaseValue, string libraryPReleaseValue, [CallerMemberName] string testPath = "")
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

            var libraryProject = new TestProject("LibraryProject")
            {
                TargetFrameworks = String.Join(";", libraryProjTfms),
                IsExe = false
            };
            libraryProject.RecordProperties("Configuration", "Optimize", PReleaseProperty);
            if (libraryPReleaseValue != "")
            {
                libraryProject.AdditionalProperties[PReleaseProperty] = libraryPReleaseValue;
            }
            var secondProject = _testAssetsManager.CreateTestProject(libraryProject, callingMethod: testPath, identifier: string.Join("", libraryProjTfms) + PReleaseProperty);

            List<TestAsset> projects = new List<TestAsset> { mainProject, secondProject };

            // Solution Setup
            var sln = new TestSolution(log, mainProject.TestRoot, projects);
            testProjects.Add(testProject);
            testProjects.Add(libraryProject);
            return new(sln, testProjects);
        }



        [InlineData("-f", $"{ToolsetInfo.CurrentTargetFramework}")]
        [InlineData($"-p:TargetFramework={ToolsetInfo.CurrentTargetFramework}")]
        [Theory]
        public void ItUsesReleaseWithATargetFrameworkOptionNet8ForNet6AndNet7MultitargetingProjectWithPReleaseUndefined(params string[] args)
        {
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework; // Net8 here is a 'net 8+' project
            var expectedConfiguration = Release;
            var expectedTfm = "net8.0";

            var solutionAndProjects = Setup(Log, new List<string> { "net6.0", "net7.0", "net8.0" }, new List<string> { secondProjectTfm }, PublishRelease, "", "");
            var sln = solutionAndProjects.Item1;

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(args.Append(sln.SolutionPath))
                .Should()
                .Pass();

            var finalPropertyResults = sln.ProjectProperties(new()
               {
                   new(expectedTfm, expectedConfiguration),
                   new(expectedTfm, expectedConfiguration),
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

            var finalPropertyResults = sln.ProjectProperties(new()
               {
                   new ("net8.0", expectedConfiguration),
                   new ("net8.0", expectedConfiguration),
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

            var finalPropertyResults = sln.ProjectProperties(new()
               {
                   new (firstProjectTfm, expectedConfiguration),
                   new (secondProjectTfm, expectedConfiguration),
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

            var finalPropertyResults = sln.ProjectProperties(new()
               {
                   new (expectedTfm, expectedConfiguration),
                   new (expectedTfm, expectedConfiguration),
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

            var finalPropertyResults = sln.ProjectProperties(new()
               {
                   new(firstProjectTfm, expectedConfiguration),
                   new(secondProjectTfm, expectedConfiguration),
               });

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }


        [InlineData("true", PublishRelease)]
        [InlineData("false", PublishRelease)]
        [InlineData("", PublishRelease)]
        [InlineData("true", PackRelease)]
        [InlineData("false", PackRelease)] // This case we would expect to fail as PackRelease is enabled regardless of TFM.
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

            if (releasePropertyValue == "false" && property == PackRelease)
            {
                var dotnetCommand = new DotnetCommand(Log);
                dotnetCommand
                    .Execute("pack", sln.SolutionPath)
                    .Should()
                    .Fail();
            }
            else
            {
                var dotnetCommand = new DotnetCommand(Log);
                dotnetCommand
                    .Execute(property == PublishRelease ? "publish" : "pack", sln.SolutionPath)
                    .Should()
                    .Pass();

                var finalPropertyResults = sln.ProjectProperties(new()
               {
                   new(firstProjectTfm, expectedConfiguration),
                   new(secondProjectTfm, expectedConfiguration),
               });

                VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
            }
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
                .HaveStdOutContaining("NETSDK1197");
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
                .HaveStdErrContaining(String.Format(Strings.SolutionProjectConfigurationsConflict, PublishRelease, "")); ;
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

        [Theory]
        [InlineData(PublishRelease)]
        [InlineData(PackRelease)]
        public void It_fails_with_conflicting_PublishRelease_or_PackRelease_values_in_solution_file(string pReleaseVar)
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var solutionAndProjects = Setup(Log, new List<string> { tfm }, new List<string> { tfm }, pReleaseVar, "true", "false");
            var sln = solutionAndProjects.Item1;

            var expectedError = string.Format(Strings.SolutionProjectConfigurationsConflict, pReleaseVar, "");

            new DotnetCommand(Log)
                .Execute("dotnet", pReleaseVar == PublishRelease ? "publish" : "pack", sln.SolutionPath)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(expectedError);
        }

        [Fact]
        public void It_sees_PublishRelease_values_of_hardcoded_sln_argument()
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var solutionAndProjects = Setup(Log, new List<string> { tfm }, new List<string> { tfm }, PublishRelease, "true", "false");
            var sln = solutionAndProjects.Item1;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(Directory.GetParent(sln.SolutionPath).FullName) // code under test looks in CWD, ensure coverage outside this scenario
                .Execute(sln.SolutionPath)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(string.Format(Strings.SolutionProjectConfigurationsConflict, PublishRelease, ""));
        }

        [Fact]
        public void It_doesnt_error_if_environment_variable_opt_out_enabled_but_PublishRelease_conflicts()
        {
            var expectedConfiguration = Debug;
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var solutionAndProjects = Setup(Log, new List<string> { tfm }, new List<string> { tfm }, PublishRelease, "true", "false");
            var sln = solutionAndProjects.Item1;

            new DotnetPublishCommand(Log)
                .WithEnvironmentVariable("DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE", "true")
                .Execute(sln.SolutionPath) // This property won't be set in VS, make sure the error doesn't occur because of this by mimicking behavior.
                .Should()
                .Pass();

            var finalPropertyResults = sln.ProjectProperties(new()
               {
                   new(tfm, expectedConfiguration),
                   new(tfm, expectedConfiguration),
               });

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);

        }

        [Fact]
        public void It_packs_with_Release_on_all_TargetFrameworks_If_8_or_above_is_included()
        {
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = "net7.0;net8.0"
            };
            testProject.RecordProperties("Configuration");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute()
                .Should()
                .Pass();

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: "net7.0", configuration: "Release"); // this will fail if configuration is debug and TFM code didn't work.
            string finalConfiguration = properties["Configuration"];
            finalConfiguration.Should().BeEquivalentTo("Release");
        }

        private void VerifyCorrectConfiguration(List<Dictionary<string, string>> finalProperties, string expectedConfiguration)
        {
            string expectedOptimizeValue = "true";
            if (expectedConfiguration != "Release")
            {
                expectedOptimizeValue = "false";
            }


            Assert.Equal(expectedOptimizeValue, finalProperties[0][Optimize]);
            Assert.Equal(expectedConfiguration, finalProperties[0][Configuration]);

            Assert.Equal(expectedOptimizeValue, finalProperties[1][Optimize]);
            Assert.Equal(expectedConfiguration, finalProperties[1][Configuration]);
        }
    }
}
