// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using Microsoft.DotNet.Cli.Utils;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantNet461ToBeAnImplicitPackageTargetFallback : SdkTest, IClassFixture<DeleteNuGetArtifactsFixture>
    {
        private TestPackageReference _net461PackageReference;

        [WindowsOnlyTheoryAttribute]
        [InlineData("netstandard2.0")]
        [InlineData("netcoreapp2.0")]
        public void Net461_is_implicit_for_Netstandard_and_Netcore_20(string targetFramework)
        {
            var testProjectName = targetFramework.Replace(".", "_");

            var testProjectTestAsset = CreateTestAsset(testProjectName, targetFramework);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(relativePath: testProjectName);
            restoreCommand.AddSource(Path.GetDirectoryName(_net461PackageReference.NupkgPath));
            restoreCommand.Execute().Should().Pass();

            var buildCommand = new BuildCommand(
                Stage0MSBuild,
                Path.Combine(testProjectTestAsset.TestRoot, testProjectName));
            buildCommand.Execute().Should().Pass();
        }

        [WindowsOnlyTheoryAttribute]
        [InlineData("netstandard1.6")]
        [InlineData("netcoreapp1.1")]
        public void Net461_is_not_implicit_for_Netstandard_and_Netcore_less_than_20(string targetFramework)
        {
            var testProjectName = targetFramework.Replace(".", "_");

            var testProjectTestAsset = CreateTestAsset(testProjectName, targetFramework);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(relativePath: testProjectName);
            restoreCommand.AddSource(Path.GetDirectoryName(_net461PackageReference.NupkgPath));
            restoreCommand.Execute().Should().Fail();
        }

        [WindowsOnlyFact]
        public void It_is_possible_to_disabled_net461_implicit_package_target_fallback()
        {
            const string testProjectName = "netstandard20";

            var testProjectTestAsset = CreateTestAsset(
                testProjectName,
                "netstandard2.0",
                new Dictionary<string, string> { {"DisableImplicitPackageTargetFallback", "true" } });

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(relativePath: testProjectName);
            restoreCommand.AddSource(Path.GetDirectoryName(_net461PackageReference.NupkgPath));
            restoreCommand.Execute().Should().Fail();
        }

        private TestAsset CreateTestAsset(
            string testProjectName,
            string targetFramework,
            Dictionary<string, string> additionalProperties = null)
        {
            _net461PackageReference = CreateNet461Package();

            var testProject =
                new TestProject
                {
                    Name = testProjectName,
                    TargetFrameworks = targetFramework,
                    IsSdkProject = true
                };

            if (additionalProperties != null)
            {
                foreach (var additionalProperty in additionalProperties)
                {
                    testProject.AdditionalProperties.Add(additionalProperty.Key, additionalProperty.Value);    
                }
            }
            
            testProject.PackageReferences.Add(_net461PackageReference);

            var testProjectTestAsset = _testAssetsManager.CreateTestProject(
                testProject,
                ConstantStringValues.TestDirectoriesNamePrefix, $"_{testProjectName}_net461");

            return testProjectTestAsset;
        }

        private TestPackageReference CreateNet461Package()
        {
            var net461Project = 
                new TestProject
                {
                    Name = "net461_package",
                    TargetFrameworks = "net461",
                    IsSdkProject = true
                };

            var net461PackageReference =
                new TestPackageReference(
                    net461Project.Name,
                    "1.0.0",
                    ConstantStringValues.ConstructNuGetPackageReferencePath(net461Project));

            var net461PackageTestAsset = _testAssetsManager.CreateTestProject(
                net461Project,
                ConstantStringValues.TestDirectoriesNamePrefix,
                ConstantStringValues.NuGetSharedDirectoryNamePostfix);
            var packageRestoreCommand =
                net461PackageTestAsset.GetRestoreCommand(relativePath: net461Project.Name).Execute().Should().Pass();
            var dependencyProjectDirectory = Path.Combine(net461PackageTestAsset.TestRoot, net461Project.Name);
            var packagePackCommand =
                new PackCommand(Stage0MSBuild, dependencyProjectDirectory).Execute().Should().Pass();

            return net461PackageReference;
        }
    }
}