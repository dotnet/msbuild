// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using System.Linq;
using FluentAssertions;
using System.Xml.Linq;
using System.Collections.Generic;
using System;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToUseContentFiles : SdkTest
    {
        public GivenThatWeWantToUseContentFiles(ITestOutputHelper log) : base(log)
        {
        }


        [Fact]
        public void It_handles_content_files_correctly()
        {
            const string targetFramework = "netcoreapp2.0";

            var project = new TestProject
            {
                Name = "ContentFiles",
                IsExe = true,
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                PackageReferences = { new TestPackageReference("ContentFilesExample", "1.0.2") },
            };

            project.SourceFiles[project.Name + ".cs"] =
$@"
using System;

namespace {project.Name}
{{
    static class Program
    {{
        static void Main()
        {{
            Console.WriteLine(ExampleReader.GetDataText());
        }}
    }}
}}";

            var asset = _testAssetsManager
                .CreateTestProject(project)
                .Restore(Log, project.Name);

            var cmd = new BuildCommand(Log, Path.Combine(asset.Path, project.Name));
            cmd.Execute().Should().Pass();

            cmd.GetOutputDirectory(targetFramework)
               .Should()
               .OnlyHaveFiles(
                    new[]
                    {
                        "ContentFiles.deps.json",
                        "ContentFiles.dll",
                        "ContentFiles.pdb",
                        "ContentFiles.runtimeconfig.dev.json",
                        "ContentFiles.runtimeconfig.json",
                        "tools/run.cmd",
                        "tools/run.sh",
                    }
                );
        }
    }
}
