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

        private TestProject MainProject { get; set; }

        private TestProject ReferencedProject { get; set; }

        private void CreateProjects()
        {
            MainProject = new TestProject()
            {
                Name = "MainProject",
                TargetFrameworks = "net5.0",
                IsSdkProject = true,
                IsExe = true
            };

            MainProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.8.26"));
            MainProject.SourceFiles["Program.cs"] = @"using Humanizer; System.Console.WriteLine(""MainProject"".Humanize());";

            //  By default we don't create the app host on Mac for FDD.  For these tests, we want to create it everywhere
            MainProject.AdditionalProperties["UseAppHost"] = "true";

            if (MainSelfContained)
            {
                MainProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid();
            }

            ReferencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = "net5.0",
                IsSdkProject = true,
                IsExe = true,
            };

            ReferencedProject.AdditionalProperties["UseAppHost"] = "true";

            if (ReferencedSelfContained)
            {
                ReferencedProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid();
            }

            //  Use a lower version of a library in the referenced project
            ReferencedProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.7.9"));
            ReferencedProject.SourceFiles["Program.cs"] = @"using Humanizer; System.Console.WriteLine(""ReferencedProject"".Humanize());";

            MainProject.ReferencedProjects.Add(ReferencedProject);
        }

        private void RunTest(bool referencedExeShouldRun, [CallerMemberName] string callingMethod = null)
        {
            var testProjectInstance = _testAssetsManager.CreateTestProject(MainProject, callingMethod: callingMethod, identifier: MainSelfContained.ToString() + "_" + ReferencedSelfContained.ToString());

            string outputDirectory;

            if (TestWithPublish)
            {
                var publishCommand = new PublishCommand(testProjectInstance);

                publishCommand.Execute()
                    .Should()
                    .Pass();

                outputDirectory = publishCommand.GetOutputDirectory(MainProject.TargetFrameworks, runtimeIdentifier: MainProject.RuntimeIdentifier).FullName;
            }
            else
            {
                var buildCommand = new BuildCommand(testProjectInstance);

                buildCommand.Execute()
                    .Should()
                    .Pass();

                outputDirectory = buildCommand.GetOutputDirectory(MainProject.TargetFrameworks, runtimeIdentifier: MainProject.RuntimeIdentifier).FullName;
            }

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

            if (referencedExeShouldRun)
            {
                referencedExeResult
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOut("Referenced project");
            }
            else
            {
                referencedExeResult
                    .Should()
                    .Fail();
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

            RunTest(true);
        }

        [Fact]
        public void ReferencedExeWithLowerTargetFrameworkCanRun()
        {
            MainSelfContained = false;
            ReferencedSelfContained = false;
            
            CreateProjects();

            ReferencedProject.TargetFrameworks = "netcoreapp3.1";
            ReferencedProject.AdditionalProperties["LangVersion"] = "9.0";

            RunTest(true);
        }

        //  Test cases that are currently failing
        [Theory]
        //  Fails because runtime pack artifacts are not transitively copied
        [InlineData(true, false)]
        //  Fails with the following error (is the self-contained copy of hostfxr or something interfering with the framework dependent app?):
        //  It was not possible to find any compatible framework version
        //  The framework 'Microsoft.NETCore.App', version '5.0.0' was not found.
        //    - No frameworks were found.
        //
        //  You can resolve the problem by installing the specified framework and/or SDK.
        [InlineData(false, true)]
        public void ReferencedExeFailsToRun(bool mainSelfContained, bool referencedSelfContained)
        {
            MainSelfContained = mainSelfContained;
            ReferencedSelfContained = referencedSelfContained;

            CreateProjects();

            RunTest(referencedExeShouldRun: false);
        }

        [Theory]
        [InlineData(false, false)]
        //[InlineData(true, false, Skip = "Currently not supported (see ReferencedExeFailsToRun)")]
        //[InlineData(false, true, Skip = "Currently not supported (see ReferencedExeFailsToRun)")]
        [InlineData(true, true)]
        public void ReferencedExeCanRunWhenPublished(bool mainSelfContained, bool referencedSelfContained)
        {
            MainSelfContained = mainSelfContained;
            ReferencedSelfContained = referencedSelfContained;

            TestWithPublish = true;
            
            CreateProjects();

            RunTest(referencedExeShouldRun: true);
        }

        [Theory]
        //[InlineData(true, false, Skip = "Currently not supported (see ReferencedExeFailsToRun)")]
        //[InlineData(false, true, Skip = "Currently not supported (see ReferencedExeFailsToRun)")]
        [InlineData(true, true)]

        public void ReferencedExeCanRunWhenPublishedWithTrimming(bool mainSelfContained, bool referencedSelfContained)
        {
            MainSelfContained = mainSelfContained;
            ReferencedSelfContained = referencedSelfContained;

            TestWithPublish = true;

            CreateProjects();

            if (MainSelfContained)
            {
                MainProject.AdditionalProperties["PublishTrimmed"] = "True";
            }
            if (ReferencedSelfContained)
            {
                ReferencedProject.AdditionalProperties["PublishTrimmed"] = "True";
            }

            RunTest(referencedExeShouldRun: true);
        }
    }
}
