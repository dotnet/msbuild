// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToRunILLink : SdkTest
    {
        public GivenThatWeWantToRunILLink(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_only_runs_when_switch_is_enabled(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeFalse();

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{unusedFrameworkAssembly}.dll");

            // Linker inputs are kept, including unused assemblies
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            File.Exists(unusedFrameworkDll).Should().BeTrue();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, unusedFrameworkAssembly).Should().BeTrue();
        }

        [Theory]
        [InlineData("netcoreapp3.0", true)]
        [InlineData("netcoreapp3.0", false)]
        public void ILLink_runs_and_creates_linked_app(string targetFramework, bool referenceClassLibAsPackage)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName, referenceClassLibAsPackage);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + referenceClassLibAsPackage)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{unusedFrameworkAssembly}.dll");

            // Intermediate assembly is kept by linker and published, but not unused assemblies
            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeFalse();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeFalse();
            DoesDepsFileHaveAssembly(depsFile, unusedFrameworkAssembly).Should().BeFalse();
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_accepts_root_descriptor(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            // Inject extra arguments to prevent the linker from
            // keeping the entire referenceProject assembly. The
            // linker by default runs in a conservative mode that
            // keeps all used assemblies, but in this case we want to
            // check whether the root descriptor actually roots only
            // the specified method.
            var extraArgs = $"-p link {referenceProjectName}";
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true",
                                   $"/p:_ExtraTrimmerArgs={extraArgs}", "/v:n").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            // With root descriptor, linker keeps specified roots but removes unused methods
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            DoesImageHaveMethod(unusedDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(unusedDll, "UnusedMethodToRoot").Should().BeTrue();
        }

        [Theory]
        [InlineData("_TrimmerBeforeFieldInit")]
        [InlineData("_TrimmerOverrideRemoval")]
        [InlineData("_TrimmerUnreachableBodies")]
        [InlineData("_TrimmerClearInitLocals")]
        [InlineData("_TrimmerUnusedInterfaces")]
        [InlineData("_TrimmerIPConstProp")]
        [InlineData("_TrimmerSealer")]
        public void ILLink_error_on_nonboolean_optimization_flag(string property)
        {
            var projectName = "HelloWorld";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: property);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true", $"/p:{property}=NonBool")
                .Should().Fail().And.HaveStdOutContaining("MSB4030");
        }

        [Fact]
        public void ILLink_respects_feature_settings_from_host_config()
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            // Set up a conditional feature substitution for the "FeatureDisabled" property
            AddFeatureDefinition(testProject, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                .WithProjectChanges(project => EmbedSubstitutions(project))
                // Set a matching RuntimeHostConfigurationOption, with Trim = "true"
                .WithProjectChanges(project => AddRuntimeConfigOption(project, trim: true))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true",
                                    $"/p:_ExtraTrimmerArgs=-p link {referenceProjectName}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var referenceDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(referenceDll).Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "FeatureAPI").Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "get_FeatureDisabled").Should().BeTrue();
            // Check that this method is removed when the feature is disabled
            DoesImageHaveMethod(referenceDll, "FeatureImplementation").Should().BeFalse();
        }

        [Fact]
        public void ILLink_ignores_host_config_settings_with_link_false()
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            // Set up a conditional feature substitution for the "FeatureDisabled" property
            AddFeatureDefinition(testProject, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                .WithProjectChanges(project => EmbedSubstitutions(project))
                // Set a matching RuntimeHostConfigurationOption, with Trim = "false"
                .WithProjectChanges(project => AddRuntimeConfigOption(project, trim: false))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true",
                                    $"/p:_ExtraTrimmerArgs=-p link {referenceProjectName}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var referenceDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(referenceDll).Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "FeatureAPI").Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "get_FeatureDisabled").Should().BeTrue();
            // Check that the feature substitution did not apply
            DoesImageHaveMethod(referenceDll, "FeatureImplementation").Should().BeTrue();
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_runs_incrementally(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var linkSemaphore = Path.Combine(intermediateDirectory, "Link.semaphore");

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreFirstModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            WaitForUtcNowToAdvance();

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreSecondModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            semaphoreFirstModifiedTime.Should().Be(semaphoreSecondModifiedTime);
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_defaults_keep_nonframework(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute("/v:n", $"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{unusedFrameworkAssembly}.dll");

            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, unusedFrameworkAssembly).Should().BeFalse();
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_does_not_include_leftover_artifacts_on_second_run(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateDirectory, "Link.semaphore");

            // Link, keeping classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreFirstModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            var publishedDllKeptFirstTimeOnly = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var linkedDllKeptFirstTimeOnly = Path.Combine(linkedDirectory, $"{referenceProjectName}.dll");
            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeTrue();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeTrue();

            // Delete kept dll from publish output (works around lack of incremental publish)
            File.Delete(publishedDllKeptFirstTimeOnly);

            // Remove root descriptor to change the linker behavior.
            WaitForUtcNowToAdvance();
            // File.SetLastWriteTimeUtc(Path.Combine(testAsset.TestRoot, testProject.Name, $"{projectName}.cs"), DateTime.UtcNow);
            testAsset = testAsset.WithProjectChanges(project => RemoveRootDescriptor(project));

            // Link, discarding classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreSecondModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            // Check that the linker actually ran again
            semaphoreFirstModifiedTime.Should().NotBe(semaphoreSecondModifiedTime);

            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeFalse();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeFalse();

            // "linked" intermediate directory does not pollute the publish output
            Directory.Exists(Path.Combine(publishDirectory, "linked")).Should().BeFalse();
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_error_on_portable_app(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute("/p:PublishTrimmed=true")
                .Should().Fail()
                .And.HaveStdOutContainingIgnoreCase("NETSDK1102");
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_displays_informational_warning(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:SelfContained=true", "/p:PublishTrimmed=true")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("https://aka.ms/dotnet-illink");
        }

        [Fact(Skip = "https://github.com/aspnet/AspNetCore/issues/12064")]
        public void ILLink_and_crossgen_process_razor_assembly()
        { 
            var targetFramework = "netcoreapp3.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = new TestProject
            {
                Name = "TestWeb",
                IsSdkProject = true,
                IsExe = true,
                ProjectSdk = "Microsoft.NET.Sdk.Web",
                TargetFrameworks = targetFramework,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        class Program
                        {
                            static void Main() {}
                        }",
                    ["Test.cshtml"] = @"
                        @page
                        @{
                            System.IO.Compression.ZipFile.OpenRead(""test.zip"");
                        }
                    ",
                },
                AdditionalProperties =
                {
                    ["RuntimeIdentifier"] = rid,
                    ["PublishTrimmed"] = "true",
                    ["PublishReadyToRun"] = "true",
                }
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute().Should().Pass();

            var publishDir = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid);
            publishDir.Should().HaveFile("System.IO.Compression.ZipFile.dll");
            GivenThatWeWantToPublishReadyToRun.DoesImageHaveR2RInfo(publishDir.File("TestWeb.Views.dll").FullName);
        }

        private static bool DoesImageHaveMethod(string path, string methodNameToCheck)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var peReader = new PEReader(fs))
            {
                var metadataReader = peReader.GetMetadataReader();
                foreach (var handle in metadataReader.MethodDefinitions)
                {
                    var methodDefinition = metadataReader.GetMethodDefinition(handle);
                    string methodName = metadataReader.GetString(methodDefinition.Name);
                    if (methodName == methodNameToCheck)
                        return true;
                }
            }
            return false;
        }

        private static bool DoesDepsFileHaveAssembly(string depsFilePath, string assemblyName)
        {
            DependencyContext dependencyContext;
            using (var fs = File.OpenRead(depsFilePath))
            {
                dependencyContext = new DependencyContextJsonReader().Read(fs);
            }

            return dependencyContext.RuntimeLibraries.Any(l =>
                l.RuntimeAssemblyGroups.Any(rag =>
                    rag.AssetPaths.Any(f =>
                        Path.GetFileName(f) == $"{assemblyName}.dll")));
        }

        static string unusedFrameworkAssembly = "System.IO";

        private TestPackageReference GetPackageReference(TestProject project, string callingMethod, string identifier)
        {
            var asset = _testAssetsManager.CreateTestProject(project, callingMethod: callingMethod, identifier: identifier);
            var pack = new PackCommand(Log, Path.Combine(asset.TestRoot, project.Name));
            pack.Execute().Should().Pass();

            return new TestPackageReference(project.Name, "1.0.0", pack.GetNuGetPackage(project.Name));
        }

        private void AddRootDescriptor(XDocument project, string rootDescriptorFileName)
        {
            var ns = project.Root.Name.Namespace;

            var itemGroup = new XElement(ns + "ItemGroup");
            project.Root.Add(itemGroup);
            itemGroup.Add(new XElement(ns + "TrimmerRootDescriptor",
                                       new XAttribute("Include", rootDescriptorFileName)));
        }

        private void RemoveRootDescriptor(XDocument project)
        {
            var ns = project.Root.Name.Namespace;

            project.Root.Elements(ns + "ItemGroup")
                .Where(ig => ig.Elements(ns + "TrimmerRootDescriptor").Any())
                .First().Remove();
        }

        [Fact]
        public void It_warns_when_targetting_netcoreapp_2_x()
        {
            var testProject = new TestProject()
            {
                Name = "ConsoleApp",
                TargetFrameworks = "netcoreapp2.2",
                IsSdkProject = true,
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute($"/p:PublishTrimmed=true")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(Strings.PublishTrimmedRequiresVersion30);
        }

        private void EnableNonFrameworkTrimming(XDocument project)
        {
            // Used to override the default linker options for testing
            // purposes. The default roots non-framework assemblies,
            // but we want to ensure that the linker is running
            // end-to-end by checking that it strips code from our
            // test projects.

            var ns = project.Root.Name.Namespace;

            var target = new XElement(ns + "Target",
                                      new XAttribute("AfterTargets", "_SetILLinkDefaults"),
                                      new XAttribute("Name", "_EnableNonFrameworkTrimming"));
            project.Root.Add(target);
            target.Add(new XElement(ns + "PropertyGroup",
                                     new XElement("_TrimmerDefaultAction", "link")));
            target.Add(new XElement(ns + "ItemGroup",
                                    new XElement("TrimmerRootAssembly",
                                                 new XAttribute("Remove", "@(TrimmerRootAssembly)")),
                                    new XElement("TrimmerRootAssembly",
                                                 new XAttribute("Include", "@(IntermediateAssembly->'%(FileName)')")),
                                    new XElement("_ManagedAssembliesToLink",
                                                 new XAttribute("Update", "@(_ManagedAssembliesToLink)"),
                                                 new XElement("action"))));
        }

        static readonly string substitutionsFilename = "ILLink.Substitutions.xml";

        private void EmbedSubstitutions(XDocument project)
        {
            var ns = project.Root.Name.Namespace;

            project.Root.Add (new XElement(ns + "ItemGroup",
                                new XElement("EmbeddedResource",
                                    new XAttribute("Include", substitutionsFilename),
                                    new XElement("LogicalName", substitutionsFilename))));
        }

        private void AddFeatureDefinition(TestProject testProject, string referenceAssemblyName)
        {
            // Add a feature definition that replaces the FeatureDisabled property when DisableFeature is true.
            testProject.EmbeddedResources[substitutionsFilename] = $@"
<linker>
  <assembly fullname=""{referenceAssemblyName}"" feature=""DisableFeature"" featurevalue=""true"">
    <type fullname=""ClassLib"">
      <method signature=""System.Boolean get_FeatureDisabled()"" body=""stub"" value=""true"" />
    </type>
  </assembly>
</linker>
";
        }

        private void AddRuntimeConfigOption(XDocument project, bool trim)
        {
            var ns = project.Root.Name.Namespace;

            project.Root.Add (new XElement(ns + "ItemGroup",
                                new XElement("RuntimeHostConfigurationOption",
                                    new XAttribute("Include", "DisableFeature"),
                                    new XAttribute("Value", "true"),
                                    new XAttribute("Trim", trim.ToString ()))));
        }

        private TestProject CreateTestProjectForILLinkTesting(
            string targetFramework,
            string mainProjectName,
            string referenceProjectName = null,
            bool usePackageReference = true,
            [CallerMemberName] string callingMethod = "",
            string referenceProjectIdentifier = "")
        {
            var testProject = new TestProject()
            {
                Name = mainProjectName,
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
            };

            testProject.SourceFiles[$"{mainProjectName}.cs"] = @"
using System;
public class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello world"");
    }
}
";

            if (referenceProjectName == null) {
                return testProject;
            }

            var referenceProject = new TestProject()
            {
                Name = referenceProjectName,
                // NOTE: If using a package reference for the reference project, it will be retrieved
                // from the nuget cache. Set the reference project TFM to the lowest common denominator
                // of these tests to prevent conflicts.
                TargetFrameworks = usePackageReference ? "netcoreapp3.0" : targetFramework,
                IsSdkProject = true
            };
            referenceProject.SourceFiles[$"{referenceProjectName}.cs"] = @"
using System;
public class ClassLib
{
    public void UnusedMethod()
    {
    }

    public void UnusedMethodToRoot()
    {
    }

    public static bool FeatureDisabled { get; }

    public static void FeatureAPI()
    {
        if (FeatureDisabled)
            return;

        FeatureImplementation();
    }

    public static void FeatureImplementation()
    {
    }
}
";

            if (usePackageReference)
            {
                var packageReference = GetPackageReference(referenceProject, callingMethod, referenceProjectIdentifier);
                testProject.PackageReferences.Add(packageReference);
                testProject.AdditionalProperties.Add(
                    "RestoreAdditionalProjectSources",
                    "$(RestoreAdditionalProjectSources);" + Path.GetDirectoryName(packageReference.NupkgPath));
            }
            else
            {
                testProject.ReferencedProjects.Add(referenceProject);
            }


            testProject.SourceFiles[$"{referenceProjectName}.xml"] = $@"
<linker>
  <assembly fullname=""{referenceProjectName}"">
    <type fullname=""ClassLib"">
      <method name=""UnusedMethodToRoot"" />
      <method name=""FeatureAPI"" />
    </type>
  </assembly>
</linker>
";

            return testProject;
        }
    }
}
