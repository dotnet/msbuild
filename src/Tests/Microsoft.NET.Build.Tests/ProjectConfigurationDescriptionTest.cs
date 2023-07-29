// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
