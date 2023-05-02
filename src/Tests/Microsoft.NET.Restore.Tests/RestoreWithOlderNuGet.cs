// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Restore.Tests
{
    public class RestoreWithOlderNuGet : SdkTest
    {
        public RestoreWithOlderNuGet(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void ItCanBuildProjectRestoredWithNuGet5_7()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsSdkProject = true
            };
            testProject.PackageReferences.Add(new TestPackageReference("Humanizer.Core", "2.8.26"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new NuGetExeRestoreCommand(Log, testAsset.Path, testProject.Name);
            restoreCommand.NuGetExeVersion = "5.7.0";
            restoreCommand
                //  Workaround for CI machines where MSBuild workload resolver isn't enabled by default
                .WithEnvironmentVariable("MSBuildEnableWorkloadResolver", "false")
                .Execute()
                .Should()
                .Pass();

            new BuildCommand(testAsset)
                .ExecuteWithoutRestore()
                .Should()
                .Pass();
        }
    }
}
