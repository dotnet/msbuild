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

        [Fact]
        public void ProjectConfigurationDescription_DefaultTest()
        {
            var testProj = new TestProject()
            {
                Name = "CompilationConstants",
                TargetFrameworks = "netcoreapp2.1;netcoreapp3.1",
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
                    #if NETCOREAPP2_1
                        Consol.WriteLine(""NETCOREAPP2_1"");
                    #endif
                    #if NETCOREAPP3_1
                        Console.WriteLine(""NETCOREAPP3_1"");
                    #endif
                }
            }");

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.Path, testProj.Name));
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(":: TargetFramework");
        }
    }
}
