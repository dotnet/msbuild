// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Microsoft.DotNet.Tests
{
    public class PackagedCommandTests : SdkTest
    {
        public PackagedCommandTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("AppWithDirectAndToolDep")]
        [InlineData("AppWithToolDependency")]
        public void TestProjectToolIsAvailableThroughDriver(string appName)
        {
            var testInstance = _testAssetsManager.CopyTestAsset(appName)
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "portable")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().HaveStdOutContaining("Hello Portable World!")
                     .And.NotHaveStdErr()
                     .And.Pass();
        }

        [RequiresSpecificFrameworkTheory("netcoreapp1.1")]
        [InlineData(true)]
        [InlineData(false)]
        public void IfPreviousVersionOfSharedFrameworkIsInstalled_ToolsTargetingItRun(bool toolPrefersCLIRuntime)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("AppWithToolDependency", identifier: toolPrefersCLIRuntime ? "preferCLIRuntime" : "")
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance = testInstance.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;

                var toolReference = project.Descendants(ns + "DotNetCliToolReference")
                    .Where(tr => tr.Attribute("Include").Value == "dotnet-portable")
                    .Single();

                toolReference.Attribute("Include").Value =
                    toolPrefersCLIRuntime ? "dotnet-portable-v1-prefercli" : "dotnet-portable-v1";
            });

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(toolPrefersCLIRuntime ? "portable-v1-prefercli" : "portable-v1");

            result.Should().Pass()
                .And.HaveStdOutContaining("I'm running on shared framework version 1.1.2!");

        }

        [RequiresSpecificFrameworkFact("netcoreapp1.1")]
        public void IfAToolHasNotBeenRestoredForNetCoreApp2_0ItFallsBackToNetCoreApp1_x()
        {
            string toolName = "dotnet-portable-v1";

            var testInstance = _testAssetsManager.CopyTestAsset("AppWithToolDependency")
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            string projectFolder = null;

            testInstance = testInstance.WithProjectChanges((projectPath, project) =>
            {
                projectFolder = Path.GetDirectoryName(projectPath);
                var ns = project.Root.Name.Namespace;

                //  Remove reference to tool that won't restore on 1.x
                project.Descendants(ns + "DotNetCliToolReference")
                    .Where(tr => tr.Attribute("Include").Value == "dotnet-PreferCliRuntime")
                    .Remove();

                var toolReference = project.Descendants(ns + "DotNetCliToolReference")
                    .Where(tr => tr.Attribute("Include").Value == "dotnet-portable")
                    .Single();

                toolReference.Attribute("Include").Value = toolName;

                //  Restore tools for .NET Core 1.1
                project.Root.Element(ns + "PropertyGroup")
                    .Add(new XElement(ns + "DotnetCliToolTargetFramework", "netcoreapp1.1"));

                //  Use project-specific global packages folder
                project.Root.Element(ns + "PropertyGroup")
                    .Add(new XElement(ns + "RestorePackagesPath", @"$(MSBuildProjectDirectory)\packages"));

            });

            var toolFolder = Path.Combine(projectFolder,
                                          "packages",
                                          ".tools",
                                          toolName);


            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();
            
            var result = new DotnetCommand(Log)
                    .WithWorkingDirectory(testInstance.Path)
                    .Execute("portable-v1");

            result.Should().Pass()
                .And.HaveStdOutContaining("I'm running on shared framework version 1.1.2!");
        }

        [Fact]
        public void CanInvokeToolWhosePackageNameIsDifferentFromDllName()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("AppWithDepOnToolWithOutputName")
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("tool-with-output-name")
                .Should().HaveStdOutContaining("Tool with output name!")
                     .And.NotHaveStdErr()
                     .And.Pass();
        }

        [Fact]
        public void ItShowsErrorWhenToolIsNotRestored()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("AppWithNonExistingToolDependency", testAssetSubdirectory: "NonRestoredTestProjects")
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("nonexistingtool")
                .Should().Fail()
                    .And.HaveStdErrContaining(string.Format(LocalizableStrings.NoExecutableFoundMatchingCommand, "dotnet-nonexistingtool"));
        }

        [Fact]
        public void ItRunsToolRestoredToSpecificPackageDir()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("ToolWithRandomPackageName", testAssetSubdirectory: "NonRestoredTestProjects")
                .WithSource();

            var appWithDepOnToolDir = new DirectoryInfo(testInstance.Path).Sub("AppWithDepOnTool");
            var toolWithRandPkgNameDir = new DirectoryInfo(testInstance.Path).Sub("ToolWithRandomPackageName");
            var pkgsDir = new DirectoryInfo(testInstance.Path).CreateSubdirectory("pkgs");

            // 3ebdd4f1-a194-470a-b01a-4515672791d1
            //                         ^-- index = 24
            string randomPackageName = Guid.NewGuid().ToString().Substring(24);

            // TODO: This is a workaround for https://github.com/dotnet/cli/issues/5020
            SetGeneratedPackageName(appWithDepOnToolDir.File("AppWithDepOnTool.csproj"),
                                    randomPackageName);

            SetGeneratedPackageName(toolWithRandPkgNameDir.File("ToolWithRandomPackageName.csproj"),
                                    randomPackageName);


            new DotnetCommand(Log)
                .WithWorkingDirectory(toolWithRandPkgNameDir.FullName)
                .Execute("pack", "-o", pkgsDir.FullName, "/p:version=1.0.0")
                .Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(appWithDepOnToolDir.FullName)
                .Execute("restore", "--source", pkgsDir.FullName)
                .Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(appWithDepOnToolDir.FullName)
                .Execute("randompackage")
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World from tool!")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void ToolsCanAccessDependencyContextProperly()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("DependencyContextFromTool")
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            testInstance.Restore(Log);

            new DotnetCommand(Log, "dependency-context-test")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void TestProjectDependencyIsNotAvailableThroughDriver()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("AppWithDirectDep")
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            var currentDirectory = Directory.GetCurrentDirectory();

            CommandResult result = new DotnetCommand(Log, "hello")
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            result.StdErr.Should().Contain(string.Format(LocalizableStrings.NoExecutableFoundMatchingCommand, "dotnet-hello"));
            
            result.Should().Fail();        
        }

        private void SetGeneratedPackageName(FileInfo project, string packageName)
        {
            const string propertyName = "GeneratedPackageId";
            var p = ProjectRootElement.Open(project.FullName, new ProjectCollection(), true);
            p.AddProperty(propertyName, packageName);
            p.Save();
        }
    }
}
