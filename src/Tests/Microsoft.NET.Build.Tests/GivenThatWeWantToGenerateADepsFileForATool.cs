// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

using Microsoft.DotNet.Cli.Utils;

using NuGet.Packaging;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToGenerateADepsFileForATool : SdkTest
    {
        public GivenThatWeWantToGenerateADepsFileForATool(ITestOutputHelper log) : base(log)
        {
        }

        //  Disabled on full Framework MSBuild due to https://github.com/dotnet/sdk/issues/1293
        [CoreMSBuildOnlyFact]
        public void It_creates_a_deps_file_for_the_tool_and_the_tool_runs()
        {
            TestProject toolProject = new()
            {
                Name = "TestTool",
                TargetFrameworks = "netcoreapp2.2", // netcoreapp2.2 is the highest possible project tools tfm
                IsExe = true
            };

            toolProject.AdditionalProperties.Add("PackageType", "DotnetCliTool");

            GenerateDepsAndRunTool(toolProject)
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Hello World!");
        }

        //  Disabled on full Framework MSBuild due to https://github.com/dotnet/sdk/issues/1293
        [CoreMSBuildOnlyFact]
        public void It_handles_conflicts_when_creating_a_tool_deps_file()
        {
            TestProject toolProject = new()
            {
                Name = "DependencyContextTool",
                TargetFrameworks = "netcoreapp2.2", // netcoreapp2.2 is the highest possible project tools tfm
                IsExe = true
            };

            toolProject.AdditionalProperties.Add("PackageType", "DotnetCliTool");

            toolProject.PackageReferences.Add(new TestPackageReference("Microsoft.Extensions.DependencyModel", "1.1.0", null));

            string toolSource = @"
using System;
using System.Linq;
using Microsoft.Extensions.DependencyModel;

class Program
{
    static void Main(string[] args)
    {
        if(DependencyContext.Default?.RuntimeGraph?.Any() == true)
        {
            Console.WriteLine(""Successfully loaded runtime graph"");
        }
        else
        {
            Console.WriteLine(""Couldn't load runtime graph"");
        }
    }
}";

            toolProject.SourceFiles.Add("Program.cs", toolSource);

            GenerateDepsAndRunTool(toolProject, "ToolConflictResolution")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Successfully loaded runtime graph");
        }

        //  This method duplicates a lot of logic from the CLI in order to test generating deps files for tools in the SDK repo
        private CommandResult GenerateDepsAndRunTool(TestProject toolProject, [CallerMemberName] string callingMethod = "")
        {
            DeleteFolder(Path.Combine(TestContext.Current.NuGetCachePath, toolProject.Name.ToLowerInvariant()));
            DeleteFolder(Path.Combine(TestContext.Current.NuGetCachePath, ".tools", toolProject.Name.ToLowerInvariant()));

            var toolProjectInstance = _testAssetsManager.CreateTestProject(toolProject, callingMethod, identifier: toolProject.Name);

            NuGetConfigWriter.Write(toolProjectInstance.TestRoot, NuGetConfigWriter.DotnetCoreBlobFeed);

            // Workaround https://github.com/dotnet/cli/issues/9701
            var useBundledNETCoreAppPackage = "/p:UseBundledNETCoreAppPackageVersionAsDefaultNetCorePatchVersion=true";

            var packCommand = new PackCommand(Log, Path.Combine(toolProjectInstance.TestRoot, toolProject.Name));

            packCommand.Execute(useBundledNETCoreAppPackage)
                .Should()
                .Pass();

            string nupkgPath = Path.Combine(packCommand.ProjectRootPath, "bin", "Debug");

            TestProject toolReferencer = new()
            {
                Name = "ToolReferencer",
                TargetFrameworks = "netcoreapp2.0"
            };

            var toolReferencerInstance = _testAssetsManager.CreateTestProject(toolReferencer, callingMethod, identifier: toolReferencer.Name)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "DotNetCliToolReference",
                        new XAttribute("Include", toolProject.Name),
                        new XAttribute("Version", "1.0.0")));
                });

            List<string> sources = new() { NuGetConfigWriter.DotnetCoreBlobFeed };
            sources.Add(nupkgPath);

            NuGetConfigWriter.Write(toolReferencerInstance.TestRoot, sources);
            var restoreCommand = toolReferencerInstance.GetRestoreCommand(Log, toolReferencer.Name);
            restoreCommand.Execute("/v:n").Should().Pass();

            string toolAssetsFilePath = Path.Combine(TestContext.Current.NuGetCachePath, ".tools", toolProject.Name.ToLowerInvariant(), "1.0.0", toolProject.TargetFrameworks, "project.assets.json");
            var toolAssetsFile = new LockFileFormat().Read(toolAssetsFilePath);

            var args = new List<string>();


            string currentToolsetSdksPath = TestContext.Current.ToolsetUnderTest.SdksPath;

            string generateDepsProjectDirectoryPath = Path.Combine(currentToolsetSdksPath, "Microsoft.NET.Sdk", "targets", "GenerateDeps");
            string generateDepsProjectFileName = "GenerateDeps.proj";

            args.Add($"/p:ProjectAssetsFile=\"{toolAssetsFilePath}\"");

            args.Add($"/p:ToolName={toolProject.Name}");

            string depsFilePath = Path.Combine(Path.GetDirectoryName(toolAssetsFilePath), toolProject.Name + ".deps.json");
            args.Add($"/p:ProjectDepsFilePath={depsFilePath}");

            var toolTargetFramework = toolAssetsFile.Targets.First().TargetFramework.GetShortFolderName();
            args.Add($"/p:TargetFramework={toolProject.TargetFrameworks}");

            //  Look for the .props file in the Microsoft.NETCore.App package, until NuGet
            //  generates .props and .targets files for tool restores (https://github.com/NuGet/Home/issues/5037)
            var platformLibrary = toolAssetsFile.Targets
                .Single()
                .Libraries
                .FirstOrDefault(e => e.Name.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase));

            if (platformLibrary != null)
            {
                string buildRelativePath = platformLibrary.Build.FirstOrDefault()?.Path;

                var platformLibraryPath = GetPackageDirectory(toolAssetsFile, platformLibrary);

                if (platformLibraryPath != null && buildRelativePath != null)
                {
                    //  Get rid of "_._" filename
                    buildRelativePath = Path.GetDirectoryName(buildRelativePath);

                    string platformLibraryBuildFolderPath = Path.Combine(platformLibraryPath, buildRelativePath);
                    var platformLibraryPropsFile = Directory.GetFiles(platformLibraryBuildFolderPath, "*.props").FirstOrDefault();

                    if (platformLibraryPropsFile != null)
                    {
                        args.Add($"/p:AdditionalImport={platformLibraryPropsFile}");
                    }
                }
            }

            args.Add("/v:n");

            var generateDepsCommand = new MSBuildCommand(Log, "BuildDepsJson", generateDepsProjectDirectoryPath, generateDepsProjectFileName);

            generateDepsCommand.ExecuteWithoutRestore(args)
                .Should()
                .Pass();

            new DirectoryInfo(generateDepsProjectDirectoryPath)
                 .Should()
                 .OnlyHaveFiles(new[] { generateDepsProjectFileName });

            var toolLibrary = toolAssetsFile.Targets
                .Single()
                .Libraries.FirstOrDefault(
                    l => StringComparer.OrdinalIgnoreCase.Equals(l.Name, toolProject.Name));

            var toolAssembly = toolLibrary?.RuntimeAssemblies
                .FirstOrDefault(r => Path.GetFileNameWithoutExtension(r.Path) == toolProject.Name);

            var toolPackageDirectory = GetPackageDirectory(toolAssetsFile, toolLibrary);

            var toolAssemblyPath = Path.Combine(
                toolPackageDirectory,
                toolAssembly.Path);

            var dotnetArgs = new List<string>();
            dotnetArgs.Add("exec");

            dotnetArgs.Add("--depsfile");
            dotnetArgs.Add(depsFilePath);

            foreach (var packageFolder in GetNormalizedPackageFolders(toolAssetsFile))
            {
                dotnetArgs.Add("--additionalprobingpath");
                dotnetArgs.Add(packageFolder);
            }

            dotnetArgs.Add(Path.GetFullPath(toolAssemblyPath));

            var toolCommandSpec = new SdkCommandSpec()
            {
                FileName = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                Arguments = dotnetArgs
            };
            TestContext.Current.AddTestEnvironmentVariables(toolCommandSpec.Environment);

            ICommand toolCommand = toolCommandSpec.ToCommand().CaptureStdOut();

            var toolResult = toolCommand.Execute();

            return toolResult;
        }

        private static void DeleteFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static IEnumerable<string> GetNormalizedPackageFolders(LockFile lockFile)
        {
            return lockFile.PackageFolders.Select(pf => pf.Path.TrimEnd(Path.DirectorySeparatorChar));
        }

        private static string GetPackageDirectory(LockFile lockFile, LockFileTargetLibrary library)
        {
            var packageFolders = GetNormalizedPackageFolders(lockFile);

            var packageFoldersCount = packageFolders.Count();
            var userPackageFolder = packageFoldersCount == 1 ? string.Empty : packageFolders.First();
            var fallbackPackageFolders = packageFoldersCount > 1 ? packageFolders.Skip(1) : packageFolders;

            var packageDirectory = new FallbackPackagePathResolver(userPackageFolder, fallbackPackageFolders)
                .GetPackageDirectory(library.Name, library.Version);

            return packageDirectory;
        }
    }
}
