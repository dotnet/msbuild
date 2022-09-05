// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit.Abstractions;
using Xunit;

namespace Microsoft.NET.Build.Tests
{
    public class ProjectConfigurationDescription : SdkTest
    {
        public ProjectConfigurationDescription(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("17.2.1.25201")]
        public void ProjectConfigurationDescription_DefaultTest()
        {
            const string errorTargetFramework = "net48";

            var testProj = new TestProject()
            {
                Name = "MultitargetingConfigurationDescription",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};{errorTargetFramework}",
                IsExe = true,
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProj);
            File.WriteAllText(Path.Combine(testAsset.Path, testProj.Name, $"{testProj.Name}.cs"), @"
            using System;
            class Program
            {
                static void Main(string[] args)
                {
                    #if NET472_OR_GREATER
                        Consol.WriteLine(""NET472"");
                    #endif
                    #if NETCOREAPP
                        Console.WriteLine(""NETCOREAPP"");
                    #endif
                }
            }");

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.Path, testProj.Name));
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining($"::TargetFramework={errorTargetFramework}");
        }
    }
}
