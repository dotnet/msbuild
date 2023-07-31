// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildACppCliNonLibraryProject : SdkTest
    {
        public GivenThatWeWantToBuildACppCliNonLibraryProject(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact]
        public void Given_an_exe_project_It_should_fail_with_error_message()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NETCoreCppClApp")
                .WithSource();

            new BuildCommand(testAsset, "NETCoreCppCliTest.sln")
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining(Strings.NoSupportCppNonDynamicLibraryDotnetCore);
        }

        [FullMSBuildOnlyFact]
        public void Given_an_StaticLibrary_project_It_should_fail_with_error_message()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NETCoreCppClApp")
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (Path.GetExtension(projectPath) == ".vcxproj")
                    {
                        XNamespace ns = project.Root.Name.Namespace;

                        foreach (var configurationType in project.Root.Descendants(ns + "ConfigurationType"))
                        {
                            configurationType.Value = "StaticLibrary";
                        }
                    }
                });

            new BuildCommand(testAsset, "NETCoreCppCliTest.sln")
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining(Strings.NoSupportCppNonDynamicLibraryDotnetCore);
        }
    }
}
