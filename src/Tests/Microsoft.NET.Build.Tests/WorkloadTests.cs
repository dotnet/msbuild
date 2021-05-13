// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class WorkloadTests : SdkTest
    {
        public WorkloadTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_should_build_with_workload()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = "net5.0-workloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_should_fail_without_workload()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = "net5.0-missingworkloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1147");
        }

        [Fact]
        public void It_should_fail_without_workload_when_multitargeted()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = "net5.0;net5.0-missingworkloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1147");
        }

        [Fact]
        public void It_should_fail_when_multitargeted_to_unknown_platforms()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = "net5.0-foo;net5.0-bar"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1139");
        }


        [Fact]
        public void It_should_fail_with_resolver_disabled()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = "net5.0-workloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            //  NETSDK1139: The target platform identifier workloadtestplatform was not recognized.
            new BuildCommand(testAsset)
                .WithEnvironmentVariable("MSBuildEnableWorkloadResolver", "false")
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1139");
        }

        [Fact]
        public void It_should_import_AutoImports_for_installed_workloads()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = "net5.0"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var expectedProperty = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "WinTestWorkloadAutoImportPropsImported" : "UnixTestWorkloadAutoImportPropsImported";

            var getValuesCommand = new GetValuesCommand(testAsset, expectedProperty);

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo("true");
        }

        [Fact]
        public void It_should_import_aliased_pack()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = "net5.0-workloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            var expectedProperty = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                "UsingWinTestWorkloadPack" :
                "UsingUnixTestWorkloadPack";

            var getValuesCommand = new GetValuesCommand(testAsset, expectedProperty);

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo("true");
        }
    }
}
