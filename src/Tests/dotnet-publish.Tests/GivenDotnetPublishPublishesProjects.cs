// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.Cli.Publish.Tests
{
    public class GivenDotnetPublishPublishesProjects : SdkTest
    {

        private static string _defaultConfiguration = "Release";

        public GivenDotnetPublishPublishesProjects(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItPublishesARunnablePortableApp()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new RestoreCommand(testInstance)
                .Execute()
                .Should().Pass();

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--framework", ToolsetInfo.CurrentTargetFramework)
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? _defaultConfiguration;
            var outputDll = Path.Combine(OutputPathCalculator.FromProject(testProjectDirectory).GetPublishDirectory(configuration: configuration), $"{testAppName}.dll");

            new DotnetCommand(Log)
                .Execute(outputDll)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenPublishing()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--framework", ToolsetInfo.CurrentTargetFramework)
                .Should().Pass();
        }

        [Fact]
        public void ItCanPublishAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = _testAssetsManager.CopyTestAsset(
                    "NETFrameworkReferenceNETStandard20",
                    testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
                .WithSource();

            string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("--framework", ToolsetInfo.CurrentTargetFramework)
                .Should().Pass();
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenPublishingWithTheNoRestoreOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--framework", "netcoreapp3.0", "--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Theory]
        [InlineData("publish", "-property", "Configuration=Debug")]
        [InlineData("publish", "-p", "Configuration=Debug")]
        [InlineData("publish", "--property", "Configuration=Debug")]
        public void ItParsesSpacedPropertiesInPublishReleaseEvaluationPhase(string command, string propertyKey, string propertyVal)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource()
                .Restore(Log);

            var rootDir = testInstance.Path;

            new DotnetCommand(Log)
                .WithWorkingDirectory(rootDir)
                .Execute(command, propertyKey, propertyVal)
                .Should().Pass().And.NotHaveStdErr();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("--sc")]
        [InlineData("--self-contained")]
        [InlineData("--sc=true")]
        [InlineData("--self-contained=true")]
        public void ItPublishesSelfContainedWithRid(string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = EnvironmentInfo.GetCompatibleRid();
            var outputDirectory = PublishApp(testAppName, rid, args);

            var outputProgram = Path.Combine(outputDirectory.FullName, $"{testAppName}{Constants.ExeSuffix}");

            new RunExeCommand(Log, outputProgram)
                .Execute()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItPublishesSelfContainedWithPublishSelfContainedTrue()
        {
            var testAppName = "MSBuildTestApp";
            var rid = EnvironmentInfo.GetCompatibleRid();
            var outputDirectory = PublishApp(testAppName, rid, "-p:PublishSelfContained=true");

            var outputProgram = Path.Combine(outputDirectory.FullName, $"{testAppName}{Constants.ExeSuffix}");

            outputDirectory.Should().HaveFiles(new[] {
                "System.dll", // File that should only exist if self contained 
            });

            new RunExeCommand(Log, outputProgram)
                .Execute()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData(true, false, false)] // PublishSC sets SC to true even if SC is false in the project file
        [InlineData(false, false, false)] // PublishSC sets SC to false even if SC is true in the project file 
        [InlineData(true, true, false)] // PublishSC does not take effect if SC is global
        public void PublishSelfContainedPropertyDoesOrDoesntOverrideSelfContained(bool publishSelfContained, bool selfContainedIsGlobal, bool publishSelfContainedIsGlobal)
        {
            bool selfContained = !publishSelfContained;
            bool resultShouldBeSelfContained = publishSelfContained && !selfContainedIsGlobal;

            string targetFramework = ToolsetInfo.CurrentTargetFramework;
            var testProject = new TestProject("MainProject")
            {
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            testProject.RecordProperties("SelfContained");
            if (!publishSelfContainedIsGlobal)
                testProject.AdditionalProperties["PublishSelfContained"] = publishSelfContained.ToString();
            if (!selfContainedIsGlobal)
                testProject.AdditionalProperties["SelfContained"] = selfContained.ToString();

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: $"PSC-OVERRIDES-{publishSelfContained}-{selfContainedIsGlobal}-{publishSelfContainedIsGlobal}");
            var publishCommand = new DotnetCommand(Log);
            List<string> args = new List<string>
            {
                "publish",
                selfContainedIsGlobal ? $"/p:SelfContained={selfContained}" : "",
                publishSelfContainedIsGlobal ? $"/p:PublishSelfContained={publishSelfContained}" : "",
            };

            publishCommand
                .WithWorkingDirectory(Path.Combine(testAsset.Path, "MainProject"))
                .Execute(args.ToArray())
                .Should()
                .Pass();

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, configuration: "Release", targetFramework: targetFramework);

            if (resultShouldBeSelfContained)
            {
                Assert.True(bool.Parse(properties["SelfContained"]) == true);
            }
        }

        [Fact]
        public void ItFailsWith1193IfPublishSelfContainedHasInvalidValue()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: "NET1193Failure")
                .WithSource();

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute("-p:PublishSelfContained=Invalid")
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1193");
        }

        [Theory]
        [InlineData("--sc=false")]
        [InlineData("--self-contained=false")]
        [InlineData("--no-self-contained")]
        public void ItPublishesFrameworkDependentWithRid(string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = EnvironmentInfo.GetCompatibleRid();
            var outputDirectory = PublishApp(testAppName, rid, args);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testAppName}{Constants.ExeSuffix}",
                $"{testAppName}.dll",
                $"{testAppName}.pdb",
                $"{testAppName}.deps.json",
                $"{testAppName}.runtimeconfig.json",
            });

            var outputProgram = Path.Combine(outputDirectory.FullName, $"{testAppName}{Constants.ExeSuffix}");

            var command = new RunExeCommand(Log, outputProgram);
            command.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("--sc=false")]
        [InlineData("--self-contained=false")]
        [InlineData(null)]
        [InlineData("--no-self-contained")]
        public void ItPublishesFrameworkDependentWithoutRid(string args)
        {
            var testAppName = "MSBuildTestApp";
            var outputDirectory = PublishApp(testAppName, rid: null, args: args);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testAppName}{Constants.ExeSuffix}",
                $"{testAppName}.dll",
                $"{testAppName}.pdb",
                $"{testAppName}.deps.json",
                $"{testAppName}.runtimeconfig.json",
            });

            new DotnetCommand(Log)
                .Execute(Path.Combine(outputDirectory.FullName, $"{testAppName}.dll"))
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("--sc --no-self-contained")]
        [InlineData("--self-contained --no-self-contained")]
        [InlineData("--sc=true --no-self-contained")]
        [InlineData("--self-contained=true --no-self-contained")]
        public void ItFailsToPublishWithConflictingArgument(string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = EnvironmentInfo.GetCompatibleRid();

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, identifier: args)
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithRuntime(rid)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(args.Split())
                .Should().Fail()
                    .And.HaveStdErrContaining(CommonLocalizableStrings.SelfContainAndNoSelfContainedConflict);
        }

        private DirectoryInfo PublishApp(string testAppName, string rid, string args = null, [CallerMemberName] string callingMethod = "")
        {
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, callingMethod: callingMethod, identifier: $"{rid ?? "none"}_{args ?? "none"}")
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithRuntime(rid)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(args?.Split() ?? Array.Empty<string>())
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? _defaultConfiguration;
            return new DirectoryInfo(OutputPathCalculator.FromProject(testProjectDirectory).GetPublishDirectory(configuration: configuration, runtimeIdentifier: rid));
        }

        [Fact]
        public void ItPublishesAppWhenRestoringToSpecificPackageDirectory()
        {
            string dir = "pkgs";
            string args = $"--packages {dir}";

            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource()
                .Restore(Log);

            var rootDir = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(rootDir)
                .Execute("--no-restore")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? _defaultConfiguration;

            var outputProgram = Path.Combine(OutputPathCalculator.FromProject(rootDir).GetPublishDirectory(configuration: configuration), $"TestAppSimple.dll");

            new DotnetCommand(Log, outputProgram)
                .Execute()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItFailsToPublishWithNoBuildIfNotPreviouslyBuilt()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource()
                .Restore(Log);

            var rootPath = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-build")
                .Should()
                .Fail()
                .And.HaveStdOutContaining("MSB3030"); // "Could not copy ___ because it was not found."
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ItPublishesSuccessfullyWithNoBuildIfPreviouslyBuilt(bool selfContained)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple", identifier: selfContained.ToString())
                .WithSource();

            var rootPath = testInstance.Path;

            var rid = selfContained ? EnvironmentInfo.GetCompatibleRid() : "";
            var ridArgs = selfContained ? $"-r {rid}".Split() : Array.Empty<string>();
            var ridAndConfigurationArgs = ridArgs.ToList().Concat(new List<string> { "-c", "Release" });

            new DotnetBuildCommand(Log, rootPath)
                .Execute(ridAndConfigurationArgs)
                .Should()
                .Pass();

            new DotnetPublishCommand(Log, "--no-build")
                .WithWorkingDirectory(rootPath)
                .Execute(ridArgs)
                .Should()
                .Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? _defaultConfiguration;

            var outputProgram = Path.Combine(OutputPathCalculator.FromProject(rootPath).GetPublishDirectory(configuration: configuration, runtimeIdentifier: rid), $"TestAppSimple.dll");

            new DotnetCommand(Log, outputProgram)
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItFailsToPublishWithNoBuildIfPreviouslyBuiltWithoutRid()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            var rootPath = testInstance.Path;

            new BuildCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("-r", "win-x64", "--no-build")
                .Should()
                .Fail();
        }

        [Fact]
        public void DotnetPublishDoesNotPrintCopyrightInfo()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource();

            var cmd = new DotnetPublishCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("--nologo");

            cmd.Should().Pass();

            if (!TestContext.IsLocalized())
            {
                cmd.Should().NotHaveStdOutContaining("Copyright (C) Microsoft Corporation. All rights reserved.");
            }
        }

        [Fact]
        public void DotnetPublishAllowsPublishOutputDir()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource()
                .Restore(Log);

            var rootDir = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(rootDir)
                .Execute("--no-restore", "-o", "publish")
                .Should()
                .Pass();
        }


        [Fact]
        public void A_PublishRelease_property_does_not_override_other_command_configuration()
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld", "PublishPropertiesHelloWorld")
               .WithSource();

            File.WriteAllText(helloWorldAsset.Path + "/Directory.Build.props", "<Project><PropertyGroup><PublishRelease>true</PublishRelease></PropertyGroup></Project>");

            // Another command, which should not be affected by PublishRelease
            new BuildCommand(helloWorldAsset)
               .Execute();
            
            var expectedAssetPath = Path.Combine(helloWorldAsset.Path, "bin", "Release");
            Assert.False(Directory.Exists(expectedAssetPath));
        }
    }
}
