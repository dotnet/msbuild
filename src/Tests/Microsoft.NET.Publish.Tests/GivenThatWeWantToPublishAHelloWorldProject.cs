// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System;
using Microsoft.Extensions.DependencyModel;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAHelloWorldProject : SdkTest
    {
        public GivenThatWeWantToPublishAHelloWorldProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_publishes_portable_apps_to_the_publish_folder_and_the_app_should_run()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore(Log);

            var publishCommand = new PublishCommand(Log, helloWorldAsset.TestRoot);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json"
            });

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { Path.Combine(publishDirectory.FullName, "HelloWorld.dll") })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void It_publishes_self_contained_apps_to_the_publish_folder_and_the_app_should_run()
        {
            var targetFramework = "netcoreapp1.1";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "SelfContained")
                .WithSource()
                .Restore(Log, relativePath: "", args: $"/p:RuntimeIdentifiers={rid}");

            var publishCommand = new PublishCommand(Log, helloWorldAsset.TestRoot);
            var publishResult = publishCommand.Execute($"/p:RuntimeIdentifier={rid}");

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);
            var selfContainedExecutable = $"HelloWorld{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(publishDirectory.FullName, selfContainedExecutable);

            publishDirectory.Should().HaveFiles(new[] {
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
            });

            Command.Create(selfContainedExecutableFullPath, new string[] { })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void Publish_self_contained_app_with_dot_in_the_name()
        {
            var targetFramework = "netcoreapp2.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            TestProject testProject = new TestProject()
            {
                Name = "Hello.World",
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = rid,
                IsExe = true,
            };
            

            testProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello from a netcoreapp2.0.!"");
    }
}
";
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            testProjectInstance.Restore(Log, testProject.Name);
            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.TestRoot, testProject.Name));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);

            publishDirectory.Should().HaveFile($"Hello.World{Constants.ExeSuffix}");
        }

        //Note: Pre Netcoreapp2.0 stanalone activation uses renamed dotnet.exe
        //      While Post 2.0 we are shifting to using apphost.exe, so both publish needs to be validated
        [Fact]
        public void Publish_standalone_post_netcoreapp2_app_and_it_should_run()
        {
            var targetFramework = "netcoreapp2.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            TestProject testProject = new TestProject()
            {
                Name = "Hello",
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = rid,
                IsExe = true,
            };
            

            testProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello from a netcoreapp2.0.!"");
    }
}
";
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            testProjectInstance.Restore(Log, testProject.Name);
            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.TestRoot, testProject.Name));
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);
            var selfContainedExecutable = $"Hello{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(publishDirectory.FullName, selfContainedExecutable);

            publishDirectory.Should().HaveFiles(new[] {
                selfContainedExecutable,
                "Hello.dll",
                "Hello.pdb",
                "Hello.deps.json",
                "Hello.runtimeconfig.json",
                $"{FileConstants.DynamicLibPrefix}coreclr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                $"mscorlib.dll",
                $"System.Private.CoreLib.dll",
            });

            Command.Create(selfContainedExecutableFullPath, new string[] { })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello from a netcoreapp2.0.!");
        }
		
		//Note: Pre Netcoreapp2.0 standalone activation uses renamed dotnet.exe
        //      While Post 2.0 we are shifting to using apphost.exe, so both publish needs to be validated
        [CoreMSBuildOnlyTheory]
        [InlineData("win-arm")]
        [InlineData("win8-arm")]
        [InlineData("win81-arm")]
        [InlineData("win10-arm")]
        [InlineData("win10-arm64")]
        public void Publish_standalone_post_netcoreapp2_arm_app(string runtimeIdentifier)
        {
            // Tests for existence of expected files when publishing an ARM project
            // See https://github.com/dotnet/sdk/issues/1239

            var targetFramework = "netcoreapp2.0";

            TestProject testProject = new TestProject()
            {
                Name = "Hello",
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = runtimeIdentifier,
                IsExe = true,
            };
            
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

            testProjectInstance.Restore(Log, testProject.Name);
            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.TestRoot, testProject.Name));
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: runtimeIdentifier);
            
            // The name of the self contained executable depends on the runtime identifier.
            // For Windows family ARM publishing, it'll always be Hello.exe.
            // We shouldn't use "Constants.ExeSuffix" for the suffix here because that changes
            // depending on the RuntimeInformation
            var selfContainedExecutable = "Hello.exe";

            publishDirectory.Should().HaveFiles(new[] {
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
            });
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
            var rid = ridSpecific ? EnvironmentInfo.GetCompatibleRid(targetFramework) : null;

            TestProject testProject = new TestProject()
            {
                Name = selfContained ? "SelfContainedWithConflicts" :
                    (ridSpecific ? "RidSpecificSharedConflicts" : "PortableWithConflicts"),
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = rid,
                IsExe = true,
            };

            string outputMessage = $"Hello from {testProject.Name}!";

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
                })
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.TestRoot, testProject.Name));
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid ?? string.Empty);

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

            ICommand runCommand;

            if (selfContained)
            {
                var selfContainedExecutable = testProject.Name + Constants.ExeSuffix;

                string selfContainedExecutableFullPath = Path.Combine(publishDirectory.FullName, selfContainedExecutable);

                var libPrefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";

                publishDirectory.Should().HaveFiles(new[] {
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
                });

                dependencyContext.Should()
                    .OnlyHaveRuntimeAssembliesWhichAreInFolder(rid, publishDirectory.FullName)
                    .And
                    .OnlyHaveNativeAssembliesWhichAreInFolder(rid, publishDirectory.FullName, testProject.Name);

                runCommand = Command.Create(selfContainedExecutableFullPath, new string[] { });
            }
            else
            {
                publishDirectory.Should().OnlyHaveFiles(new[] {
                    $"{testProject.Name}.dll",
                    $"{testProject.Name}.pdb",
                    $"{testProject.Name}.deps.json",
                    $"{testProject.Name}.runtimeconfig.json"
                });

                dependencyContext.Should()
                    .OnlyHaveRuntimeAssemblies(rid ?? "", testProject.Name);

                runCommand = Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { Path.Combine(publishDirectory.FullName, $"{testProject.Name}.dll") });
            }

            runCommand
                    .CaptureStdOut()
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining(outputMessage);

        }

        [Fact]
        public void A_deployment_project_can_reference_the_hello_world_project()
        {
            var rid = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment.GetRuntimeIdentifier();

            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("DeployProjectReferencingSdkProject")
                .WithSource()
                .Restore(Log, relativePath: "HelloWorld", args: $"/p:RuntimeIdentifiers={rid}");

            var buildCommand = new BuildCommand(Log, helloWorldAsset.TestRoot, Path.Combine("DeployProj", "Deploy.proj"));

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
