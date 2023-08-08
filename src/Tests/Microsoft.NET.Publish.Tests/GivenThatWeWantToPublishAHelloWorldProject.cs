// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAHelloWorldProject : SdkTest
    {
        public GivenThatWeWantToPublishAHelloWorldProject(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp1.1")]
        [InlineData("netcoreapp2.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_publishes_portable_apps_to_the_publish_folder_and_the_app_should_run(string targetFramework)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(targetFramework))
            {
                return;
            }

            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var publishCommand = new PublishCommand(helloWorldAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework);
            var outputDirectory = publishDirectory.Parent;

            var filesPublished = new[] {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json"
            };

            outputDirectory.Should().HaveFiles(filesPublished);
            publishDirectory.Should().HaveFiles(filesPublished);

            new DotnetCommand(Log, Path.Combine(publishDirectory.FullName, "HelloWorld.dll"))
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Theory]
        [InlineData("netcoreapp1.1")]
        [InlineData("netcoreapp2.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_publishes_self_contained_apps_to_the_publish_folder_and_the_app_should_run(string targetFramework)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(targetFramework))
            {
                return;
            }

            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "SelfContained", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var publishCommand = new PublishCommand(helloWorldAsset);
            var publishResult = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:CopyLocalLockFileAssemblies=true");

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);
            var outputDirectory = publishDirectory.Parent;
            var selfContainedExecutable = $"HelloWorld{Constants.ExeSuffix}";

            var filesPublished = new[] {
                selfContainedExecutable,
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json",
                $"{FileConstants.DynamicLibPrefix}coreclr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                $"mscorlib.dll",
                $"System.Private.CoreLib.dll",
            };

            outputDirectory.Should().HaveFiles(filesPublished);
            publishDirectory.Should().HaveFiles(filesPublished);

            var filesNotPublished = new[] {
                $"apphost{Constants.ExeSuffix}"
            };

            outputDirectory.Should().NotHaveFiles(filesNotPublished);
            publishDirectory.Should().NotHaveFiles(filesNotPublished);

            string selfContainedExecutableFullPath = Path.Combine(publishDirectory.FullName, selfContainedExecutable);
            new RunExeCommand(Log, selfContainedExecutableFullPath)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void Publish_self_contained_app_with_dot_in_the_name()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            TestProject testProject = new TestProject()
            {
                Name = "Hello.World",
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = rid,
                IsExe = true,
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "true";
            testProject.SourceFiles["Program.cs"] = $@"
using System;
public static class Program
{{
    public static void Main()
    {{
        Console.WriteLine(""Hello from a {ToolsetInfo.CurrentTargetFramework}!"");
    }}
}}
";
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testProjectInstance);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);

            publishDirectory.Should().HaveFile($"Hello.World{Constants.ExeSuffix}");
        }

        [Theory]
        [InlineData("win-arm")]
        [InlineData("win8-arm")]
        [InlineData("win81-arm")]
        [InlineData($"{ToolsetInfo.LatestWinRuntimeIdentifier}-arm")]
        [InlineData($"{ToolsetInfo.LatestWinRuntimeIdentifier}-arm64")]
        public void Publish_standalone_post_netcoreapp2_arm_app(string runtimeIdentifier)
        {
            // Tests for existence of expected files when publishing an ARM project
            // See https://github.com/dotnet/sdk/issues/1239

            var targetFramework = "netcoreapp2.0";

            TestProject testProject = new TestProject()
            {
                Name = "Hello",
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = runtimeIdentifier,
                IsExe = true,
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "true";
            testProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello from an arm netcoreapp2.0 app!"");
    }
}
";
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: runtimeIdentifier);

            var publishCommand = new PublishCommand(testProjectInstance);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: runtimeIdentifier);
            var outputDirectory = publishDirectory.Parent;

            // The name of the self contained executable depends on the runtime identifier.
            // For Windows family ARM publishing, it'll always be Hello.exe.
            // We shouldn't use "Constants.ExeSuffix" for the suffix here because that changes
            // depending on the RuntimeInformation
            var selfContainedExecutable = "Hello.exe";

            var filesPublished = new[] {
                selfContainedExecutable,
                "Hello.dll",
                "Hello.pdb",
                "Hello.deps.json",
                "Hello.runtimeconfig.json",
                "coreclr.dll",
                "hostfxr.dll",
                "hostpolicy.dll",
                "mscorlib.dll",
                "System.Private.CoreLib.dll",
            };

            outputDirectory.Should().HaveFiles(filesPublished);
            publishDirectory.Should().HaveFiles(filesPublished);
        }

        [Fact]
        public void Conflicts_are_resolved_when_publishing_a_portable_app()
        {
            Conflicts_are_resolved_when_publishing(selfContained: false, ridSpecific: false);
        }

        [Fact]
        public void Conflicts_are_resolved_when_publishing_a_self_contained_app()
        {
            Conflicts_are_resolved_when_publishing(selfContained: true, ridSpecific: true);
        }

        [Fact]
        public void Conflicts_are_resolved_when_publishing_a_rid_specific_shared_framework_app()
        {
            Conflicts_are_resolved_when_publishing(selfContained: false, ridSpecific: true);
        }

        void Conflicts_are_resolved_when_publishing(bool selfContained, bool ridSpecific, [CallerMemberName] string callingMethod = "")
        {
            if (selfContained && !ridSpecific)
            {
                throw new ArgumentException("Self-contained apps must be rid specific");
            }

            var targetFramework = "netcoreapp2.0";
            if (!EnvironmentInfo.SupportsTargetFramework(targetFramework))
            {
                return;
            }
            var rid = ridSpecific ? EnvironmentInfo.GetCompatibleRid(targetFramework) : null;

            TestProject testProject = new TestProject()
            {
                Name = selfContained ? "SelfContainedWithConflicts" :
                    (ridSpecific ? "RidSpecificSharedConflicts" : "PortableWithConflicts"),
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = rid,
                IsExe = true,
            };

            string outputMessage = $"Hello from {testProject.Name}!";

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "true";
            testProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        TestConflictResolution();
        Console.WriteLine(""" + outputMessage + @""");
    }
