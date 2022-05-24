// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Testing;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class MsBuildFileSetFactoryTest
    {
        private readonly IReporter _reporter;
        private readonly TestAssetsManager _testAssets;

        public MsBuildFileSetFactoryTest(ITestOutputHelper output)
        {
            _reporter = new TestReporter(output);
            _testAssets = new TestAssetsManager(output);
        }

        [Fact]
        public async Task FindsCustomWatchItems()
        {
            var project = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            });

            project.WithProjectChanges(d => d.Root.Add(XElement.Parse(
@"<ItemGroup>
    <Watch Include=""*.js"" Exclude=""gulpfile.js"" />
</ItemGroup>")));

            WriteFile(project, "Program.cs");
            WriteFile(project, "app.js");
            WriteFile(project, "gulpfile.js");

            var fileset = await GetFileSet(project);

            AssertEx.EqualFileList(
                GetTestProjectDirectory(project),
                new[]
                {
                    "Project1.csproj",
                    "Project1.cs",
                    "Program.cs",
                    "app.js"
                },
                fileset
            );
        }

        [Fact]
        public async Task ExcludesDefaultItemsWithWatchFalseMetadata()
        {
            var project = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = "net40",
                AdditionalProperties =
                {
                    ["EnableDefaultEmbeddedResourceItems"] = "false",
                },
            });

            project.WithProjectChanges(d => d.Root.Add(XElement.Parse(
@"<ItemGroup>
    <EmbeddedResource Include=""*.resx"" Watch=""false"" />
</ItemGroup>")));

            WriteFile(project, "Program.cs");
            WriteFile(project, "Strings.resx");

            var fileset = await GetFileSet(project);

            AssertEx.EqualFileList(
                GetTestProjectDirectory(project),
                new[]
                {
                    "Project1.csproj",
                    "Project1.cs",
                    "Program.cs",
                },
                fileset
            );
        }

        [Fact]
        public async Task SingleTfm()
        {
            var project = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                AdditionalProperties =
                {
                    ["BaseIntermediateOutputPath"] = "obj",
                },
            });

            WriteFile(project, "Program.cs");
            WriteFile(project, "Class1.cs");
            WriteFile(project, Path.Combine("obj", "Class1.cs"));
            WriteFile(project, Path.Combine("Properties", "Strings.resx"));

            var fileset = await GetFileSet(project);

            AssertEx.EqualFileList(
                GetTestProjectDirectory(project),
                new[]
                {
                    "Project1.csproj",
                    "Project1.cs",
                    "Program.cs",
                    "Class1.cs",
                    "Properties/Strings.resx",
                },
                fileset
            );
        }

        [Fact]
        public async Task MultiTfm()
        {
            var project = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net462",
                AdditionalProperties =
                {
                    ["EnableDefaultCompileItems"] = "false",
                },
            });

            project.WithProjectChanges(d => d.Root.Add(XElement.Parse(
$@"<ItemGroup>
    <Compile Include=""Class1.netcore.cs"" Condition=""'$(TargetFramework)'=='{ToolsetInfo.CurrentTargetFramework}'"" />
    <Compile Include=""Class1.desktop.cs"" Condition=""'$(TargetFramework)'=='net462'"" />
</ItemGroup>")));

            WriteFile(project, "Class1.netcore.cs");
            WriteFile(project, "Class1.desktop.cs");
            WriteFile(project, "Class1.notincluded.cs");

            var fileset = await GetFileSet(project);

            AssertEx.EqualFileList(
                GetTestProjectDirectory(project),
                new[]
                {
                    "Project1.csproj",
                    "Class1.netcore.cs",
                    "Class1.desktop.cs",
                },
                fileset
            );
        }

        [Fact]
        public async Task IncludesContentFiles()
        {
            var testDir = _testAssets.CreateTestDirectory();

            var project = WriteFile(testDir, Path.Combine("Project1.csproj"),
@"<Project Sdk=""Microsoft.NET.Sdk.Web"">
    <PropertyGroup>
        <TargetFramework>netstandard2.1</TargetFramework>
    </PropertyGroup>
</Project>");
            WriteFile(testDir, Path.Combine("Program.cs"));

            WriteFile(testDir, Path.Combine("wwwroot", "css", "app.css"));
            WriteFile(testDir, Path.Combine("wwwroot", "js", "site.js"));
            WriteFile(testDir, Path.Combine("wwwroot", "favicon.ico"));

            var fileset = await GetFileSet(project);

            AssertEx.EqualFileList(
                testDir.Path,
                new[]
                {
                    "Project1.csproj",
                    "Program.cs",
                    "wwwroot/css/app.css",
                    "wwwroot/js/site.js",
                    "wwwroot/favicon.ico",
                },
                fileset
            );
        }

        [Fact]
        public async Task IncludesContentFilesFromRCL()
        {
            var testDir = _testAssets.CreateTestDirectory();
            WriteFile(testDir, Path.Combine("RCL1", "RCL1.csproj"),
@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">
    <PropertyGroup>
        <TargetFramework>netcoreapp5.0</TargetFramework>
    </PropertyGroup>
</Project>
");
            WriteFile(testDir, Path.Combine("RCL1", "wwwroot", "css", "app.css"));
            WriteFile(testDir, Path.Combine("RCL1", "wwwroot", "js", "site.js"));
            WriteFile(testDir, Path.Combine("RCL1", "wwwroot", "favicon.ico"));

            var projectPath = WriteFile(testDir, Path.Combine("Project1", "Project1.csproj"),
@"<Project Sdk=""Microsoft.NET.Sdk.Web"">
    <PropertyGroup>
        <TargetFramework>netstandard2.1</TargetFramework>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include=""..\RCL1\RCL1.csproj"" />
    </ItemGroup>
</Project>");
            WriteFile(testDir, Path.Combine("Project1", "Program.cs"));


            var fileset = await GetFileSet(projectPath);

            AssertEx.EqualFileList(
                testDir.Path,
                new[]
                {
                    "Project1/Project1.csproj",
                    "Project1/Program.cs",
                    "RCL1/RCL1.csproj",
                    "RCL1/wwwroot/css/app.css",
                    "RCL1/wwwroot/js/site.js",
                    "RCL1/wwwroot/favicon.ico",
                },
                fileset
            );
        }

        [Fact]
        public async Task ProjectReferences_OneLevel()
        {
            var project2 = _testAssets.CreateTestProject(new TestProject("Project2")
            {
                TargetFrameworks = "netstandard2.1",
            });

            var project1 = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net462",
                ReferencedProjects = { project2.TestProject, },
            });

            var fileset = await GetFileSet(project1);

            AssertEx.EqualFileList(
                project1.TestRoot,
                new[]
                {
                    "Project2/Project2.csproj",
                    "Project2/Project2.cs",
                    "Project1/Project1.csproj",
                    "Project1/Project1.cs",
                },
                fileset
            );
        }

        [Fact]
        public async Task TransitiveProjectReferences_TwoLevels()
        {
            var project3 = _testAssets.CreateTestProject(new TestProject("Project3")
            {
                TargetFrameworks = "netstandard2.1",
            });

            var project2 = _testAssets.CreateTestProject(new TestProject("Project2")
            {
                TargetFrameworks = "netstandard2.1",
                ReferencedProjects = { project3.TestProject, },
            });

            var project1 = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net462",
                ReferencedProjects = { project2.TestProject, },
            });

            var fileset = await GetFileSet(project1);

            AssertEx.EqualFileList(
                project1.TestRoot,
                new[]
                {
                    "Project3/Project3.csproj",
                    "Project3/Project3.cs",
                    "Project2/Project2.csproj",
                    "Project2/Project2.cs",
                    "Project1/Project1.csproj",
                    "Project1/Project1.cs",
                },
                fileset
            );

            Assert.All(fileset, f => Assert.False(f.IsStaticFile, $"File {f.FilePath} should not be a static file."));
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/29213")]
        public async Task ProjectReferences_Graph()
        {
            // A->B,F,W(Watch=False)
            // B->C,E
            // C->D
            // D->E
            // F->E,G
            // G->E
            // W->U
            // Y->B,F,Z
            var testDirectory = _testAssets.CopyTestAsset("ProjectReferences_Graph")
                .WithSource()
                .Path;
            var projectA = Path.Combine(testDirectory, "A", "A.csproj");

            var output = new OutputSink();
            var options = GetWatchOptions();
            var filesetFactory = new MsBuildFileSetFactory(options, _reporter, projectA, output, waitOnError: false, trace: true);

            var fileset = await GetFileSet(filesetFactory);

            Assert.NotNull(fileset);

            _reporter.Output(string.Join(
                Environment.NewLine,
                output.Current.Lines.Select(l => "Sink output: " + l)));

            var includedProjects = new[] { "A", "B", "C", "D", "E", "F", "G" };
            AssertEx.EqualFileList(
                testDirectory,
                includedProjects
                    .Select(p => $"{p}/{p}.csproj"),
                fileset
            );

            // ensure each project is only visited once for collecting watch items
            Assert.All(includedProjects,
                projectName =>
                    Assert.Single(output.Current.Lines,
                        line => line.Contains($"Collecting watch items from '{projectName}'"))
            );
        }

        private Task<FileSet> GetFileSet(TestAsset target)
        {
            var projectPath = GetTestProjectPath(target);
            return GetFileSet(projectPath);
        }

        private Task<FileSet> GetFileSet(string projectPath)
        {
            DotNetWatchOptions options = GetWatchOptions();
            return GetFileSet(new MsBuildFileSetFactory(options, _reporter, projectPath, new OutputSink(), waitOnError: false, trace: false));
        }

        private static DotNetWatchOptions GetWatchOptions() => 
            new DotNetWatchOptions(false, false, false, false, false, false);

        private static string GetTestProjectPath(TestAsset target) => Path.Combine(GetTestProjectDirectory(target), target.TestProject.Name + ".csproj");

        private async Task<FileSet> GetFileSet(MsBuildFileSetFactory filesetFactory)
        {
            return await filesetFactory
                .CreateAsync(CancellationToken.None)
                .TimeoutAfter(TimeSpan.FromSeconds(30));
        }

        private static string WriteFile(TestAsset testAsset, string name, string contents = "")
        {
            var path = Path.Combine(GetTestProjectDirectory(testAsset), name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);

            return path;
        }

        private static string WriteFile(TestDirectory testAsset, string name, string contents = "")
        {
            var path = Path.Combine(testAsset.Path, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);

            return path;
        }

        private static string GetTestProjectDirectory(TestAsset testAsset)
            => Path.Combine(testAsset.Path, testAsset.TestProject.Name);
    }
}
