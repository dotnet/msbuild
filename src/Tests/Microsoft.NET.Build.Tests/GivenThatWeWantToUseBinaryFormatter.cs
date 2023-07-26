// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToUseBinaryFormatter : SdkTest
    {
        public GivenThatWeWantToUseBinaryFormatter(ITestOutputHelper log) : base(log)
        {
        }

        private const string SourceWithPragmaSuppressions = @"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

#pragma warning disable SYSLIB0011

namespace BinaryFormatterTests
{
    public class TestClass
    {
        public static void Main(string[] args)
        {
            var formatter = new BinaryFormatter();
            var stream = new MemoryStream();
            var deserializedObj = formatter.Deserialize(stream);
        }
    }
}";

        private const string SourceWithoutPragmaSuppressions = @"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace BinaryFormatterTests
{
    public class TestClass
    {
        public static void Main(string[] args)
        {
            var formatter = new BinaryFormatter();
            var stream = new MemoryStream();
            var deserializedObj = formatter.Deserialize(stream);
        }
    }
}";

        [Theory]
        [InlineData("netcoreapp3.1")]
        [InlineData("netstandard2.0")]
        [InlineData("net472")]
        public void It_does_not_warn_when_targeting_downlevel_frameworks(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "BinaryFormatterTests",
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            testProject.SourceFiles.Add("TestClass.cs", SourceWithoutPragmaSuppressions);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
            var buildCommand = new BuildCommand(testAsset, "BinaryFormatterTests");

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("SYSLIB0011");
        }

        [Theory]
        [InlineData("netcoreapp3.1")]
        [InlineData("netstandard2.0")]
        [InlineData("net472")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_does_not_warn_on_any_framework_when_using_pragma_suppressions(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "BinaryFormatterTests",
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            testProject.SourceFiles.Add("TestClass.cs", SourceWithPragmaSuppressions);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
            var buildCommand = new BuildCommand(testAsset, "BinaryFormatterTests");

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("warning SYSLIB0011")
                .And
                .NotHaveStdOutContaining("error SYSLIB0011");
        }

        [Theory]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void It_warns_when_targeting_certain_frameworks_and_not_using_pragma_suppressions(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "BinaryFormatterTests",
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            testProject.SourceFiles.Add("TestClass.cs", SourceWithoutPragmaSuppressions);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
            var buildCommand = new BuildCommand(testAsset, "BinaryFormatterTests");

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("warning SYSLIB0011")
                .And
                .NotHaveStdOutContaining("error SYSLIB0011");
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_errors_when_targeting_certain_frameworks_and_not_using_pragma_suppressions(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "BinaryFormatterTests",
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            testProject.SourceFiles.Add("TestClass.cs", SourceWithoutPragmaSuppressions);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
            var buildCommand = new BuildCommand(testAsset, "BinaryFormatterTests");

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .NotHaveStdOutContaining("warning SYSLIB0011")
                .And
                .HaveStdOutContaining("error SYSLIB0011");
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_allows_downgrading_errors_to_warnings_via_project_config(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "BinaryFormatterTests",
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            testProject.SourceFiles.Add("TestClass.cs", SourceWithoutPragmaSuppressions);
            testProject.AdditionalProperties["EnableUnsafeBinaryFormatterSerialization"] = "true";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
            var buildCommand = new BuildCommand(testAsset, "BinaryFormatterTests");

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("warning SYSLIB0011")
                .And
                .NotHaveStdOutContaining("error SYSLIB0011");
        }
    }
}
