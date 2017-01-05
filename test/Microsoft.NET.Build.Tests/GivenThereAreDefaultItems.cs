// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThereAreDefaultItems : SdkTest
    {
        //[Fact]
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

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(_testAssetsManager, "Compile", setup);

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

        //[Fact]
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

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                project.Root.Element(ns + "PropertyGroup").Add(new XElement(ns + "EnableDefaultItems", "False"));

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", "**\\*.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(_testAssetsManager,
                "Compile", setup, new[] { "/p:DisableDefaultRemoves=true" }, GetValuesCommand.ValueType.Item,
                projectChanges: projectChanges);

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

        //[Fact]
        public void It_allows_items_outside_project_root_to_be_included()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                string sharedCodePath = Path.Combine(Path.GetDirectoryName(getValuesCommand.ProjectRootPath), "Shared");
                WriteFile(Path.Combine(sharedCodePath, "Shared.cs"),
                    "public class SharedClass {}");

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", "..\\Shared\\**\\*.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(_testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
                @"..\Shared\Shared.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        //[Fact]
        public void It_allows_a_project_subfolder_to_be_excluded()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Excluded", "Excluded.cs"),
                    "!InvalidCSharp!");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Remove", "Excluded\\**\\*.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(_testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        //[Fact]
        public void It_allows_a_CSharp_file_to_be_used_as_an_EmbeddedResource()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "CSharpAsResource.cs"),
                    "public class CSharpAsResource {}");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "EmbeddedResource", new XAttribute("Include", "CSharpAsResource.cs")));
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Remove", "CSharpAsResource.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(_testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);


            var embeddedResourceItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(_testAssetsManager, "EmbeddedResource", setup, projectChanges: projectChanges);

            var expectedEmbeddedResourceItems = new[]
            {
                "CSharpAsResource.cs"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            embeddedResourceItems.Should().BeEquivalentTo(expectedEmbeddedResourceItems);
        }

        //[Fact]
        public void It_allows_a_CSharp_file_to_be_used_as_Content()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "CSharpAsContent.cs"),
                    "public class CSharpAsContent {}");

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "None.txt"), "Content file");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Content", new XAttribute("Include", "CSharpAsContent.cs"),
                    new XAttribute("CopyToOutputDirectory", "true")));
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Remove", "CSharpAsContent.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(_testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);


            var contentItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(_testAssetsManager, "Content", setup, projectChanges: projectChanges);

            var expectedContentItems = new[]
            {
                "CSharpAsContent.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            contentItems.Should().BeEquivalentTo(expectedContentItems);

            var noneItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(_testAssetsManager, "None", setup, projectChanges: projectChanges);

            var expectedNoneItems = new[]
            {
                "None.txt"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            noneItems.Should().BeEquivalentTo(expectedNoneItems);
        }

        //[Fact]
        public void Default_items_have_the_correct_relative_paths()
        {
            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                var propertyGroup = new XElement(ns + "PropertyGroup");
                project.Root.Add(propertyGroup);
                propertyGroup.Add(new XElement(ns + "EnableDefaultContentItems", "false"));

                //  Copy all None items to output directory
                var itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "None", new XAttribute("Update", "@(None)"), new XAttribute("CopyToOutputDirectory", "PreserveNewest")));
            };

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource()
                .WithProjectChanges(projectChanges)
                .Restore(relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);

            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "ProjectRoot.txt"), "ProjectRoot");
            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "Subfolder", "ProjectSubfolder.txt"), "ProjectSubfolder");
            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "wwwroot", "wwwroot.txt"), "wwwroot");
            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "wwwroot", "wwwsubfolder", "wwwsubfolder.txt"), "wwwsubfolder");

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netstandard1.5");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "TestLibrary.deps.json",
                "ProjectRoot.txt",
                "Subfolder/ProjectSubfolder.txt",
                "wwwroot/wwwroot.txt",
                "wwwroot/wwwsubfolder/wwwsubfolder.txt",
            });
        }

        //[Fact]
        public void Compile_items_can_be_explicitly_specified_while_default_EmbeddedResource_items_are_used()
        {
            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                project.Root.Element(ns + "PropertyGroup").Add(
                    new XElement(ns + "EnableDefaultCompileItems", "false"));

                var itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", "Program.cs")));
            };

            Action<BuildCommand> setup = buildCommand =>
            {
                WriteFile(Path.Combine(buildCommand.ProjectRootPath, "ShouldNotBeCompiled.cs"),
                    "!InvalidCSharp!");
            };

            GivenThatWeWantAllResourcesInSatellite.TestSatelliteResources(_testAssetsManager, projectChanges, setup, "ExplicitCompileDefaultEmbeddedResource");
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

        private void WriteFile(string path, string contents)
        {
            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, contents);
        }
    }
}
