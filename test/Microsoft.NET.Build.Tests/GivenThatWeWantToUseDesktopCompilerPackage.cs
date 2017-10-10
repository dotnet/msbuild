// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using FluentAssertions;
using Xunit;

using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using Xunit.Abstractions;
using System.Text.RegularExpressions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToUseDesktopCompilerPackage : SdkTest
    {
        public GivenThatWeWantToUseDesktopCompilerPackage(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildAndWindowsOnlyFact]
        public void It_builds_netstandard_satellites_using_desktop_csc()
        {
            var testProject = new TestProject()
            {
                Name = "UseDesktopCompilerPackage",
                TargetFrameworks = "netstandard1.3",
                IsSdkProject = true,
                IsExe = true,
            };

            testProject.EmbeddedResources["Strings.fr.resx"] = @"
<root>
  <data name=""key"">
    <value>value</value>
  </data>
</root>";

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.NET.Compilers", "2.3.2"));

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
