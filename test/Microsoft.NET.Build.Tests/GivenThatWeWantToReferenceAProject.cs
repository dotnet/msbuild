using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using FluentAssertions;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToReferenceAProject
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        //  Different types of projects which should form the test matrix:

        //  Desktop (non-SDK) project
        //  Single-targeted SDK project
        //  Multi-targeted SDK project
        //  PCL

        //  Compatible
        //  Incompatible

        //  Exe
        //  Library

        //  .NET Core
        //  .NET Standard
        //  .NET Framework (SDK and non-SDK)

        public enum ReferenceBuildResult
        {
            BuildSucceeds,
            FailsRestore,
            FailsBuild
        }

        [Theory]
        [InlineData("netcoreapp1.0", "netstandard1.5", true, true)]
        [InlineData("netstandard1.2", "netstandard1.5", false, false)]
        [InlineData("netcoreapp1.0", "net45;netstandard1.5", true, true)]
        [InlineData("netcoreapp1.0", "net45;net46", false, false)]
        [InlineData("netcoreapp1.0;net461", "netstandard1.4", true, true)]
        [InlineData("netcoreapp1.0;net45", "netstandard1.4", false, false)]
        [InlineData("netcoreapp1.0;net46", "net45;netstandard1.6", true, true)]
        [InlineData("netcoreapp1.0;net45", "net46;netstandard1.6", false, false)]
        public void It_checks_for_valid_references(string referencerTarget, string dependencyTarget, bool restoreSucceeds, bool buildSucceeds)
        {
            string identifier = referencerTarget.ToString() + " " + dependencyTarget.ToString();

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                IsSdkProject = true,
            };

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                IsSdkProject = true
            };

            if (referencerTarget.Contains(";"))
            {
                referencerProject.TargetFrameworks = referencerTarget;
            }
            else
            {
                referencerProject.TargetFramework = referencerTarget;
            }

            if (dependencyTarget.Contains(";"))
            {
                dependencyProject.TargetFrameworks = dependencyTarget;
            }
            else
            {
                dependencyProject.TargetFramework = dependencyTarget;
            }

            referencerProject.ReferencedProjects.Add(dependencyProject);

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!referencerProject.BuildsOnNonWindows || !dependencyProject.BuildsOnNonWindows)
                {
                    return;
                }
            }

            var testAsset = _testAssetsManager.CreateTestProject(referencerProject, nameof(It_checks_for_valid_references), identifier);

            var restoreCommand = testAsset.GetRestoreCommand(relativePath: "Referencer");

            if (restoreSucceeds)
            {
                restoreCommand
                    .Execute()
                    .Should()
                    .Pass();
            }
            else
            {
                restoreCommand
                    .CaptureStdOut()
                    .Execute()
                    .Should()
                    .Fail();
            }

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "Referencer");

            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);

            if (!buildSucceeds)
            {
                buildCommand = buildCommand.CaptureStdOut();
            }

            var result = buildCommand.Execute();

            if (buildSucceeds)
            {
                result.Should().Pass();
            }
            else
            {
                result.Should().Fail()
                    .And.HaveStdOutContaining("has no target framework compatible with");
            }
        }
    }
}