" + ConflictResolutionAssets.ConflictResolutionTestMethod + @"
}
";
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(p =>
                {

                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
                    {
                        itemGroup.Add(new XElement(ns + "PackageReference",
                            new XAttribute("Include", dependency.Item1),
                            new XAttribute("Version", dependency.Item2)));
                    }

                    if (!selfContained && ridSpecific)
                    {
                        var propertyGroup = new XElement(ns + "PropertyGroup");
                        p.Root.Add(propertyGroup);

                        propertyGroup.Add(new XElement(ns + "SelfContained",
                            "false"));
                    }
                });

            var publishCommand = new PublishCommand(testProjectInstance);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid ?? string.Empty);
            var outputDirectory = publishDirectory.Parent;

            DependencyContext dependencyContext;
            using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, $"{testProject.Name}.deps.json")))
            {
                dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
            }

            dependencyContext.Should()
                .HaveNoDuplicateRuntimeAssemblies(rid ?? "")
                .And
                .HaveNoDuplicateNativeAssets(rid ?? "")
                .And
                .OnlyHavePackagesWithPathProperties();

            TestCommand runCommand;

            if (selfContained)
            {
                var selfContainedExecutable = testProject.Name + Constants.ExeSuffix;

                string selfContainedExecutableFullPath = Path.Combine(publishDirectory.FullName, selfContainedExecutable);

                var libPrefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";

                var filesPublished = new[] {
                    selfContainedExecutable,
                    $"{testProject.Name}.dll",
                    $"{testProject.Name}.pdb",
                    $"{testProject.Name}.deps.json",
                    $"{testProject.Name}.runtimeconfig.json",
                    $"{libPrefix}coreclr{FileConstants.DynamicLibSuffix}",
                    $"{libPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                    $"{libPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                    $"mscorlib.dll",
                    $"System.Private.CoreLib.dll",
                };

                outputDirectory.Should().HaveFiles(filesPublished);
                publishDirectory.Should().HaveFiles(filesPublished);

                dependencyContext.Should()
                    .OnlyHaveRuntimeAssembliesWhichAreInFolder(rid, publishDirectory.FullName)
                    .And
                    .OnlyHaveNativeAssembliesWhichAreInFolder(rid, publishDirectory.FullName, testProject.Name);

                runCommand = new RunExeCommand(Log, selfContainedExecutableFullPath);
            }
            else
            {
                var filesPublished = new[] {
                    $"{testProject.Name}.dll",
                    $"{testProject.Name}.pdb",
                    $"{testProject.Name}.deps.json",
                    $"{testProject.Name}.runtimeconfig.json"
                };

                outputDirectory.Should().HaveFiles(filesPublished);
                publishDirectory.Should().HaveFiles(filesPublished);

                dependencyContext.Should()
                    .OnlyHaveRuntimeAssemblies(rid ?? "", testProject.Name);

                runCommand = new DotnetCommand(Log, Path.Combine(publishDirectory.FullName, $"{testProject.Name}.dll"));
            }

            runCommand
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining(outputMessage);

        }

        [Fact]
        public void A_deployment_project_can_reference_the_hello_world_project()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("DeployProjectReferencingSdkProject")
                .WithSource();

            var buildCommand = new BuildCommand(helloWorldAsset, Path.Combine("DeployProj", "Deploy.proj"));

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_fails_for_unsupported_rid()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            var publishCommand = new PublishCommand(helloWorldAsset);
            var publishResult = publishCommand.Execute("/p:RuntimeIdentifier=notvalid");

            publishResult.Should().Fail();
        }

        [Fact]
        public void It_publishes_on_release_if_PublishRelease_property_set()
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld", "PublishReleaseHelloWorld")
               .WithSource()
               .WithTargetFramework(ToolsetInfo.CurrentTargetFramework);

            System.IO.File.WriteAllText(helloWorldAsset.Path + "/Directory.Build.props", "<Project><PropertyGroup><PublishRelease>true</PublishRelease></PropertyGroup></Project>");

            new BuildCommand(helloWorldAsset)
           .Execute()
           .Should()
           .Pass();

            var publishCommand = new DotnetPublishCommand(Log, helloWorldAsset.TestRoot);

            publishCommand
            .Execute()
            .Should()
            .Pass();

            var expectedAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.True(File.Exists(expectedAssetPath));
        }

        [Fact]
        public void It_respects_CLI_PublishRelease_over_project_PublishRelease_value()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "PublishReleaseHelloWorldCsProjRespect")
                .WithSource()
                .WithTargetFramework(ToolsetInfo.CurrentTargetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "PublishRelease", "true"));
                });

            new BuildCommand(helloWorldAsset)
           .Execute()
           .Should()
           .Pass();

            var publishCommand = new DotnetPublishCommand(Log, helloWorldAsset.TestRoot);

            publishCommand
            .Execute("-p:PublishRelease=false")
            .Should()
            .Pass();

            var expectedAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.True(File.Exists(expectedAssetPath));
            var releaseAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.False(File.Exists(releaseAssetPath)); // build will produce a debug asset, need to make sure this doesn't exist either.
        }

        [Fact]
        public void It_publishes_on_release_if_PublishRelease_property_set_in_sln()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.ENABLE_PUBLISH_RELEASE_FOR_SOLUTIONS, "true");

            var slnDir = _testAssetsManager
               .CopyTestAsset("TestAppWithSlnUsingPublishRelease", "PublishReleaseSln")
               .WithSource()
               .Path;

            new BuildCommand(Log, slnDir, "App.sln")
               .Execute()
               .Should()
               .Pass();

            var publishCommand = new DotnetCommand(Log)
                .WithWorkingDirectory(slnDir)
                .Execute(@"dotnet", "publish")
                .Should()
                .Pass();

            Environment.SetEnvironmentVariable(EnvironmentVariableNames.ENABLE_PUBLISH_RELEASE_FOR_SOLUTIONS, null);

            var expectedAssetPath = System.IO.Path.Combine(slnDir, "App", "bin", "Release", ToolsetInfo.CurrentTargetFramework, "publish", "App.dll");
            Assert.True(File.Exists(expectedAssetPath));

        }

        [Fact]
        public void It_passes_using_PublishRelease_with_conflicting_capitalization_but_same_values_across_solution_projects()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.ENABLE_PUBLISH_RELEASE_FOR_SOLUTIONS, "true");

            var slnDir = _testAssetsManager
               .CopyTestAsset("TestAppWithSlnUsingPublishReleaseConflictingCasing", "PublishReleaseConflictSln")
               .WithSource()
               .Path;

            new BuildCommand(Log, slnDir, "App.sln")
               .Execute()
               .Should()
               .Pass();

            var publishCommand = new DotnetCommand(Log)
                .WithWorkingDirectory(slnDir)
                .Execute(@"dotnet", "publish")
                .Should()
                .Pass();

            Environment.SetEnvironmentVariable(EnvironmentVariableNames.ENABLE_PUBLISH_RELEASE_FOR_SOLUTIONS, null);

            var expectedAssetPath = System.IO.Path.Combine(slnDir, "App", "bin", "Release", ToolsetInfo.CurrentTargetFramework, "publish", "App.dll");
            Assert.True(File.Exists(expectedAssetPath));

        }


        [Fact]
        public void It_warns_if_PublishRelease_set_on_sln_but_env_var_not_used()
        {
            var slnDir = _testAssetsManager
               .CopyTestAsset("TestAppWithSlnUsingPublishRelease", "PublishReleaseSlnNoEnvVar")
               .WithSource()
               .Path;

            new BuildCommand(Log, slnDir, "App.sln")
               .Execute()
               .Should()
               .Pass();

            var publishCommand = new DotnetCommand(Log)
                .WithWorkingDirectory(slnDir)
                .Execute(@"dotnet", "publish")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1190");
        }

        [Fact]
        public void It_publishes_correctly_in_PublishRelease_evaluation_despite_option_forwarded_format()
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld", $"PublishesWithProperyFormats")
               .WithSource()
               .WithTargetFramework(ToolsetInfo.CurrentTargetFramework);

            new BuildCommand(helloWorldAsset)
           .Execute()
           .Should()
           .Pass();

            var publishCommand = new DotnetPublishCommand(Log, helloWorldAsset.TestRoot);

            publishCommand
            .Execute("-f", ToolsetInfo.CurrentTargetFramework)
            .Should()
            .Pass().And.NotHaveStdErr();
        }

        [Fact]
        public void It_publishes_on_release_if_PublishRelease_property_set_in_csproj()
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld", "PublishReleaseHelloWorldCsProj")
               .WithSource()
               .WithTargetFramework(ToolsetInfo.CurrentTargetFramework)
               .WithProjectChanges(project =>
               {
                   var ns = project.Root.Name.Namespace;
                   var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                   propertyGroup.Add(new XElement(ns + "PublishRelease", "true"));
               });

            new BuildCommand(helloWorldAsset)
           .Execute()
           .Should()
           .Pass();

            var publishCommand = new DotnetPublishCommand(Log, helloWorldAsset.TestRoot);

            publishCommand
            .Execute()
            .Should()
            .Pass();

            var expectedAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.True(File.Exists(expectedAssetPath));
        }

        [Fact]
        public void PublishRelease_overrides_Configuration_Debug_on_proj()
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld", "PublishReleaseHelloWorldCsProjConfigOverride")
               .WithSource()
               .WithTargetFramework(ToolsetInfo.CurrentTargetFramework)
               .WithProjectChanges(project =>
               {
                   var ns = project.Root.Name.Namespace;
                   var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                   propertyGroup.Add(new XElement(ns + "PublishRelease", "true"));
                   propertyGroup.Add(new XElement(ns + "Configuration", "Debug"));
               });

            new BuildCommand(helloWorldAsset)
           .Execute()
           .Should()
           .Pass();

            var publishCommand = new DotnetPublishCommand(Log, helloWorldAsset.TestRoot);

            publishCommand
            .Execute()
            .Should()
            .Pass();

            var expectedAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.True(File.Exists(expectedAssetPath));
            var releaseAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.True(File.Exists(releaseAssetPath)); // build will produce a debug asset, need to make sure this doesn't exist either.
        }

        [Fact]
        public void PublishRelease_does_not_override_custom_Configuration_on_proj_and_logs()
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld", "PublishReleaseHelloWorldCsProjConfigOverrideCustom")
               .WithSource()
               .WithTargetFramework(ToolsetInfo.CurrentTargetFramework)
               .WithProjectChanges(project =>
               {
                   var ns = project.Root.Name.Namespace;
                   var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                   propertyGroup.Add(new XElement(ns + "PublishRelease", "true"));
                   propertyGroup.Add(new XElement(ns + "Configuration", "CUSTOM"));
               });

            new BuildCommand(helloWorldAsset)
           .Execute()
           .Should()
           .Pass();

            var publishCommand = new DotnetPublishCommand(Log, helloWorldAsset.TestRoot);

            publishCommand
            .Execute()
            .Should()
            .Pass()
            .And
            .HaveStdOutContaining(helloWorldAsset.Path) // match the logged string without being specific to localization
            .And
            .HaveStdOutContaining("PublishRelease");

            var releaseAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.False(File.Exists(releaseAssetPath)); // build will produce a debug asset, need to make sure this doesn't exist either.       
        }

        [Theory]
        [InlineData("-p:Configuration=Debug")]
        [InlineData("-property:Configuration=Debug")]
        [InlineData("--property:Configuration=Debug")]
        [InlineData("/p:Configuration=Debug")]
        [InlineData("-p:_IsPublishing=true;Configuration=Debug")]
        [InlineData("-p:_IsPublishing=true;Configuration=Debug;")]
        [InlineData("/property:Configuration=Debug")]
        public void PublishRelease_does_not_override_Configuration_property_across_formats(string configOpt)
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld", identifier: configOpt)
               .WithSource()
               .WithTargetFramework(ToolsetInfo.CurrentTargetFramework)
               .WithProjectChanges(project =>
               {
                   var ns = project.Root.Name.Namespace;
                   var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                   propertyGroup.Add(new XElement(ns + "PublishRelease", "true"));
               });

            new DotnetPublishCommand(Log, helloWorldAsset.TestRoot)
               .Execute(configOpt)
               .Should()
               .Pass().And.NotHaveStdErr();

            var expectedAssetPath = Path.Combine(helloWorldAsset.Path, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.True(File.Exists(expectedAssetPath));
            var releaseAssetPath = Path.Combine(helloWorldAsset.Path, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.False(File.Exists(releaseAssetPath)); // build will produce a debug asset, need to make sure this doesn't exist either.
        }

        [Theory]
        [InlineData("")]
        [InlineData("=")]
        public void PublishRelease_does_recognize_undefined_property(string propertySuffix)
        {
            string tfm = ToolsetInfo.CurrentTargetFramework;
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = tfm
            };

            testProject.RecordProperties("SelfContained");
            testProject.RecordProperties("PublishAot");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(Path.Combine(testAsset.TestRoot, MethodBase.GetCurrentMethod().Name))
                .Execute(("-p:SelfContained" + propertySuffix))
                .Should()
                .Pass();

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: tfm);

            Assert.Equal("", properties["SelfContained"]);
            Assert.Equal("", properties["PublishAot"]);
        }

        [Fact]
        public void PublishRelease_does_not_override_Configuration_option()
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld", "PublishReleaseHelloWorldCsProjConfigOptionOverride")
               .WithSource()
               .WithTargetFramework(ToolsetInfo.CurrentTargetFramework)
               .WithProjectChanges(project =>
               {
                   var ns = project.Root.Name.Namespace;
                   var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                   propertyGroup.Add(new XElement(ns + "PublishRelease", "true"));
               });

            new BuildCommand(helloWorldAsset)
           .Execute()
           .Should()
           .Pass();

            var publishCommand = new DotnetPublishCommand(Log, helloWorldAsset.TestRoot);

            publishCommand
            .Execute("--configuration", "Debug")
            .Should()
            .Pass();

            var expectedAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.True(File.Exists(expectedAssetPath));
            var releaseAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "HelloWorld.dll");
            Assert.False(File.Exists(releaseAssetPath)); // build will produce a debug asset, need to make sure this doesn't exist either.
        }

        [Theory]
        [InlineData("Debug")]
        [InlineData("Custom")]
        public void PublishRelease_interacts_similarly_with_PublishProfile_Configuration(string config)
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var rid = EnvironmentInfo.GetCompatibleRid(tfm);

            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", $"PublishReleaseHelloWorldCsProjPublishProfile{config}")
                .WithSource()
                .WithTargetFramework(ToolsetInfo.CurrentTargetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "PublishRelease", "true"));
                });

            var publishProfilesDirectory = Path.Combine(helloWorldAsset.Path, "Properties", "PublishProfiles");
            Directory.CreateDirectory(publishProfilesDirectory);

            File.WriteAllText(Path.Combine(publishProfilesDirectory, "test.pubxml"), $@"
            <Project>
              <PropertyGroup>
                <RuntimeIdentifier>{rid}</RuntimeIdentifier>
                <Configuration>{config}</Configuration>
              </PropertyGroup>
            </Project>
            ");

            new BuildCommand(helloWorldAsset)
           .Execute()
           .Should()
           .Pass();

            var publishCommand = new DotnetPublishCommand(Log, helloWorldAsset.Path);

            CommandResult publishOutput = publishCommand
            .Execute("/p:PublishProfile=test");

            publishOutput.Should().Pass();
            var releaseAssetPath = System.IO.Path.Combine(helloWorldAsset.Path, "bin", "Release", ToolsetInfo.CurrentTargetFramework, rid, "HelloWorld.dll");
            if (config == "Debug")
            {
                Assert.True(File.Exists(releaseAssetPath)); // We ignore Debug configuration and override it, IF its custom though, we dont use publishrelease.
            }
            else
            {
                Assert.False(File.Exists(releaseAssetPath)); // build will produce a debug asset, need to make sure this doesn't exist either.       
                publishOutput.Should().HaveStdOutContaining("PublishRelease");
            }
        }

        [Fact]
        public void It_allows_unsupported_rid_with_override()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("netcoreapp2.1");

            var publishCommand = new PublishCommand(helloWorldAsset);
            var publishResult = publishCommand.Execute("/p:RuntimeIdentifier=notvalid", "/p:EnsureNETCoreAppRuntime=false");

            publishResult.Should().Pass();
        }

        [Theory]
        [InlineData("netcoreapp2.1")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_preserves_newest_files_on_publish(string tfm)
        {
            var testProject = new TestProject()
            {
                Name = "PreserveNewestFilesOnPublish",
                TargetFrameworks = tfm,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, identifier: tfm);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute("-v:n")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Copying");

            publishCommand
                .Execute("-v:n")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("Copying");
        }

        [Fact]
        public void It_fails_if_nobuild_was_requested_but_build_was_invoked()
        {
            var testProject = new TestProject()
            {
                Name = "InvokeBuildOnPublish",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(project =>
                {
                    project.Root.Add(XElement.Parse(@"<Target Name=""InvokeBuild"" DependsOnTargets=""Build"" BeforeTargets=""Publish"" />"));
                });

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new PublishCommand(testAsset)
                .Execute("/p:NoBuild=true")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1085");
        }

        [WindowsOnlyFact]
        public void It_contains_no_duplicates_in_resolved_publish_assets_on_windows()
            => It_contains_no_duplicates_in_resolved_publish_assets("windows");

        [Theory]
        [InlineData("console")]
        [InlineData("web")]
        public void It_contains_no_duplicates_in_resolved_publish_assets(string type)
        {
            // Use a specific RID to guarantee a consistent set of assets
            var testProject = new TestProject()
            {
                Name = "NoDuplicatesInResolvedPublishAssets",
                TargetFrameworks = "netcoreapp3.0",
                RuntimeIdentifier = "win-x64",
                IsExe = true
            };

            switch (type)
            {
                case "windows":
                    testProject.ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop";
                    testProject.AdditionalProperties.Add("UseWpf", "true");
                    testProject.AdditionalProperties.Add("UseWindowsForms", "true");
                    break;
                case "console":
                    break;
                case "web":
                    testProject.ProjectSdk = "Microsoft.NET.Sdk.Web";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }

            testProject.PackageReferences.Add(new TestPackageReference("NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, identifier: type)
                .WithProjectChanges(project =>
                {
                    project.Root.Add(XElement.Parse(@"
<Target Name=""VerifyNoDuplicatesInPublishAssets"" AfterTargets=""Publish"">
    <RemoveDuplicates Inputs=""@(_ResolvedCopyLocalPublishAssets)"">
        <Output TaskParameter=""Filtered"" ItemName=""FilteredAssets""/>
    </RemoveDuplicates>
    <Message Condition=""'@(_ResolvedCopyLocalPublishAssets)' != '@(FilteredAssets)'"" Importance=""High"" Text=""Duplicate items are present in: @(_ResolvedCopyLocalPublishAssets)!"" />
    <ItemGroup>
        <AssetDestinationSubPaths Include=""@(_ResolvedCopyLocalPublishAssets->'%(DestinationSubPath)')"" />
    </ItemGroup>
    <RemoveDuplicates Inputs=""@(AssetDestinationSubPaths)"">
        <Output TaskParameter=""Filtered"" ItemName=""FilteredAssetDestinationSubPaths""/>
    </RemoveDuplicates>
    <Message Condition=""'@(AssetDestinationSubPaths)' != '@(FilteredAssetDestinationSubPaths)'"" Importance=""High"" Text=""Duplicate DestinationSubPaths are present in: @(AssetDestinationSubPaths)!"" />
</Target>"));
                });

            new PublishCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("Duplicate items are present")
                .And
                .NotHaveStdOutContaining("Duplicate filenames are present");
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(false, null)]
        [InlineData(true, null)]
        [InlineData(null, false)]
        [InlineData(null, true)]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void It_publishes_with_a_publish_profile(bool? selfContained, bool? useAppHost)
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var rid = EnvironmentInfo.GetCompatibleRid(tfm);

            var testProject = new TestProject()
            {
                Name = "ConsoleWithPublishProfile",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk;Microsoft.NET.Sdk.Publish",
                IsExe = true,
            };

            var identifer = (selfContained == null ? "null" : selfContained.ToString()) + (useAppHost == null ? "null" : useAppHost.ToString());
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: identifer);

            var projectDirectory = Path.Combine(testProjectInstance.Path, testProject.Name);
            var publishProfilesDirectory = Path.Combine(projectDirectory, "Properties", "PublishProfiles");
            Directory.CreateDirectory(publishProfilesDirectory);

            File.WriteAllText(Path.Combine(publishProfilesDirectory, "test.pubxml"), $@"
<Project>
  <PropertyGroup>
    <RuntimeIdentifier>{rid}</RuntimeIdentifier>
    {(selfContained.HasValue ? $"<SelfContained>{selfContained}</SelfContained>" : "")}
    {((!(selfContained ?? true) && useAppHost.HasValue) ? $"<UseAppHost>{useAppHost}</UseAppHost>" : "")}
  </PropertyGroup>
</Project>
");

            var command = new PublishCommand(testProjectInstance);
            command
                .Execute("/p:PublishProfile=test")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(targetFramework: tfm, runtimeIdentifier: rid);

            output.Should().HaveFiles(new[] {
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
            });

            if (selfContained ?? true)
            {
                output.Should().HaveFiles(new[] {
                    $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                    $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                });
            }
            else
            {
                output.Should().NotHaveFiles(new[] {
                    $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                    $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                });
            }

            if ((selfContained ?? true) || (useAppHost ?? true))
            {
                output.Should().HaveFile($"{testProject.Name}{Constants.ExeSuffix}");
            }
            else
            {
                output.Should().NotHaveFile($"{testProject.Name}{Constants.ExeSuffix}");
            }
        }

        [Fact]
        public void It_publishes_with_full_path_publish_profile()
        {
            var libProject = new TestProject()
            {
                Name = "LibProjectWithDifferentTFM",
                TargetFrameworks = "netstandard2.0",
            };

            var testProject = new TestProject()
            {
                Name = "ExeWithPublishProfile",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            testProject.ReferencedProjects.Add(libProject);

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    project.Root.Add(XElement.Parse(@"
<ItemDefinitionGroup>
  <ProjectReference>
    <GlobalPropertiesToRemove>%(GlobalPropertiesToRemove);WebPublishProfileFile</GlobalPropertiesToRemove>
  </ProjectReference>
</ItemDefinitionGroup>"));
                });

            var projectDirectory = Path.Combine(testProjectInstance.Path, testProject.Name);
            var projectPath = Path.Combine(projectDirectory, $"{testProject.Name}.csproj");
            var publishProfilesDirectory = Path.Combine(projectDirectory, "Properties", "PublishProfiles");
            var publishProfilePath = Path.Combine(publishProfilesDirectory, "test.pubxml");

            Directory.CreateDirectory(publishProfilesDirectory);
            File.WriteAllText(publishProfilePath, $@"
<Project>
  <PropertyGroup>
    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
  </PropertyGroup>
</Project>
");

            var command = new PublishCommand(testProjectInstance);
            command
                .Execute(
                    $"/p:WebPublishProfileFile={publishProfilePath}",
                    $"/p:ProjectToOverrideProjectExtensionsPath={projectPath}"
                )
                .Should()
                .Pass();
        }

        [Theory]
        [InlineData("invalidProfile", true)]
        [InlineData("invalidProfile.pubxml", true)]
        [InlineData("..\\Properties\\PublishProfiles\\invalidProfile.pubxml", true)]
        [InlineData("invalidProfile.txt", true)]
        [InlineData("testProfile", false)]
        [InlineData("testProfile.pubxml", false)]
        [InlineData("..\\Properties\\PublishProfiles\\testProfile.pubxml", false)]
        [InlineData("", false)]
        public void It_warns_with_an_invalid_publish_profile_NetSdk(string publishProfile, bool shouldWarn)
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;

            var testProject = new TestProject()
            {
                Name = "ConsoleWithPublishProfile",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk",
                IsExe = true,
            };

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: $"PublishProfile{publishProfile.Length}");

            var projectDirectory = Path.Combine(testProjectInstance.Path, testProject.Name);
            var publishProfilesDirectory = Path.Combine(projectDirectory, "Properties", "PublishProfiles");
            Directory.CreateDirectory(publishProfilesDirectory);

            File.WriteAllText(Path.Combine(publishProfilesDirectory, "testProfile.pubxml"), $@"
<Project>
  <PropertyGroup>
    <msbuildProperty>value</msbuildProperty>
  </PropertyGroup>
</Project>
");

            var command = new PublishCommand(testProjectInstance);
            if (shouldWarn)
            {
                command
                    .Execute($"/p:PublishProfile={publishProfile}")
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining("NETSDK1198");
            }
            else
            {
                command
                    .Execute($"/p:PublishProfile={publishProfile}")
                    .Should()
                    .Pass()
                    .And
                    .NotHaveStdOutContaining("NETSDK1198");
            }
        }

        [Theory]
        [InlineData("invalidProfile", true)]
        [InlineData("invalidProfile.pubxml", true)]
        [InlineData("..\\Properties\\PublishProfiles\\invalidProfile.pubxml", true)]
        [InlineData("invalidProfile.txt", true)]
        [InlineData("testProfile", false)]
        [InlineData("testProfile.pubxml", false)]
        [InlineData("..\\Properties\\PublishProfiles\\testProfile.pubxml", false)]
        [InlineData("Default", false)]
        [InlineData("", false)]
        public void It_warns_with_an_invalid_publish_profile_WebSdk(string publishProfile, bool shouldWarn)
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;

            var testProject = new TestProject()
            {
                Name = "WebWithPublishProfile",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk.Web",
                IsExe = true,
            };

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: $"PublishProfile{publishProfile.Length}");

            var projectDirectory = Path.Combine(testProjectInstance.Path, testProject.Name);
            var publishProfilesDirectory = Path.Combine(projectDirectory, "Properties", "PublishProfiles");
            Directory.CreateDirectory(publishProfilesDirectory);

            File.WriteAllText(Path.Combine(publishProfilesDirectory, "testProfile.pubxml"), $@"
<Project>
  <PropertyGroup>
    <msbuildProperty>value</msbuildProperty>
  </PropertyGroup>
</Project>
");

            var command = new PublishCommand(testProjectInstance);
            if (shouldWarn)
            {
                command
                    .Execute($"/p:PublishProfile={publishProfile}")
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining("NETSDK1198");
            }
            else
            {
                command
                    .Execute($"/p:PublishProfile={publishProfile}")
                    .Should()
                    .Pass()
                    .And
                    .NotHaveStdOutContaining("NETSDK1198");
            }
        }

        [Theory]
        [InlineData("--p:PublishReadyToRun=true")]
        [InlineData("-p:PublishSingleFile=true")]
        [InlineData("-p:PublishSelfContained=true")]
        public void It_publishes_with_implicit_rid_with_rid_specific_properties(string executeOptionsAndProperties)
        {
            var testProject = new TestProject()
            {
                Name = "PublishImplicitRid",
                TargetFrameworks = $"net472;{ToolsetInfo.CurrentTargetFramework}",
            };
            testProject.AdditionalProperties.Add("IsPublishable", "false");
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: executeOptionsAndProperties);

            var publishCommand = new DotnetPublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
               .Execute(executeOptionsAndProperties)
               .Should()
               .Pass()
               .And
               .NotHaveStdErrContaining("NETSDK1191"); // Publish Properties Requiring RID Checks 
        }

        [Fact]
        public void IsPublishableIsRespectedWhenMultitargeting()
        {
            var testProject = new TestProject()
            {
                Name = "PublishMultitarget",
                TargetFrameworks = $"net472;{ToolsetInfo.CurrentTargetFramework}"
            };
            testProject.AdditionalProperties.Add("IsPublishable", "false");
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("The 'Publish' target is not supported without specifying a target framework.");
        }
    }
}
