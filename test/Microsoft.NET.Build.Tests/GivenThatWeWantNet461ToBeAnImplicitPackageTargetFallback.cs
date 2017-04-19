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
    public class GivenThatWeWantNet461ToBeAnImplicitPackageTargetFallback : SdkTest
    {
        [Fact]
        public void Net461_is_implicit_for_Netstandard20()
        {
            var netstandard20Project =
                new TestProject
                {
                    Name = "netstandard20",
                    TargetFrameworks = "netstandard2.0",
                    IsSdkProject = true
                };

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

            netstandard20Project.PackageReferences.Add(net461PackageReference);

            var net461PackageTestAsset = _testAssetsManager.CreateTestProject(
                net461Project,
                ConstantStringValues.TestDirectoriesNamePrefix,
                ConstantStringValues.NuGetSharedDirectoryNamePostfix);
            var packageRestoreCommand =
                net461PackageTestAsset.GetRestoreCommand(relativePath: net461Project.Name).Execute().Should().Pass();
            var dependencyProjectDirectory = Path.Combine(net461PackageTestAsset.TestRoot, net461Project.Name);
            var packagePackCommand = new PackCommand(Stage0MSBuild, dependencyProjectDirectory).Execute().Should().Pass();

            var netstandard20TestAsset = _testAssetsManager.CreateTestProject(
                netstandard20Project,
                ConstantStringValues.TestDirectoriesNamePrefix, "_netstandard20_net461");
            var restoreCommand = netstandard20TestAsset.GetRestoreCommand(relativePath: netstandard20Project.Name);

            foreach (TestPackageReference packageReference in netstandard20Project.PackageReferences)
            {
                restoreCommand.AddSource(Path.GetDirectoryName(packageReference.NupkgPath));
            }

            restoreCommand.Execute().Should().Pass();

            var buildCommand = new BuildCommand(
                Stage0MSBuild,
                Path.Combine(netstandard20TestAsset.TestRoot, netstandard20Project.Name));

            buildCommand.Execute().Should().Pass();
        }
    }
}