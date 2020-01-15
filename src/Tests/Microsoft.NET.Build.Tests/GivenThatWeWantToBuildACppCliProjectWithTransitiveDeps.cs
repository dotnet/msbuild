// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildACppCliProjectWithTransitiveDeps : SdkTest
    {
        public GivenThatWeWantToBuildACppCliProjectWithTransitiveDeps(ITestOutputHelper log) : base(log)
        {
            _buildAsset = new Lazy<TestAsset>(BuildAsset);
        }

        private readonly Lazy<TestAsset> _buildAsset;

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/3785")]
        public void It_can_generate_correct_depsJson_file()
        {
            TestAsset testAsset = _buildAsset.Value;

            string depsJsonContent = File.ReadAllText(Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest", "Debug",
                "NETCoreCppCliTest.deps.json"));
            depsJsonContent.Should().Contain("NETCoreCppCliTestB.dll", "should contain direct project reference");
            depsJsonContent.Should().Contain("NETCoreCppCliTestC.dll", "should contain transitive reference");
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/3785")]
        public void It_can_generate_all_runtimeconfig_files_to_output_folder()
        {
            TestAsset testAsset = _buildAsset.Value;
            var outputDirectory = new DirectoryInfo(Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest", "Debug"));
            outputDirectory.Should().HaveFiles(new[]
            {
                "NETCoreCppCliTest.runtimeconfig.json", "NETCoreCppCliTestB.runtimeconfig.json",
                "NETCoreCppCliTestC.runtimeconfig.json"
            });
        }

        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/3785")]
        public void It_can_generate_all_depsjson_files_to_output_folder()
        {
            TestAsset testAsset = _buildAsset.Value;
            var outputDirectory = new DirectoryInfo(Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest", "Debug"));
            outputDirectory.Should().HaveFiles(new[]
            {
                "NETCoreCppCliTest.deps.json", "NETCoreCppCliTestB.deps.json", "NETCoreCppCliTestC.deps.json"
            });
        }

        private TestAsset BuildAsset()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCppCliLibWithTransitiveDeps")
                .WithSource();

            // build projects separately with BuildProjectReferences=false to simulate VS build behavior
            new BuildCommand(Log, Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest"))
                .Execute("-p:Platform=win32")
                .Should()
                .Pass();
            return testAsset;
        }
    }
}
