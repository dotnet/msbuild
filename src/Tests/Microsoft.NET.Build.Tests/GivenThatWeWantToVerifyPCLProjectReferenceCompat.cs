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
    public class GivenThatWeWantToVerifyPCLProjectReferenceCompat : SdkTest
    {
        public GivenThatWeWantToVerifyPCLProjectReferenceCompat(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyTheory]
        [InlineData("netstandard1.1", "Profile7", "v4.5", true, true)]
        [InlineData("netstandard1.0", "Profile31", "v4.6", true, true)]
        [InlineData("netstandard1.2", "Profile32", "v4.6", true, true)]
        [InlineData("netstandard1.2", "Profile44", "v4.6", true, true)]
        [InlineData("netstandard1.0", "Profile49", "v4.5", true, true)]
        [InlineData("netstandard1.0", "Profile78", "v4.5", true, true)]
        [InlineData("netstandard1.0", "Profile84", "v4.6", true, true)]
        [InlineData("netstandard1.1", "Profile111", "v4.5", true, true)]
        [InlineData("netstandard1.2", "Profile151", "v4.6", true, true)]
        [InlineData("netstandard1.0", "Profile157", "v4.6", true, true)]
        [InlineData("netstandard1.0", "Profile259", "v4.5", true, true)]

        public void PCL_Project_reference_compat(string referencerTarget, string profileDependencyTarget, string netDependencyTarget,
                bool restoreSucceeds, bool buildSucceeds)
        {
            string identifier = "_TestID_" + referencerTarget + "_" + profileDependencyTarget;

            TestProject referencerProject = GetTestProject("Referencer", referencerTarget, null, true);
            TestProject dependencyProject = GetTestProject("Dependency", netDependencyTarget, profileDependencyTarget, false);
            referencerProject.ReferencedProjects.Add(dependencyProject);

            //  Set the referencer project as an Exe unless it targets .NET Standard
            if (!referencerProject.ShortTargetFrameworkIdentifiers.Contains("netstandard"))
            {
                referencerProject.IsExe = true;
            }

            //  Skip running test if not running on Windows
            //        https://github.com/dotnet/sdk/issues/335
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!referencerProject.BuildsOnNonWindows || !dependencyProject.BuildsOnNonWindows)
                {
                    return;
                }
            }

            var testAsset = _testAssetsManager.CreateTestProject(referencerProject, nameof(PCL_Project_reference_compat), identifier);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: "Referencer");

            if (restoreSucceeds)
            {
                restoreCommand.Execute().Should().Pass();
            }
            else
            {
                restoreCommand.Execute().Should().Fail();
            }

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "Referencer");
            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            var result = buildCommand.Execute();

            if (buildSucceeds)
            {
                result.Should().Pass();
            }
            else
            {
                result.Should().Fail().And.HaveStdOutContaining("NU1201");
            }
        }

        TestProject GetTestProject(string name, string target, string profile, bool isSdkProject)
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
                if (!string.IsNullOrEmpty(profile))
                {
                    ret.TargetFrameworkProfile = profile;
                }
            }

            return ret;
        }

    }
}
