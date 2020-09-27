// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
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

        //  Enabling all of these tests on full framework is tracked by https://github.com/dotnet/sdk/issues/13849
        [CoreMSBuildOnlyFact]
        public void It_should_build_with_workload()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                IsSdkProject = true,
                TargetFrameworks = "net5.0-workloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .WithEnvironmentVariable("MSBuildEnableWorkloadResolver", "true")
                .Execute()
                .Should()
                .Pass();
        }

        [CoreMSBuildOnlyFact]
        public void It_should_fail_without_workload()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                IsSdkProject = true,
                TargetFrameworks = "net5.0-missingworkloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .WithEnvironmentVariable("MSBuildEnableWorkloadResolver", "true")
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1146");
        }

        [CoreMSBuildOnlyFact]
        public void It_should_fail_without_resolver_enabled()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                IsSdkProject = true,
                TargetFrameworks = "net5.0-workloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            //  NETSDK1139: The target platform identifier workloadtestplatform was not recognized.
            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1139");
        }

        [CoreMSBuildOnlyFact]
        public void It_should_import_AutoImports_for_installed_workloads()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var getValuesCommand = new GetValuesCommand(testAsset, "TestWorkloadAutoImportPropsImported");

            getValuesCommand
                .WithEnvironmentVariable("MSBuildEnableWorkloadResolver", "true")
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
