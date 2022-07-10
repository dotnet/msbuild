// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class ReferenceExeTests : SdkTest
    {
        public ReferenceExeTests(ITestOutputHelper log) : base(log)
        {
        }

        private bool MainSelfContained { get; set; }

        private bool ReferencedSelfContained { get; set; }

        private bool TestWithPublish { get; set; } = false;

        private bool PublishTrimmed = false;

        private bool ReferenceExeInCode = false;

        private TestProject MainProject { get; set; }

        private TestProject ReferencedProject { get; set; }

        private void CreateProjects()
        {
            MainProject = new TestProject()
            {
                Name = "MainProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsSdkProject = true,
                IsExe = true
            };

            MainProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.8.26"));
            var mainProjectSrc = @"
using System;
using Humanizer;
Console.WriteLine(""MainProject"".Humanize());";

            if (PublishTrimmed)
            {
                MainProject.AdditionalProperties["PublishTrimmed"] = "true";

                // If we're fully trimming, unless the trimmed project contains an explicit reference in code
                // to the referenced project, it will get trimmed away
                if (ReferenceExeInCode)
                {
                    mainProjectSrc += @"
// Always false, but the trimmer doesn't know that
if (string.Empty.Length > 0)
{
    ReferencedExeProgram.Main();
}";

                }
            }


            MainProject.SourceFiles["Program.cs"] = mainProjectSrc;

            if (MainSelfContained)
            {
                MainProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid();
            }

            ReferencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsSdkProject = true,
                IsExe = true,
            };

            if (ReferencedSelfContained)
            {
                ReferencedProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid();
            }

            //  Use a lower version of a library in the referenced project
            ReferencedProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.7.9"));
            ReferencedProject.SourceFiles["Program.cs"] = @"
using Humanizer;
public class ReferencedExeProgram
{
    public static void Main()
    {
        System.Console.WriteLine(""ReferencedProject"".Humanize());
    }
}";

            MainProject.ReferencedProjects.Add(ReferencedProject);
        }

        private void RunTest(string buildFailureCode = null, [CallerMemberName] string callingMethod = null)
        {
            var testProjectInstance = _testAssetsManager.CreateTestProject(MainProject, callingMethod: callingMethod, identifier: MainSelfContained.ToString() + "_" + ReferencedSelfContained.ToString());

            string outputDirectory;

            TestCommand buildOrPublishCommand;

            if (TestWithPublish)
            {
                var publishCommand = new PublishCommand(testProjectInstance);

                outputDirectory = publishCommand.GetOutputDirectory(MainProject.TargetFrameworks, runtimeIdentifier: MainProject.RuntimeIdentifier).FullName;

                buildOrPublishCommand = publishCommand;
            }
            else
            {
                var buildCommand = new BuildCommand(testProjectInstance);

                outputDirectory = buildCommand.GetOutputDirectory(MainProject.TargetFrameworks, runtimeIdentifier: MainProject.RuntimeIdentifier).FullName;

                buildOrPublishCommand = buildCommand;
            }

            if (buildFailureCode == null)
            {
                buildOrPublishCommand.Execute()
                    .Should()
                    .Pass();

                var mainExePath = Path.Combine(outputDirectory, MainProject.Name + Constants.ExeSuffix);

                var referencedExePath = Path.Combine(outputDirectory, ReferencedProject.Name + Constants.ExeSuffix);

                new RunExeCommand(Log, mainExePath)
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOut("Main project");


                var referencedExeResult = new RunExeCommand(Log, referencedExePath)
                    .Execute();

                // If we're trimming and didn't reference the exe in source we would expect it to be trimmed from the output
                if (PublishTrimmed && !ReferenceExeInCode)
                {
                    referencedExeResult
                        .Should()
                        .Fail()
                        .And
                        .HaveStdErrContaining("The application to execute does not exist");
                }
                else
                {
                    referencedExeResult
                        .Should()
                        .Pass()
                        .And
                        .HaveStdOut("Referenced project");
                }
            }
            else
            {
                //  Build should not succeed
                buildOrPublishCommand.Execute()
                    .Should()
                    .Fail()
                    .And
                    .HaveStdOutContaining(buildFailureCode);
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void ReferencedExeCanRun(bool mainSelfContained, bool referencedSelfContained)
        {
            MainSelfContained = mainSelfContained;
            ReferencedSelfContained = referencedSelfContained;

            CreateProjects();

            RunTest();
        }

        [Fact]
        public void ReferencedExeWithLowerTargetFrameworkCanRun()
        {
            MainSelfContained = false;
            ReferencedSelfContained = false;

            CreateProjects();

            ReferencedProject.TargetFrameworks = "netcoreapp3.1";
            ReferencedProject.AdditionalProperties["LangVersion"] = "9.0";

            RunTest();
        }

        //  Having a self-contained and a framework-dependent app in the same folder is not supported (due to the way the host works).
        //  The referenced app will fail to run.  See here for more details: https://github.com/dotnet/sdk/pull/14488#issuecomment-725406998
        [Theory]
        [InlineData(true, false, "NETSDK1150")]
        [InlineData(false, true, "NETSDK1151")]
        public void ReferencedExeFailsToBuild(bool mainSelfContained, bool referencedSelfContained, string expectedFailureCode)
        {
            MainSelfContained = mainSelfContained;
            ReferencedSelfContained = referencedSelfContained;

            CreateProjects();

            RunTest(expectedFailureCode);
        }

        [Fact]
        public void ReferencedExeCanRunWhenReferencesExeWithSelfContainedMismatchForDifferentTargetFramework()
        {
            MainSelfContained = true;
            ReferencedSelfContained = false;

            CreateProjects();

            //  Reference project which is self-contained for net5.0, not self-contained for net5.0-windows.
            ReferencedProject.TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};{ToolsetInfo.CurrentTargetFramework}-windows";
            ReferencedProject.ProjectChanges.Add(project =>
            {
                var ns = project.Root.Name.Namespace;

                project.Root.Element(ns + "PropertyGroup")
                    .Add(XElement.Parse($@"<RuntimeIdentifier Condition=""'$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'"">" + EnvironmentInfo.GetCompatibleRid() + "</RuntimeIdentifier>"));
            });

            RunTest();
        }

        [Fact]
        public void ReferencedExeFailsToBuildWhenReferencesExeWithSelfContainedMismatchForSameTargetFramework()
        {
            MainSelfContained = true;
            ReferencedSelfContained = false;

            CreateProjects();

            //  Reference project which is self-contained for net5.0-windows, not self-contained for net5.0.
            ReferencedProject.TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};{ToolsetInfo.CurrentTargetFramework}-windows";
            ReferencedProject.ProjectChanges.Add(project =>
            {
                var ns = project.Root.Name.Namespace;

                project.Root.Element(ns + "PropertyGroup")
                    .Add(XElement.Parse($@"<RuntimeIdentifier Condition=""'$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}-windows'"">" + EnvironmentInfo.GetCompatibleRid() + "</RuntimeIdentifier>"));
            });

            RunTest("NETSDK1150");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReferencedExeCanRunWhenPublished(bool selfContained)
        {
            MainSelfContained = selfContained;
            ReferencedSelfContained = selfContained;

            TestWithPublish = true;

            CreateProjects();

            RunTest();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ReferencedExeCanRunWhenPublishedWithTrimming(bool referenceExeInCode)
        {
            MainSelfContained = true;
            ReferencedSelfContained = true;

            TestWithPublish = true;
            PublishTrimmed = true;
            ReferenceExeInCode = referenceExeInCode;

            CreateProjects();

            RunTest();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("xunit")]
        [InlineData("mstest")]
        public void TestProjectCanReferenceExe(string testTemplateName)
        {
            var testConsoleProject = new TestProject("ConsoleApp")
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };

            var testAsset = _testAssetsManager.CreateTestProject(testConsoleProject, identifier: testTemplateName);

            var testProjectDirectory = Path.Combine(testAsset.TestRoot, "TestProject");
            Directory.CreateDirectory(testProjectDirectory);

            new DotnetCommand(Log, "new", testTemplateName, "--debug:ephemeral-hive")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "add", "reference", ".." + Path.DirectorySeparatorChar + testConsoleProject.Name)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new BuildCommand(Log, testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("xunit")]
        [InlineData("mstest")]
        public void ExeProjectCanReferenceTestProject(string testTemplateName)
        {
            var testConsoleProject = new TestProject("ConsoleApp")
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };

            var testAsset = _testAssetsManager.CreateTestProject(testConsoleProject, identifier: testTemplateName);

            var testProjectDirectory = Path.Combine(testAsset.TestRoot, "TestProject");
            Directory.CreateDirectory(testProjectDirectory);

            new DotnetCommand(Log, "new", testTemplateName, "--debug:ephemeral-hive")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            string consoleProjectDirectory = Path.Combine(testAsset.Path, testConsoleProject.Name);

            new DotnetCommand(Log, "add", "reference", ".." + Path.DirectorySeparatorChar + "TestProject")
                .WithWorkingDirectory(consoleProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new BuildCommand(Log, consoleProjectDirectory)
                .Execute()
                .Should()
                .Pass();
        }
    }
}
