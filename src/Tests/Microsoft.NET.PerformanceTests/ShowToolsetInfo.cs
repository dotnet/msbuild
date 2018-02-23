using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Perf.Tests
{
    public class ShowToolsetInfo : SdkTest
    {
        public ShowToolsetInfo(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ShowToolsetPaths()
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            string[] propertiesToShow = new[]
            {
                "MSBuildBinPath",
                "MicrosoftNETBuildTasksAssembly",
            };

            foreach (var propertyName in propertiesToShow)
            {
                var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name),
                    testProject.TargetFrameworks, propertyName, GetValuesCommand.ValueType.Property);

                getValuesCommand.Execute()
                    .Should()
                    .Pass();

                Console.WriteLine(propertyName + ": " + getValuesCommand.GetValues().FirstOrDefault());
            }
        }
    }
}
