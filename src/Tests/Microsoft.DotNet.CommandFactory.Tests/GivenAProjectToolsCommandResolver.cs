// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Microsoft.DotNet.CommandFactory;
using LocalizableStrings = Microsoft.DotNet.CommandFactory.LocalizableStrings;

namespace Microsoft.DotNet.Tests
{
    public class GivenAProjectToolsCommandResolver : SdkTest
    {
        private static readonly NuGetFramework s_toolPackageFramework =
            FrameworkConstants.CommonFrameworks.NetCoreApp22;

        private const string TestProjectName = "AppWithToolDependency";

        public GivenAProjectToolsCommandResolver(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItReturnsNullWhenCommandNameIsNull()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
                CommandArguments = new string[] { "" },
                ProjectDirectory = "/some/directory"
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItReturnsNullWhenProjectDirectoryIsNull()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "command",
                CommandArguments = new string[] { "" },
                ProjectDirectory = null
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItReturnsNullWhenProjectDirectoryDoesNotContainAProjectFile()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var projectDirectory = _testAssetsManager.CreateTestDirectory();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "command",
                CommandArguments = new string[] { "" },
                ProjectDirectory = projectDirectory.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItReturnsNullWhenCommandNameDoesNotExistInProjectTools()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = _testAssetsManager.CopyTestAsset(TestProjectName)
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance.Restore(Log);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItReturnsACommandSpecWithDOTNETAsFileNameAndCommandNameInArgsWhenCommandNameExistsInProjectTools()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = _testAssetsManager.CopyTestAsset(TestProjectName)
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance.Restore(Log);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandFile = Path.GetFileNameWithoutExtension(result.Path);

            commandFile.Should().Be("dotnet");

            result.Args.Should().Contain(commandResolverArguments.CommandName);
        }

        [Fact]
        public void ItEscapesCommandArgumentsWhenReturningACommandSpec()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = _testAssetsManager.CopyTestAsset(TestProjectName)
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance.Restore(Log);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                CommandArguments = new[] { "arg with space" },
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull("Because the command is a project tool dependency");
            result.Args.Should().Contain("\"arg with space\"");
        }

