// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToCustomizeTheApphost : SdkTest
    {
        public GivenThatWeWantToCustomizeTheApphost(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_contains_resources_from_the_managed_dll()
        {
            var targetFramework = "netcoreapp2.0";
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var version = "5.6.7.8";
            var testProject = new TestProject()
            {
                Name = "ResourceTest",
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = runtimeIdentifier,
                IsSdkProject = true,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("AssemblyVersion", version);

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
            outputDirectory.Should().HaveFiles(new[] { testProject.Name + ".exe" });

            string apphostPath = Path.Combine(outputDirectory.FullName, testProject.Name + ".exe");
            var apphostVersion = FileVersionInfo.GetVersionInfo(apphostPath).FileVersion;
            apphostVersion.Should().Be(version);
        }
    }
}
