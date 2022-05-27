// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildACppCliProject : SdkTest
    {
        public GivenThatWeWantToBuildACppCliProject(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/3785")]
        public void It_builds_and_runs()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource();

            // build projects separately with BuildProjectReferences=false to simulate VS build behavior
            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Pass();

            new BuildCommand(testAsset, "CSConsoleApp")
                .Execute(new string[] { "-p:Platform=x64", "-p:BuildProjectReferences=false" })
                .Should()
                .Pass();

            var exe = Path.Combine( //find the platform directory
                new DirectoryInfo(Path.Combine(testAsset.TestRoot, "CSConsoleApp", "bin")).GetDirectories().Single().FullName,
                "Debug",
                "net5.0",
                "CSConsoleApp.exe");

            var runCommand = new RunExeCommand(Log, exe);
            runCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello, World!");
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/3785")]
        public void Given_no_restore_It_builds_cpp_project()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource();

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Pass();
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/3785")]
        public void Given_Wpf_framework_reference_It_builds_cpp_project()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CppCliLibWithWpfFrameworkReference")
                .WithSource();

            new BuildCommand(testAsset)
                .Execute("-p:Platform=x64")
                .Should()
                .Pass();
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/11008")]
        public void It_fails_with_error_message_on_EnableComHosting()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (Path.GetExtension(projectPath) == ".vcxproj")
                    {
                        XNamespace ns = project.Root.Name.Namespace;

                        var globalPropertyGroup = project.Root
                            .Descendants(ns + "PropertyGroup")
                            .Where(e => e.Attribute("Label")?.Value == "Globals")
                            .Single();
                        globalPropertyGroup.Add(new XElement(ns + "EnableComHosting", "true"));
                    }
                });

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.NoSupportCppEnableComHosting);
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/11008")]
        public void It_fails_with_error_message_on_fullframework()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                    ChangeTargetFramework(projectPath, project, "net472"));

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.NETFrameworkWithoutUsingNETSdkDefaults);
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/11008")]
        public void It_fails_with_error_message_on_tfm_lower_than_3_1()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                    ChangeTargetFramework(projectPath, project, "netcoreapp3.0"));

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CppRequiresTFMVersion31);
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/11008")]
        public void When_run_with_selfcontained_It_fails_with_error_message()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource();

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64", "-p:selfcontained=true", $"-p:RuntimeIdentifier={ToolsetInfo.LatestWinRuntimeIdentifier}-x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.NoSupportCppSelfContained);
        }

        private void ChangeTargetFramework(string projectPath, XDocument project, string targetFramework)
        {
            if (Path.GetExtension(projectPath) == ".vcxproj")
            {
                XNamespace ns = project.Root.Name.Namespace;

                project.Root.Descendants(ns + "PropertyGroup")
                                            .Descendants(ns + "TargetFramework")
                                            .Single().Value = targetFramework;
            }
        }
    }
}