        [Fact]
        public void ItReturnsACommandSpecWithArgsContainingCommandPathWhenReturningACommandSpecAndCommandArgumentsAreNull()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = _testAssetsManager.CopyTestAsset(TestProjectName)
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance.Restore(Log);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandPath = result.Args.Trim('"');
            commandPath.Should().Contain("dotnet-portable.dll");
        }

        [Fact]
        public void ItReturnsACommandSpecWithArgsContainingCommandPathWhenInvokingAToolReferencedWithADifferentCasing()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = _testAssetsManager.CopyTestAsset(TestProjectName)
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance.Restore(Log);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-prefercliruntime",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandPath = result.Args.Trim('"');
            commandPath.Should().Contain("dotnet-prefercliruntime.dll");
        }

        [Fact]
        public void ItWritesADepsJsonFileNextToTheLockfile()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = _testAssetsManager.CopyTestAsset(TestProjectName)
                .WithSource()
                .WithRepoGlobalPackages();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance.Restore(Log);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var nugetPackagesRoot = TestContext.Current.TestGlobalPackagesFolder;

            var toolPathCalculator = new ToolPathCalculator(nugetPackagesRoot);

            var lockFilePath = toolPathCalculator.GetLockFilePath(
                "dotnet-portable",
                new NuGetVersion("1.0.0"),
                s_toolPackageFramework);

            var directory = Path.GetDirectoryName(lockFilePath);

            var depsJsonFile = Directory
                .EnumerateFiles(directory)
                .FirstOrDefault(p => Path.GetFileName(p).EndsWith(FileNameSuffixes.DepsJson));

            if (depsJsonFile != null)
            {
                File.Delete(depsJsonFile);
            }

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            new DirectoryInfo(directory)
                .Should().HaveFilesMatching("*.deps.json", SearchOption.TopDirectoryOnly);
        }

        [Fact]
        public void GenerateDepsJsonMethodDoesntOverwriteWhenDepsFileAlreadyExists()
        {
            var testInstance = _testAssetsManager.CopyTestAsset(TestProjectName)
                .WithSource()
                .WithRepoGlobalPackages();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance.Restore(Log);

            var toolPathCalculator = new ToolPathCalculator(TestContext.Current.TestGlobalPackagesFolder);

            var lockFilePath = toolPathCalculator.GetLockFilePath(
                "dotnet-portable",
                new NuGetVersion("1.0.0"),
                s_toolPackageFramework);

            var lockFile = new LockFileFormat().Read(lockFilePath);

            // NOTE: We must not use the real deps.json path here as it will interfere with tests running in parallel.
            var depsJsonFile = Path.GetTempFileName();
            File.WriteAllText(depsJsonFile, "temp");

            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();
            projectToolsCommandResolver.GenerateDepsJsonFile(
                lockFile,
                s_toolPackageFramework,
                depsJsonFile,
                new SingleProjectInfo("dotnet-portable", "1.0.0", Enumerable.Empty<ResourceAssemblyInfo>()),
                GetToolDepsJsonGeneratorProject());

            File.ReadAllText(depsJsonFile).Should().Be("temp");
            File.Delete(depsJsonFile);
        }

        [Fact]
        public void ItDoesNotAddFxVersionAsAParamWhenTheToolDoesNotHaveThePrefercliruntimeFile()
        {
            var projectToolsCommandResolver = SetupProjectToolsCommandResolver();

            var testInstance = _testAssetsManager.CopyTestAsset(TestProjectName)
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance.Restore(Log);

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-portable",
                CommandArguments = null,
                ProjectDirectory = testInstance.Path
            };

            var result = projectToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            result.Args.Should().NotContain("--fx-version");
        }

        [Fact]
        public void ItFindsToolsLocatedInTheNuGetFallbackFolder()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("AppWithFallbackFolderToolDependency")
                .WithSource();

            var testProjectDirectory = testInstance.Path;
            var fallbackFolder = Path.Combine(testProjectDirectory, "fallbackFolder");

            var nugetConfig = UseNuGetConfigWithFallbackFolder(testInstance, fallbackFolder, TestContext.Current.TestPackages);

            PopulateFallbackFolder(testProjectDirectory, fallbackFolder);

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute($"fallbackfoldertool").Should().Pass();
        }

        [Fact]
        public void ItShowsAnErrorWhenTheToolDllIsNotFound()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("AppWithFallbackFolderToolDependency")
                .WithSource();
            var testProjectDirectory = testInstance.Path;
            var fallbackFolder = Path.Combine(testProjectDirectory, "fallbackFolder");
            var nugetPackages = Path.Combine(testProjectDirectory, "nugetPackages");

            var nugetConfig = UseNuGetConfigWithFallbackFolder(testInstance, fallbackFolder, TestContext.Current.TestPackages);

            PopulateFallbackFolder(testProjectDirectory, fallbackFolder);

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute($"/p:RestorePackagesPath={nugetPackages}")
                .Should()
                .Pass();

            // We need to run the tool once to generate the deps.json
            // otherwise we end up with a different error message.

            new DotnetCommand(Log)
            .WithWorkingDirectory(testProjectDirectory)
            .Execute("fallbackfoldertool", $"/p:RestorePackagesPath={nugetPackages}").Should().Pass();

            Directory.Delete(Path.Combine(fallbackFolder, "dotnet-fallbackfoldertool"), true);

            new DotnetCommand(Log)
            .WithWorkingDirectory(testProjectDirectory)
            .Execute("fallbackfoldertool", $"/p:RestorePackagesPath={nugetPackages}")
            .Should().Fail().And.NotHaveStdOutContaining(string.Format(LocalizableStrings.CommandAssembliesNotFound, "dotnet-fallbackfoldertool"));
        }

        private void PopulateFallbackFolder(string testProjectDirectory, string fallbackFolder)
        {
            var nugetConfigPath = Path.Combine(testProjectDirectory, "NuGet.Config");

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--packages", fallbackFolder)
                .Should()
                .Pass();

            Directory.Delete(Path.Combine(fallbackFolder, ".tools"), true);
        }

        private string UseNuGetConfigWithFallbackFolder(TestAsset testInstance, string fallbackFolder, string testPackagesSource)
        {
            var nugetConfig = Path.Combine(testInstance.Path, "NuGet.Config");
            
            File.WriteAllText(
                nugetConfig,
                $@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                  <packageSources>
                    <add key=""{Guid.NewGuid().ToString()}"" value=""{testPackagesSource}"" />
                  </packageSources>
                  <fallbackPackageFolders>
                        <add key=""MachineWide"" value=""{fallbackFolder}""/>
                    </fallbackPackageFolders>
                </configuration>
                ");

            return nugetConfig;
        }

        private ProjectToolsCommandResolver SetupProjectToolsCommandResolver()
        {
            var packagedCommandSpecFactory = new PackagedCommandSpecFactoryWithCliRuntime();

            var projectToolsCommandResolver =
                new ProjectToolsCommandResolver(packagedCommandSpecFactory, new EnvironmentProvider());

            return projectToolsCommandResolver;
        }

        private string GetToolDepsJsonGeneratorProject()
        {
            //  When using the product, the ToolDepsJsonGeneratorProject property is used to get this path, but for testing
            //  we'll hard code the path inside the SDK since we don't have a project to evaluate here
            return Path.Combine(TestContext.Current.ToolsetUnderTest.SdksPath, "Microsoft.NET.Sdk", "targets", "GenerateDeps", "GenerateDeps.proj");
        }
    }
}
