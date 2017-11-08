// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
#pragma warning disable xUnit1004 // Test methods should not be skipped

    public class GivenThatWeWantToTargetNet471 : SdkTest
    {
        public GivenThatWeWantToTargetNet471(ITestOutputHelper log) : base(log)
        {
        }

        string[] net471Shims =
        {
            "System.Data.Common.dll",
            "System.Diagnostics.StackTrace.dll",
            "System.Diagnostics.Tracing.dll",
            "System.Globalization.Extensions.dll",
            "System.IO.Compression.dll",
            "System.Net.Http.dll",
            "System.Net.Sockets.dll",
            "System.Runtime.Serialization.Primitives.dll",
            "System.Security.Cryptography.Algorithms.dll",
            "System.Security.SecureString.dll",
            "System.Threading.Overlapped.dll",
            "System.Xml.XPath.XDocument.dll"
        };

        [Fact]
        public void It_builds_a_net471_app()
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }
            var testProject = new TestProject()
            {
                Name = "Net471App",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
            });
        }

        [Fact]
        public void It_builds_a_net471_app_referencing_netstandard20()
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }
            var testProject = new TestProject()
            {
                Name = "Net471App_Referencing_NetStandard20",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            var netStandardProject = new TestProject()
            {
                Name="NetStandard20_Library",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            testProject.ReferencedProjects.Add(netStandardProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "net471_ref_ns20")
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
                $"{netStandardProject.Name}.dll",
                $"{netStandardProject.Name}.pdb",
            }.Concat(net471Shims));
        }

        [Fact]
        public void It_does_not_include_facades_from_nuget_packages()
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }
            var testProject = new TestProject()
            {
                Name = "Net471_NuGetFacades",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("NETStandard.Library", "1.6.1"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
                
                "System.Net.Http.dll",
                "System.IO.Compression.dll",

                //  This is an implementation dependency of the System.Net.Http package, which won't get conflict resolved out
                "System.Diagnostics.DiagnosticSource.dll",
            });
        }

        [Fact]
        public void It_includes_shims_when_net471_app_references_netstandard16()
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }
            var testProject = new TestProject()
            {
                Name = "Net471App_Referencing_NetStandard16",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            var netStandardProject = new TestProject()
            {
                Name = "NetStandard16_Library",
                TargetFrameworks = "netstandard1.6",
                IsSdkProject = true
            };

            testProject.ReferencedProjects.Add(netStandardProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "net471_ref_ns16")
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
                $"{netStandardProject.Name}.dll",
                $"{netStandardProject.Name}.pdb",
                "System.Diagnostics.DiagnosticSource.dll" // This library will get pulled in as part of the closure of the ns16 project and will be copyied because it's not inbox.
            }.Concat(net471Shims));
        }

        [Fact]
        public void It_does_not_include_shims_when_app_references_471_library()
        {
            //  https://github.com/dotnet/sdk/issues/1625
            if (!Net471ReferenceAssembliesAreInstalled())
            {
                return;
            }
            var testProject = new TestProject()
            {
                Name = "Net471App_Referencing_Net471Library",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            var net471library = new TestProject()
            {
                Name = "Net471_Library",
                TargetFrameworks = "net471",
                IsSdkProject = true
            };

            testProject.ReferencedProjects.Add(net471library);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "net471_ref_net471")
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
                $"{net471library.Name}.dll",
                $"{net471library.Name}.pdb",
            });
        }

        static bool Net471ReferenceAssembliesAreInstalled()
        {
            var net461referenceAssemblies = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version461);
            if (net461referenceAssemblies == null)
            {
                //  4.6.1 reference assemblies not found, assume that 4.7.1 isn't available either
                return false;
            }
            var net471referenceAssemblies = Path.Combine(new DirectoryInfo(net461referenceAssemblies).Parent.FullName, "v4.7.1");
            return Directory.Exists(net471referenceAssemblies);
        }
    }
}
