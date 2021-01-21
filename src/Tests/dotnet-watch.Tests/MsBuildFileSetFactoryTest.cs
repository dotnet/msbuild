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

        private static string DotNetHostPath => TestContext.Current.ToolsetUnderTest.DotNetHostPath;

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
                TargetFrameworks = "netcoreapp2.1",
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
                TargetFrameworks = "netcoreapp2.1",
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
                TargetFrameworks = "netcoreapp2.1;net461",
                AdditionalProperties =
                {
                    ["EnableDefaultCompileItems"] = "false",
                },
            });

            project.WithProjectChanges(d => d.Root.Add(XElement.Parse(
@"<ItemGroup>
    <Compile Include=""Class1.netcore.cs"" Condition=""'$(TargetFramework)'=='netcoreapp2.1'"" />
    <Compile Include=""Class1.desktop.cs"" Condition=""'$(TargetFramework)'=='net461'"" />
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
        public async Task ProjectReferences_OneLevel()
        {
            var project2 = _testAssets.CreateTestProject(new TestProject("Project2")
            {
                TargetFrameworks = "netstandard2.1",
            });

            var project1 = _testAssets.CreateTestProject(new TestProject("Project1")
            {
                TargetFrameworks = "netcoreapp2.1;net461",
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
                TargetFrameworks = "netcoreapp2.1;net461",
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
            var filesetFactory = new MsBuildFileSetFactory(DotNetHostPath, _reporter, projectA, output, waitOnError: false, trace: true);

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

        private Task<IFileSet> GetFileSet(TestAsset target)
        {
            string projectPath = GetTestProjectPath(target);
            return GetFileSet(new MsBuildFileSetFactory(DotNetHostPath, _reporter, projectPath, new OutputSink(), waitOnError: false, trace: false));
        }

        private static string GetTestProjectPath(TestAsset target) => Path.Combine(GetTestProjectDirectory(target), target.TestProject.Name + ".csproj");

        private async Task<IFileSet> GetFileSet(MsBuildFileSetFactory filesetFactory)
        {
            return await filesetFactory
                .CreateAsync(CancellationToken.None)
                .TimeoutAfter(TimeSpan.FromSeconds(30));
        }

        private static void WriteFile(TestAsset testAsset, string name, string contents = "")
        {
            var path = Path.Combine(GetTestProjectDirectory(testAsset), name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
        }

        private static string GetTestProjectDirectory(TestAsset testAsset)
            => Path.Combine(testAsset.Path, testAsset.TestProject.Name);
    }
}
