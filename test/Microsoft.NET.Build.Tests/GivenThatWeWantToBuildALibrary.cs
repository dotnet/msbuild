// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Linq;
using FluentAssertions;
using System.Xml.Linq;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildALibrary
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

       [Fact]
        public void It_builds_the_library_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource()
                .Restore(relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netstandard1.5");

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
                .Restore(relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void All_props_and_targets_add_themselves_to_MSBuildAllTargets()
        {
            //  Workaround MSBuild bug causing preprocessing to fail if there is a "--" in a resolved Sdk path: https://github.com/Microsoft/msbuild/pull/1428
            if (RepoInfo.RepoRoot.Contains("--"))
            {
                return;
            }

            List<string> expectedAllProjects = new List<string>();
            string baseIntermediateDirectory = null;

            var allProjectsFromProperty = GetValuesFromTestLibrary("MSBuildAllProjects", getValuesCommand =>
            {
                baseIntermediateDirectory = getValuesCommand.GetBaseIntermediateDirectory().FullName;

                string preprocessedFile = Path.Combine(getValuesCommand.GetOutputDirectory("netstandard1.5").FullName, "preprocessed.xml");

                //  Preprocess the file, and then scan it to find all the project files that were imported.  The preprocessed output
                //  includes comments with long lines of "======", between which is the import statement and then the path to the file
                //  that was imported
                getValuesCommand.Execute("/pp:" + preprocessedFile)
                    .Should()
                    .Pass();

                string previousLine = null;
                bool insideDelimiters = false;
                int lineNumber = 0;
                foreach (string line in File.ReadAllLines(preprocessedFile))
                {
                    lineNumber++;
                    if (line.All(c => c == '=') && line.Length >= 80)
                    {
                        if (insideDelimiters)
                        {
                            if (!previousLine.Trim().Equals("</Import>", StringComparison.OrdinalIgnoreCase))
                            {
                                //  MSBuild replaces "--" with "__" in the filenames, since a double hyphen isn't allowed in XML comments per the spec.
                                //  This causes problems on the CI machines where the path includes "---".  So convert it back here.
                                previousLine = previousLine.Replace("__", "--");

                                expectedAllProjects.Add(previousLine);
                            }
                        }
                        insideDelimiters = !insideDelimiters;
                    }

                    previousLine = line;
                }

                File.Delete(preprocessedFile);
            }, valueType: GetValuesCommand.ValueType.Property);

            string dotnetRoot = Path.GetDirectoryName(RepoInfo.DotNetHostPath);

            expectedAllProjects = expectedAllProjects.Distinct().ToList();

            var expectedBuiltinProjects = expectedAllProjects.Where(project => project.StartsWith(dotnetRoot, StringComparison.OrdinalIgnoreCase)).ToList();
            var expectedIntermediateProjects = expectedAllProjects.Where(project => project.StartsWith(baseIntermediateDirectory, StringComparison.OrdinalIgnoreCase)).ToList();
            var expectedOtherProjects = expectedAllProjects
                .Except(expectedBuiltinProjects)
                .Except(expectedIntermediateProjects)
                //  TODO: Remove this when https://github.com/NuGet/Home/issues/3851 is fixed
                .Where(project => !Path.GetFileName(project).Equals("NuGet.Build.Tasks.Pack.targets", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var builtinProjectsFromProperty = allProjectsFromProperty.Where(project => project.StartsWith(dotnetRoot, StringComparison.OrdinalIgnoreCase)).ToList();
            var intermediateProjectsFromProperty = allProjectsFromProperty.Where(project => project.StartsWith(baseIntermediateDirectory, StringComparison.OrdinalIgnoreCase)).ToList();
            var otherProjectsFromProperty = allProjectsFromProperty.Except(builtinProjectsFromProperty).Except(intermediateProjectsFromProperty).ToList();

            otherProjectsFromProperty.Should().BeEquivalentTo(expectedOtherProjects);

            //  TODO: Uncomment the following lines when the following bugs are fixed:
            //          - https://github.com/Microsoft/msbuild/issues/1298
            //          - https://github.com/NuGet/Home/issues/3851
            //          - https://github.com/dotnet/cli/issues/4571
            //          - https://github.com/dotnet/roslyn/issues/14870
            //  Tracking bug in the SDK repo for this: https://github.com/dotnet/sdk/issues/380

            //intermediateProjectsFromProperty.Should().BeEquivalentTo(expectedIntermediateProjects);
            //builtinProjectsFromProperty.Should().BeEquivalentTo(expectedBuiltinProjects);
        }

        [Fact]
        public void It_ignores_excluded_folders()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                foreach (string folder in new[] { "bin", "obj", "packages" })
                {
                    WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, folder, "source.cs"),
                        "!InvalidCSharp!");
                }

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
            };

            var compileItems = GetValuesFromTestLibrary("Compile", setup);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        [Fact]
        public void It_allows_excluded_folders_to_be_overridden()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                foreach (string folder in new[] { "bin", "obj", "packages" })
                {
                    WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, folder, "source.cs"),
                        $"public class ClassFrom_{folder} {{}}");
                }

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
            };

            var compileItems = GetValuesFromTestLibrary("Compile", setup, new[] { "/p:DisableDefaultRemoves=true" }, GetValuesCommand.ValueType.Item);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
                @"bin\source.cs",
                @"obj\source.cs",
                @"packages\source.cs"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        void RemoveGeneratedCompileItems(List<string> compileItems)
        {
            //  Remove auto-generated compile items.
            //  TargetFrameworkAttribute comes from %TEMP%\{TargetFrameworkMoniker}.AssemblyAttributes.cs
            //  Other default attributes generated by .NET SDK (for example AssemblyDescriptionAttribute and AssemblyTitleAttribute) come from
            //      { AssemblyName}.AssemblyInfo.cs in the intermediate output path
            var itemsToRemove = compileItems.Where(i =>
                    i.EndsWith("AssemblyAttributes.cs", System.StringComparison.OrdinalIgnoreCase) ||
                    i.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var itemToRemove in itemsToRemove)
            {
                compileItems.Remove(itemToRemove);
            }
        }

        private List<string> GetValuesFromTestLibrary(string itemTypeOrPropertyName, Action<GetValuesCommand> setup = null, string[] msbuildArgs = null,
            GetValuesCommand.ValueType valueType = GetValuesCommand.ValueType.Item, [CallerMemberName] string callingMethod = "")
        {
            msbuildArgs = msbuildArgs ?? Array.Empty<string>();

            string targetFramework = "netstandard1.5";

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", callingMethod)
                .WithSource()
                .Restore(relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Stage0MSBuild, libraryProjectDirectory,
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

        private void WriteFile(string path, string contents)
        {
            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, contents);
        }

        private TestAsset CreateDocumentationFileLibraryAsset(bool? generateDocumentationFile, string documentationFile, [CallerMemberName] string callingMethod = "")
        {
            string genDocFileIdentifier = generateDocumentationFile == null ? "null" : generateDocumentationFile.Value.ToString();
            string docFileIdentifier = documentationFile == null ? "null" : Path.GetFileName(documentationFile);
            string identifier = $"-genDoc={genDocFileIdentifier}, docFile={Path.GetFileName(docFileIdentifier)}";

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", callingMethod, identifier)
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
                .Restore(relativePath: "TestLibrary");

            return testAsset;
        }

        [Fact]
        public void It_creates_a_documentation_file()
        {
            var testAsset = CreateDocumentationFileLibraryAsset(true, null);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);

            buildCommand
                //  Capture standard output so that warnings about missing XML documentation don't show up as warnings at the end of the SDK build
                .CaptureStdOut()
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
                "Helper.cs",
                "TestLibrary.csproj"
            }, SearchOption.TopDirectoryOnly);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_allows_us_to_override_the_documentation_file_name(bool setGenerateDocumentationFileProperty)
        {
            var testAsset = CreateDocumentationFileLibraryAsset(setGenerateDocumentationFileProperty ? (bool?)true : null, "TestLibDoc.xml", "OverrideDocFileName");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);

            buildCommand
                //  Capture standard output so that warnings about missing XML documentation don't show up as warnings at the end of the SDK build
                .CaptureStdOut()
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
            new DirectoryInfo(libraryProjectDirectory).Should().OnlyHaveFiles(new[]
            {
                "Helper.cs",
                "TestLibrary.csproj",
                "TestLibDoc.xml"
            }, SearchOption.TopDirectoryOnly);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_does_not_create_a_documentation_file_if_GenerateDocumentationFile_property_is_false(bool setDocumentationFileProperty)
        {
            var testAsset = CreateDocumentationFileLibraryAsset(false, setDocumentationFileProperty ? "TestLibDoc.xml" : null, "DoesntCreateDocFile");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);

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
                "Helper.cs",
                "TestLibrary.csproj"
            }, SearchOption.TopDirectoryOnly);
        }

        [Theory]
        [InlineData(".NETStandard,Version=v1.0", new[] { "NETSTANDARD1_0" }, false)]
        [InlineData("netstandard1.3", new[] { "NETSTANDARD1_3" }, false)]
        [InlineData("netstandard1.6", new[] { "NETSTANDARD1_6" }, false)]
        [InlineData("netstandard20", new[] { "NETSTANDARD2_0" }, false)]
        [InlineData("net45", new[] { "NET45" }, true)]
        [InlineData("net461", new[] { "NET461" }, true)]
        [InlineData("netcoreapp1.0", new[] { "NETCOREAPP1_0" }, false)]
        [InlineData(".NETPortable,Version=v4.5,Profile=Profile78", new string[] { }, false)]
        [InlineData(".NETFramework,Version=v4.0,Profile=Client", new string[] { "NET40" }, false)]
        [InlineData("Xamarin.iOS,Version=v1.0", new string[] { "XAMARINIOS1_0" }, false)]
        [InlineData("UnknownFramework,Version=v3.14", new string[] { "UNKNOWNFRAMEWORK3_14" }, false)]
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
                .Restore(relativePath: "TestLibrary");

            if (buildOnlyOnWindows && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shouldCompile = false;
            }

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Stage0MSBuild, libraryProjectDirectory,
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
    }
}