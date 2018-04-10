// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using FluentAssertions;
using System.Runtime.InteropServices;
using System.Linq;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToReferenceAProject : SdkTest
    {
        public GivenThatWeWantToReferenceAProject(ITestOutputHelper log) : base(log)
        {
        }

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
        [InlineData("netstandard1.2", true, "netstandard1.5", true, false, false)]
        [InlineData("netcoreapp1.1", true, "net45;netstandard1.5", true, true, true)]
        [InlineData("netcoreapp1.1", true, "net45;net46", true, false, false)]
        [InlineData("netcoreapp1.1;net461", true, "netstandard1.4", true, true, true)]
        [InlineData("netcoreapp1.1;net45", true, "netstandard1.4", true, false, false)]
        [InlineData("netcoreapp1.1;net46", true, "net45;netstandard1.6", true, true, true)]
        [InlineData("netcoreapp1.1;net45", true, "net46;netstandard1.6", true, false, false)]
        [InlineData("v4.5", false, "netstandard1.6", true, true, false)]
        [InlineData("v4.6.1", false, "netstandard1.6;net461", true, true, true)]
        [InlineData("v4.5", false, "netstandard1.6;net461", true, true, false)]
        public void It_checks_for_valid_references(string referencerTarget, bool referencerIsSdkProject,
            string dependencyTarget, bool dependencyIsSdkProject,
            bool restoreSucceeds, bool buildSucceeds)
        {
            string identifier = referencerTarget.ToString() + " " + dependencyTarget.ToString();
            //  MSBuild isn't happy with semicolons in the path when doing file exists checks
            identifier = identifier.Replace(';', '_');

            TestProject referencerProject = GetTestProject("Referencer", referencerTarget, referencerIsSdkProject);
            TestProject dependencyProject = GetTestProject("Dependency", dependencyTarget, dependencyIsSdkProject);
            referencerProject.ReferencedProjects.Add(dependencyProject);

            //  Set the referencer project as an Exe unless it targets .NET Standard
            if (!referencerProject.ShortTargetFrameworkIdentifiers.Contains("netstandard"))
            {
                referencerProject.IsExe = true;
            }

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!referencerProject.BuildsOnNonWindows || !dependencyProject.BuildsOnNonWindows)
                {
                    return;
                }
            }

            var testAsset = _testAssetsManager.CreateTestProject(referencerProject, nameof(It_checks_for_valid_references), identifier);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: "Referencer");

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
                    .Execute()
                    .Should()
                    .Fail();
            }

            if (!referencerProject.IsSdkProject)
            {
                //  The Restore target currently seems to be a no-op for non-SDK projects,
                //  so we need to explicitly restore the dependency
                testAsset.GetRestoreCommand(Log, relativePath: "Dependency")
                    .Execute()
                    .Should()
                    .Pass();
            }

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "Referencer");
            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            var result = buildCommand.Execute();

            if (buildSucceeds)
            {
                result.Should().Pass();
            }
            else if (referencerIsSdkProject)
            {
                result.Should().Fail().And.HaveStdOutContaining("NU1201");
            }
            else
            {
                result.Should().Fail()
                    .And.HaveStdOutContaining("It cannot be referenced by a project that targets");
            }
        }

        TestProject GetTestProject(string name, string target, bool isSdkProject)
        {
            TestProject ret = new TestProject()
            {
                Name = name,
                IsSdkProject = isSdkProject
            };

            if (isSdkProject)
            {
                ret.TargetFrameworks = target;
            }
            else
            {
                ret.TargetFrameworkVersion = target;
            }

            return ret;
        }
    }
}
