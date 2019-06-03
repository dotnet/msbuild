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
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using NuGet.ProjectModel;
using NuGet.Common;
using Newtonsoft.Json.Linq;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildALibrary : SdkTest
    {
        public GivenThatWeWantToBuildALibrary(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netstandard1.5")]
        [InlineData("netcoreapp2.1")]
        [InlineData("netcoreapp3.0")]
        public void It_builds_the_library_successfully(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework, "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);
            buildCommand
                .Execute("/restore", "/bl:" + Path.Combine(libraryProjectDirectory, "build.binlog"))
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "TestLibrary.deps.json"
            });
        }

        [Fact]
        public void It_builds_the_library_twice_in_a_row()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource()
                .Restore(Log, relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        internal static List<string> GetValuesFromTestLibrary(
            ITestOutputHelper log,
            TestAssetsManager testAssetsManager,
            string itemTypeOrPropertyName,
            Action<GetValuesCommand> setup = null, 
            string[] msbuildArgs = null,
            GetValuesCommand.ValueType valueType = GetValuesCommand.ValueType.Item, 
            [CallerMemberName] string callingMethod = "", 
            Action<XDocument> projectChanges = null)
        {
            msbuildArgs = msbuildArgs ?? Array.Empty<string>();

            string targetFramework = "netstandard1.5";

            var testAsset = testAssetsManager
                .CopyTestAsset("AppWithLibrary", callingMethod)
                .WithSource();

            if (projectChanges != null)
            {
                testAsset.WithProjectChanges(projectChanges);
            }

            testAsset.Restore(log, relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(log, libraryProjectDirectory,
                targetFramework, itemTypeOrPropertyName, valueType);

            if (setup != null)
            {
                setup(getValuesCommand);
            }

            getValuesCommand
                .Execute(msbuildArgs)
                .Should()
                .Pass();

            var itemValues = getValuesCommand.GetValues();

            return itemValues;
        }

        private TestAsset CreateDocumentationFileLibraryAsset(bool? generateDocumentationFile, string documentationFile, string language, [CallerMemberName] string callingMethod = "")
        {
            string genDocFileIdentifier = generateDocumentationFile == null ? "null" : generateDocumentationFile.Value.ToString();
            string docFileIdentifier = documentationFile == null ? "null" : Path.GetFileName(documentationFile);
            string identifier = $"-genDoc={genDocFileIdentifier}, docFile={Path.GetFileName(docFileIdentifier)}";

            var testAssetName = "AppWithLibrary";
            if (language != "cs")
            {
                testAssetName += language.ToUpperInvariant();
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, callingMethod, identifier)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").FirstOrDefault();
                    propertyGroup.Should().NotBeNull();

                    if (generateDocumentationFile != null)
                    {
                        propertyGroup.Add(new XElement(ns + "GenerateDocumentationFile", generateDocumentationFile.Value.ToString()));
                    }
                    if (documentationFile != null)
                    {
                        propertyGroup.Add(new XElement(ns + "DocumentationFile", documentationFile));
                    }
                })
                .Restore(Log, relativePath: "TestLibrary");

            return testAsset;
        }

        [Theory]
        [InlineData("cs")]
        [InlineData("vb")]
        public void It_creates_a_documentation_file(string language)
        {
            var testAsset = CreateDocumentationFileLibraryAsset(true, null, language);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netstandard1.5");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "TestLibrary.deps.json",
                "TestLibrary.xml"
            });

            new DirectoryInfo(libraryProjectDirectory).Should().OnlyHaveFiles(new[]
            {
                $"Helper.{language}",
                $"TestLibrary.{language}proj"
            }, SearchOption.TopDirectoryOnly);
        }

        [Theory]
        [InlineData("cs", true)]
        [InlineData("cs", false)]
        [InlineData("vb", true)]
        [InlineData("vb", false)]
        public void It_allows_us_to_override_the_documentation_file_name(string language, bool setGenerateDocumentationFileProperty)
        {
            var testAsset = CreateDocumentationFileLibraryAsset(setGenerateDocumentationFileProperty ? (bool?)true : null, "TestLibDoc.xml", language,  "OverrideDocFileName");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netstandard1.5");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "TestLibrary.deps.json",
                "TestLibDoc.xml"
            });

            //  Due to the way the DocumentationFile works, if you specify an unrooted filename, then the documentation file will be generated in that
            //  location relative to the project folder, and then copied to the output folder.
            var expectedProjectDirectoryFiles = new List<string>()
            {
                $"Helper.{language}",
                $"TestLibrary.{language}proj"
            };

            // vb uses DocumentationFile relative to the IntermediateOutputPath
            if (language != "vb") {
                expectedProjectDirectoryFiles.Add("TestLibDoc.xml");
            }

            new DirectoryInfo(libraryProjectDirectory).Should().OnlyHaveFiles(expectedProjectDirectoryFiles, SearchOption.TopDirectoryOnly);
        }

        [Theory]
        [InlineData("cs", true)]
        [InlineData("cs", false)]
        [InlineData("vb", true)]
        [InlineData("vb", false)]
        public void It_does_not_create_a_documentation_file_if_GenerateDocumentationFile_property_is_false(string language, bool setDocumentationFileProperty)
        {
            var testAsset = CreateDocumentationFileLibraryAsset(false, setDocumentationFileProperty ? "TestLibDoc.xml" : null, language, "DoesntCreateDocFile");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netstandard1.5");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "TestLibrary.deps.json",
            });

            //  Make sure documentation file isn't generated in project folder either
            new DirectoryInfo(libraryProjectDirectory).Should().OnlyHaveFiles(new[]
            {
                $"Helper.{language}",
                $"TestLibrary.{language}proj"
            }, SearchOption.TopDirectoryOnly);
        }

        [Fact]
        public void Restore_succeeds_even_if_the_project_extension_is_for_a_different_language()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource();

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var oldProjectFile = Path.Combine(libraryProjectDirectory, "TestLibrary.csproj");
            var newProjectFile = Path.Combine(libraryProjectDirectory, "TestLibrary.different_language_proj");

            File.Move(oldProjectFile, newProjectFile);

            var restoreCommand = new RestoreCommand(Log, libraryProjectDirectory, "TestLibrary.different_language_proj");

            restoreCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Theory]
        [InlineData("Debug", "DEBUG")]
        [InlineData("Release", "RELEASE")]
        [InlineData("CustomConfiguration", "CUSTOMCONFIGURATION")]
        [InlineData("Debug-NetCore", "DEBUG_NETCORE")]
        public void It_implicitly_defines_compilation_constants_for_the_configuration(string configuration, string expectedDefine)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", "ImplicitConfigurationConstants", configuration)
                .WithSource()
                .Restore(Log, relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Log, libraryProjectDirectory,
                "netstandard1.5", "DefineConstants");

            getValuesCommand.ShouldCompile = true;
            getValuesCommand.Configuration = configuration;

            getValuesCommand
                .Execute("/p:Configuration=" + configuration)
                .Should()
                .Pass();

            var definedConstants = getValuesCommand.GetValues();

            definedConstants.Should().BeEquivalentTo(new[] { expectedDefine, "TRACE", "NETSTANDARD", "NETSTANDARD1_5" });
        }

        [Theory]
        [InlineData(".NETStandard,Version=v1.0", new[] { "NETSTANDARD", "NETSTANDARD1_0" }, false)]
        [InlineData("netstandard1.3", new[] { "NETSTANDARD", "NETSTANDARD1_3" }, false)]
        [InlineData("netstandard1.6", new[] { "NETSTANDARD", "NETSTANDARD1_6" }, false)]
        [InlineData("net45", new[] { "NETFRAMEWORK", "NET45" }, true)]
        [InlineData("net461", new[] { "NETFRAMEWORK", "NET461" }, true)]
        [InlineData("netcoreapp1.0", new[] { "NETCOREAPP", "NETCOREAPP1_0" }, false)]
        [InlineData("netcoreapp3.0", new[] { "NETCOREAPP", "NETCOREAPP3_0" }, false)]
        [InlineData(".NETPortable,Version=v4.5,Profile=Profile78", new string[] { }, false)]
        [InlineData(".NETFramework,Version=v4.0,Profile=Client", new string[] { "NETFRAMEWORK", "NET40" }, false)]
        [InlineData("Xamarin.iOS,Version=v1.0", new string[] { "XAMARINIOS", "XAMARINIOS1_0" }, false)]
        [InlineData("UnknownFramework,Version=v3.14", new string[] { "UNKNOWNFRAMEWORK", "UNKNOWNFRAMEWORK3_14" }, false)]
        public void It_implicitly_defines_compilation_constants_for_the_target_framework(string targetFramework, string[] expectedDefines, bool buildOnlyOnWindows)
        {
            bool shouldCompile = true;

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", "ImplicitFrameworkConstants", targetFramework)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    //  Update target framework in project
                    var ns = project.Root.Name.Namespace;
                    var targetFrameworkProperties = project.Root
                        .Elements(ns + "PropertyGroup")
                        .Elements(ns + "TargetFramework")
                        .ToList();

                    targetFrameworkProperties.Count.Should().Be(1);

                    if (targetFramework.Contains(",Version="))
                    {
                        //  We use the full TFM for frameworks we don't have built-in support for targeting, so we don't want to run the Compile target
                        shouldCompile = false;

                        var frameworkName = new FrameworkName(targetFramework);

                        var targetFrameworkProperty = targetFrameworkProperties.Single();
                        targetFrameworkProperty.AddBeforeSelf(new XElement(ns + "TargetFrameworkIdentifier", frameworkName.Identifier));
                        targetFrameworkProperty.AddBeforeSelf(new XElement(ns + "TargetFrameworkVersion", "v" + frameworkName.Version.ToString()));
                        if (!string.IsNullOrEmpty(frameworkName.Profile))
                        {
                            targetFrameworkProperty.AddBeforeSelf(new XElement(ns + "TargetFrameworkProfile", frameworkName.Profile));
                        }

                        //  For the NuGet restore task to work with package references, it needs the TargetFramework property to be set.
                        //  Otherwise we would just remove the property.
                        targetFrameworkProperty.SetValue(targetFramework);
                    }
                    else
                    {
                        shouldCompile = true;
                        targetFrameworkProperties.Single().SetValue(targetFramework);
                    }
                })
                .Restore(Log, relativePath: "TestLibrary");

            if (buildOnlyOnWindows && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shouldCompile = false;
            }

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Log, libraryProjectDirectory,
                targetFramework, "DefineConstants")
            {
                ShouldCompile = shouldCompile
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var definedConstants = getValuesCommand.GetValues();

            definedConstants.Should().BeEquivalentTo(new[] { "DEBUG", "TRACE" }.Concat(expectedDefines).ToArray());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_fails_gracefully_if_targetframework_is_empty(bool useSolution)
        {
            string targetFramework = "";
            TestInvalidTargetFramework("EmptyTargetFramework", targetFramework, useSolution,
                $"The TargetFramework value '{targetFramework}' was not recognized");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_fails_gracefully_if_targetframework_is_invalid(bool useSolution)
        {
            string targetFramework = "notaframework";
            TestInvalidTargetFramework("InvalidTargetFramework", targetFramework, useSolution,
                $"The TargetFramework value '{targetFramework}' was not recognized");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_fails_gracefully_if_targetframework_should_be_targetframeworks(bool useSolution)
        {
            string targetFramework = "netcoreapp2.0;net461";
            TestInvalidTargetFramework("InvalidTargetFramework", targetFramework, useSolution,
                $"The TargetFramework value '{targetFramework}' is not valid. To multi-target, use the 'TargetFrameworks' property instead");
        }

        private void TestInvalidTargetFramework(string testName, string targetFramework, bool useSolution, string expectedOutput)
        {
            var testProject = new TestProject()
            {
                Name = testName,
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };

            string identifier = useSolution ? "_Solution" : "";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, identifier);

            if (targetFramework.Contains(";"))
            {
                //  The TestProject class doesn't differentiate between TargetFramework and TargetFrameworks, and helpfully selects
                //  which property to use based on whether there's a semicolon.
                //  For this test, we need to override this behavior
                testAsset = testAsset.WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Element(ns + "PropertyGroup")
                        .Element(ns + "TargetFrameworks")
                        .Name = ns + "TargetFramework";
                });
            }

            TestCommand restoreCommand;
            TestCommand buildCommand;

            if (useSolution)
            {
                var dotnetCommand = new DotnetCommand(Log)
                {
                    WorkingDirectory = testAsset.TestRoot
                };

                dotnetCommand.Execute("new", "sln")
                    .Should()
                    .Pass();

                var relativePathToProject = Path.Combine(testProject.Name, testProject.Name + ".csproj");
                dotnetCommand.Execute($"sln", "add", relativePathToProject)
                    .Should()
                    .Pass();

                var relativePathToSln = Path.GetFileName(testAsset.Path) + ".sln";

                restoreCommand = testAsset.GetRestoreCommand(Log, relativePathToSln);
                buildCommand = new BuildCommand(Log, testAsset.TestRoot, relativePathToSln);
            }
            else
            {
                restoreCommand = testAsset.GetRestoreCommand(Log, testProject.Name);
                buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            }

            //  Set RestoreContinueOnError=ErrorAndContinue to force failure on error
            //  See https://github.com/NuGet/Home/issues/5309
            var restore = restoreCommand.Execute("/p:RestoreContinueOnError=ErrorAndContinue");
            if (targetFramework.Contains(';'))
            {
                // Restore does not error on TargetFramework with semicolons, it treats it equivalently to TargetFrameworks.
                // The error is therefore deferred to the build check below.
                restore.Should().Pass();
            }
            else
            {
                // Intentionally not checking the error message on restore here as we can't put ourselves in front of
                // restore and customize the message for invalid target frameworks as that would break restoring packages
                // like MSBuild.Sdk.Extras that add support for extra TFMs.
                restore.Should().Fail();
            }

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(expectedOutput)
                .And.NotHaveStdOutContaining(">="); // old error about comparing empty string to version when TargetFramework was blank;
        }

        [Theory]
        [InlineData("netcoreapp3.1")]
        [InlineData("netstandard2.2")]
        public void It_fails_to_build_if_targeting_a_higher_framework_than_is_supported(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "TargetFrameworkVersionCap",
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, targetFramework);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: testProject.Name);

            restoreCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("The current .NET SDK does not support targeting");

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, testProject.Name);

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("The current .NET SDK does not support targeting");
        }

        [Fact]
        public void It_passes_ridless_target_to_compiler()
        {
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid("netcoreapp2.0");

            var testProject = new TestProject()
            {
                Name = "CompileDoesntUseRid",
                TargetFrameworks = "netcoreapp2.0",
                RuntimeIdentifier = runtimeIdentifier,
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(project =>
                {
                    //  Set property to disable logic in Microsoft.NETCore.App package that will otherwise cause a failure
                    //  when we remove everything under the rid-specific targets in the assets file
                    var ns = project.Root.Name.Namespace;
                    project.Root.Element(ns + "PropertyGroup")
                        .Add(new XElement(ns + "EnsureNETCoreAppRuntime", false));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, testProject.Name);

            //  Test that compilation doesn't depend on any rid-specific assets by removing them from the assets file after it's been restored
            var assetsFilePath = Path.Combine(buildCommand.GetBaseIntermediateDirectory().FullName, "project.assets.json");

            JObject assetsContents = JObject.Parse(File.ReadAllText(assetsFilePath));
            foreach (JProperty target in assetsContents["targets"])
            {
                if (target.Name.Contains("/"))
                {
                    //  This is a target element with a RID specified, so remove all its contents
                    target.Value = new JObject();
                }
            }
            string newContents = assetsContents.ToString();
            File.WriteAllText(assetsFilePath, newContents);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_can_target_uwp_using_sdk_extras()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("UwpUsingSdkExtras")
                .WithSource()
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public void It_marks_package_references_as_externally_resolved(bool? markAsExternallyResolved)
        {
            var project = new TestProject
            {
                Name = "Library",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true,
                // references from packages go through a different code path to be marked externally resolved.
                PackageReferences = { new TestPackageReference("NewtonSoft.Json", "10.0.1") }
            };

            var asset = _testAssetsManager.CreateTestProject(
                project,
                "ExternallyResolvedPackages",
                markAsExternallyResolved.ToString())
                .WithProjectChanges((path, p) =>
                {
                    if (markAsExternallyResolved != null)
                    {
                        var ns = p.Root.Name.Namespace;
                        p.Root.Add(
                            new XElement(ns + "PropertyGroup",
                                new XElement(ns + "MarkPackageReferencesAsExternallyResolved",
                                    markAsExternallyResolved)));
                    }
                })
                .Restore(Log, project.Name);

            var command = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, project.Name),
                project.TargetFrameworks,
                "Reference",
                GetValuesCommand.ValueType.Item);

            command.MetadataNames.Add("ExternallyResolved");
            command.Execute().Should().Pass();

            var references = command.GetValuesWithMetadata();
            references.Should().NotBeEmpty();

            foreach (var (value, metadata) in references)
            {
                metadata["ExternallyResolved"].Should().BeEquivalentTo((markAsExternallyResolved ?? true) ? "true" : "");
            }
        }
    }
}
