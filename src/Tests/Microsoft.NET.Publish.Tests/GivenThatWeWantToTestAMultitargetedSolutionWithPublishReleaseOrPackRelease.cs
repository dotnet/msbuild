// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.NET.Build.Tasks;

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
        /// <param name="callingMethod">Use to set a unique folder name for the test, like other test infrastructure code.</param>
        /// <returns></returns>
        internal (TestAsset testAsset, List<TestProject> testProjects) Setup(List<string> exeProjTfms, List<string> libraryProjTfms, string PReleaseProperty,
            string exePReleaseValue, string libraryPReleaseValue, [CallerMemberName] string callingMethod = "", string identifier = "")
        {
            // Project Setup
            List<TestProject> testProjects = new();
            var testProject = new TestProject("TestProject")
            {
                TargetFrameworks = string.Join(";", exeProjTfms),
                IsExe = true
            };
            testProject.RecordProperties("Configuration", "Optimize", PReleaseProperty);
            if (exePReleaseValue != "")
            {
                testProject.AdditionalProperties[PReleaseProperty] = exePReleaseValue;
            }

            var libraryProject = new TestProject("LibraryProject")
            {
                TargetFrameworks = string.Join(";", libraryProjTfms),
                IsExe = false
            };
            libraryProject.RecordProperties("Configuration", "Optimize", PReleaseProperty);
            if (libraryPReleaseValue != "")
            {
                libraryProject.AdditionalProperties[PReleaseProperty] = libraryPReleaseValue;
            }

            testProjects.Add(testProject);
            testProjects.Add(libraryProject);
            var testAsset = _testAssetsManager.CreateTestProjects(testProjects, callingMethod: callingMethod, identifier: identifier);

            return (testAsset, testProjects);
        }

        [InlineData("-f", $"{ToolsetInfo.CurrentTargetFramework}")]
        [InlineData($"-p:TargetFramework={ToolsetInfo.CurrentTargetFramework}")]
        [Theory]
        public void ItUsesReleaseWithATargetFrameworkOptionNet8ForNet6AndNet7MultitargetingProjectWithPReleaseUndefined(params string[] args)
        {
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework; // Net8 here is a 'net 8+' project
            var expectedConfiguration = Release;
            var expectedTfm = "net8.0";

            var (testAsset, testProjects) = Setup(new List<string> { "net6.0", "net7.0", "net8.0" }, new List<string> { secondProjectTfm }, PublishRelease, "", "", identifier: string.Join('-', args));

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(args.Append(testAsset.Path))
                .Should()
                .Pass();

            var finalPropertyResults = new List<Dictionary<string, string>>();
            foreach (var testProject in testProjects)
            {
                finalPropertyResults.Add(testProject.GetPropertyValues(testAsset.Path, expectedTfm, expectedConfiguration));
            }

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }

        [Fact]
        public void ItPacksDebugWithSolutionWithNet8ProjectAndNet8tNet7ProjectThatDefinePackReleaseFalse()
        {
            var expectedConfiguration = Debug;

            var (testAsset, testProjects) = Setup(new List<string> { "net8.0" }, new List<string> { "net7.0", "net8.0" }, PackRelease, "false", "false");

            var dotnetCommand = new DotnetCommand(Log, pack);
            dotnetCommand
                .Execute(testAsset.Path)
                .Should()
                .Pass();

            var finalPropertyResults = new List<Dictionary<string, string>>();
            foreach (var testProject in testProjects)
            {
                finalPropertyResults.Add(testProject.GetPropertyValues(testAsset.Path, "net8.0", expectedConfiguration));
            }

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }

        [Fact]
        public void ItPacksReleaseWithANet8ProjectAndNet7ProjectSolutionWherePackReleaseUndefined()
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework;
            var expectedConfiguration = Release;

            var (testAsset, testProjects) = Setup(new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PackRelease, "", "");

            var dotnetCommand = new DotnetCommand(Log, pack);
            dotnetCommand
                .Execute(testAsset.Path)
                .Should()
                .Pass();

            var finalPropertyResults = new List<Dictionary<string, string>>();
            foreach (var testProject in testProjects)
            {
                finalPropertyResults.Add(testProject.GetPropertyValues(testAsset.Path, testProject == testProjects[0] ? firstProjectTfm : secondProjectTfm, expectedConfiguration));
            }

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }

        [InlineData("net7.0", true)]
        [InlineData("-p:TargetFramework=net7.0", false)]
        [Theory]
        public void ItPublishesDebugWithATargetFrameworkOptionNet7ForNet8Net7ProjectAndNet7Net6ProjectSolutionWithPublishReleaseUndefined(string args, bool passDashF)
        {
            var expectedTfm = "net7.0";
            var expectedConfiguration = Debug;

            var (testAsset, testProjects) = Setup(new List<string> { "net6.0", "net7.0" }, new List<string> { "net7.0", "net8.0" }, PublishRelease, "", "");

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(passDashF ? "-f" : "", args, testAsset.Path)
                .Should()
                .Pass();

            var finalPropertyResults = new List<Dictionary<string, string>>();
            foreach (var testProject in testProjects)
            {
                finalPropertyResults.Add(testProject.GetPropertyValues(testAsset.Path, expectedTfm, expectedConfiguration));
            }

            VerifyCorrectConfiguration(finalPropertyResults, expectedConfiguration);
        }

        [Fact]
        public void ItPublishesReleaseIfNet7DefinesPublishReleaseTrueNet8PlusDefinesNothing()
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = ToolsetInfo.CurrentTargetFramework;
            var expectedConfiguration = Release;

            var (testAsset, testProjects) = Setup(new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PublishRelease, "true", "");

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(testAsset.Path)
                .Should()
                .Pass();

            var finalPropertyResults = new List<Dictionary<string, string>>();
            foreach (var testProject in testProjects)
            {
                finalPropertyResults.Add(testProject.GetPropertyValues(testAsset.Path, testProject == testProjects[0] ? firstProjectTfm : secondProjectTfm, expectedConfiguration));
            }

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

            var (testAsset, testProjects) = Setup(new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, property, "", releasePropertyValue, identifier: property + releasePropertyValue);

            if (releasePropertyValue == "false" && property == PackRelease)
            {
                var dotnetCommand = new DotnetCommand(Log);
                dotnetCommand
                    .Execute("pack", testAsset.Path)
                    .Should()
                    .Fail();
            }
            else
            {
                var dotnetCommand = new DotnetCommand(Log);
                dotnetCommand
                    .Execute(property == PublishRelease ? "publish" : "pack", testAsset.Path)
                    .Should()
                    .Pass();

                var finalPropertyResults = new List<Dictionary<string, string>>();
                foreach (var testProject in testProjects)
                {
                    finalPropertyResults.Add(testProject.GetPropertyValues(testAsset.Path, testProject == testProjects[0] ? firstProjectTfm : secondProjectTfm, expectedConfiguration));
                }

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

            var (testAsset, testProjects) = Setup(new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PublishRelease, "", publishReleaseValue, identifier: publishReleaseValue);

            var dotnetCommand = new DotnetPublishCommand(Log);
            dotnetCommand
                .WithEnvironmentVariable("DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS", "true")
                .Execute(testAsset.Path)
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

            var (testAsset, _) = Setup(new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PublishRelease, "false", "");

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(testAsset.Path)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(string.Format(Strings.SolutionProjectConfigurationsConflict, PublishRelease, "")); ;
        }

        [Fact]
        public void ItDoesNotErrorWithLegacyNet7ProjectAndNet6ProjectSolutionWithNoPublishRelease()
        {
            var firstProjectTfm = "net7.0";
            var secondProjectTfm = "net6.0";

            var (testAsset, _) = Setup(new List<string> { firstProjectTfm }, new List<string> { secondProjectTfm }, PublishRelease, "", "");

            var dotnetCommand = new DotnetCommand(Log, publish);
            dotnetCommand
                .Execute(testAsset.Path)
                .Should()
                .Pass();
        }

        [Theory]
        [InlineData(PublishRelease)]
        [InlineData(PackRelease)]
        public void It_fails_with_conflicting_PublishRelease_or_PackRelease_values_in_solution_file(string pReleaseVar)
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var (testAsset, _) = Setup(new List<string> { tfm }, new List<string> { tfm }, pReleaseVar, "true", "false");

            var expectedError = string.Format(Strings.SolutionProjectConfigurationsConflict, pReleaseVar, "");

            new DotnetCommand(Log)
                .Execute("dotnet", pReleaseVar == PublishRelease ? "publish" : "pack", testAsset.Path)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(expectedError);
        }

        [Fact]
        public void It_sees_PublishRelease_values_of_hardcoded_sln_argument()
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var (testAsset, _) = Setup(new List<string> { tfm }, new List<string> { tfm }, PublishRelease, "true", "false");

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(Directory.GetParent(testAsset.Path).FullName) // code under test looks in CWD, ensure coverage outside this scenario
                .Execute(testAsset.Path)
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
            var (testAsset, testProjects) = Setup(new List<string> { tfm }, new List<string> { tfm }, PublishRelease, "true", "false");

            new DotnetPublishCommand(Log)
                .WithEnvironmentVariable("DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE", "true")
                .Execute(testAsset.Path) // This property won't be set in VS, make sure the error doesn't occur because of this by mimicking behavior.
                .Should()
                .Pass();

            var finalPropertyResults = new List<Dictionary<string, string>>();
            foreach (var testProject in testProjects)
            {
                finalPropertyResults.Add(testProject.GetPropertyValues(testAsset.Path, tfm, expectedConfiguration));
            }

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
