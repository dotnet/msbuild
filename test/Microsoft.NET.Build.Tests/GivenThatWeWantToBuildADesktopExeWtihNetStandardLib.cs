// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;

using FluentAssertions;
using Xunit;

using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopExeWtihNetStandardLib : SdkTest
    {
        private const string AppName = "TestApp";
        private const string LibraryName = "TestLibrary";

        private const string TemplateName = "DesktopAppWithLibrary";
        private const string TemplateNamePackagesConfig = "DesktopAppWithLibrary-PackagesConfig";
        private const string TemplateNameNonSdk = "DesktopAppWithLibrary-NonSDK";

        public GivenThatWeWantToBuildADesktopExeWtihNetStandardLib(ITestOutputHelper log) : base(log)
        {
        }

        public enum ReferenceScenario
        {
            ProjectReference,
            RawFileName,
            HintPath
        };

        private void AddReferenceToLibrary(XDocument project, ReferenceScenario scenario)
        {
            var ns = project.Root.Name.Namespace;
            var itemGroup = project.Root
                .Elements(ns + "ItemGroup")
                .Where(ig => ig.Elements(ns + "Reference").Any())
                .FirstOrDefault();

            if (itemGroup == null)
            {
                itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
            }

            if (scenario == ReferenceScenario.ProjectReference)
            {
                itemGroup.Add(new XElement(ns + "ProjectReference",
                    new XAttribute("Include", $@"..\{LibraryName}\{LibraryName}.csproj")));
            }
            else
            {
                var binaryPath = $@"..\{LibraryName}\bin\$(Configuration)\netstandard2.0\{LibraryName}.dll";
                if (scenario == ReferenceScenario.HintPath)
                {
                    itemGroup.Add(new XElement(ns + "Reference",
                        new XAttribute("Include", LibraryName),
                        new XElement(ns + "HintPath", binaryPath)));
                }
                else if (scenario == ReferenceScenario.RawFileName)
                {
                    itemGroup.Add(new XElement(ns + "Reference",
                        new XAttribute("Include", binaryPath)));
                }
            }
        }

        private string GetTemplateName(bool isSdk, bool usePackagesConfig = false)
        {
            return isSdk ? TemplateName : usePackagesConfig ? TemplateNamePackagesConfig : TemplateNameNonSdk;
        }

        private bool IsAppProject(string projectPath)
        {
            return Path.GetFileNameWithoutExtension(projectPath).Equals(AppName, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsLibraryProject(string projectPath)
        {
            return Path.GetFileNameWithoutExtension(projectPath).Equals(LibraryName, StringComparison.OrdinalIgnoreCase);
        }

        [WindowsOnlyTheory]
        [InlineData(true, ReferenceScenario.ProjectReference)]
        [InlineData(true, ReferenceScenario.RawFileName)]
        [InlineData(true, ReferenceScenario.HintPath)]
        [InlineData(false, ReferenceScenario.ProjectReference)]
        [InlineData(false, ReferenceScenario.RawFileName)]
        [InlineData(false, ReferenceScenario.HintPath)]
        public void It_includes_netstandard(bool isSdk, ReferenceScenario scenario)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset(GetTemplateName(isSdk), identifier: scenario.ToString())
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (IsAppProject(projectPath))
                    {
                        AddReferenceToLibrary(project, scenario);
                    }
                });

            if (scenario != ReferenceScenario.ProjectReference)
            {
                testAsset.Restore(Log, relativePath: LibraryName);

                var libBuildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, LibraryName));
                libBuildCommand
                    .Execute()
                    .Should()
                    .Pass();
            }

            testAsset.Restore(Log, relativePath: AppName);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, AppName));
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = isSdk ? 
                buildCommand.GetOutputDirectory("net461") :
                buildCommand.GetNonSDKOutputDirectory();

            outputDirectory.Should().HaveFiles(new[] {
                "netstandard.dll",
                $"{AppName}.exe.config"
            });
        }

        [WindowsOnlyTheory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public void It_resolves_conflicts(bool isSdk, bool usePackagesConfig)
        {
            // TODO: need to restore packages.config based project correctly.

            var successMessage = "No conflicts found for support libs";

            var testAsset = _testAssetsManager
                .CopyTestAsset(GetTemplateName(isSdk, usePackagesConfig))
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (IsAppProject(projectPath))
                    {
                        var ns = project.Root.Name.Namespace;

                        AddReferenceToLibrary(project, ReferenceScenario.ProjectReference);

                        var itemGroup = new XElement(ns + "ItemGroup");
                        project.Root.Add(itemGroup);

                        // packages.config template already has a reference to NETStandard.Library 1.6.1
                        if (!usePackagesConfig)
                        {
                            // Reference the old package based NETStandard.Library.
                            itemGroup.Add(new XElement(ns + "PackageReference",
                                new XAttribute("Include", "NETStandard.Library"),
                                new XAttribute("Version", "1.6.1")));
                        }

                        // Add a target to validate that no conflicts are from support libs
                        var target = new XElement(ns + "Target",
                            new XAttribute("Name", "CheckForConflicts"),
                            new XAttribute("AfterTargets", "_HandlePackageFileConflicts"));
                        project.Root.Add(target);

                        target.Add(new XElement(ns + "FindUnderPath",
                            new XAttribute("Files", "@(_ConflictPackageFiles)"),
                            new XAttribute("Path", RepoInfo.BuildExtensionsMSBuildPath),
                            new XElement(ns + "Output",
                                new XAttribute("TaskParameter", "InPath"),
                                new XAttribute("ItemName", "_ConflictsInSupportLibs"))
                            ));
                        target.Add(new XElement(ns + "Message",
                            new XAttribute("Condition", "'@(_ConflictsInSupportLibs)' == ''"),
                            new XAttribute("Importance", "High"),
                            new XAttribute("Text", successMessage)));
                        target.Add(new XElement(ns + "Error",
                            new XAttribute("Condition", "'@(_ConflictsInSupportLibs)' != ''"),
                            new XAttribute("Text", "Found conflicts under support libs: @(_ConflictsInSupportLibs)")));
                    }
                });

            testAsset.Restore(Log, relativePath: AppName);

            // build should succeed without duplicates
            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, AppName));
            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("warning")
                .And
                .HaveStdOutContainingIgnoreCase(successMessage);

            var outputDirectory = isSdk ?
                buildCommand.GetOutputDirectory("net461") :
                buildCommand.GetNonSDKOutputDirectory();

            outputDirectory.Should().HaveFiles(new[] {
                "netstandard.dll",
                $"{AppName}.exe.config"
            });
        }

        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_does_not_include_netstandard_when_inbox(bool isSdk)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset(GetTemplateName(isSdk))
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (IsAppProject(projectPath))
                    {
                        AddReferenceToLibrary(project, ReferenceScenario.ProjectReference);

                        // Add a target that replaces the facade folder with the set of netstandard support assemblies
                        // this can be replaced by targeting the version of .NETFramework that includes netstandard inbox,
                        // once available
                        var facadesDir = Path.Combine(RepoInfo.BuildExtensionsMSBuildPath, "net461", "ref\\");
                        var ns = project.Root.Name.Namespace;
                        var target = new XElement(ns + "Target",
                            new XAttribute("Name", "ReplaceDesignTimeFacadeDirectories"),
                            new XAttribute("AfterTargets", "GetReferenceAssemblyPaths"));
                        project.Root.Add(target);

                        var itemGroup = new XElement(ns + "ItemGroup");
                        target.Add(itemGroup);

                        itemGroup.Add(new XElement(ns + "_UpdateTargetFrameworkDirectory",
                            new XAttribute("Include", "$(TargetFrameworkDirectory);" + facadesDir),
                            new XAttribute("Exclude", "@(DesignTimeFacadeDirectories)")));

                        itemGroup.Add(new XElement(ns + "DesignTimeFacadeDirectories",
                            new XAttribute("Remove", "@(DesignTimeFacadeDirectories)")));
                        itemGroup.Add(new XElement(ns + "DesignTimeFacadeDirectories",
                            new XAttribute("Include", facadesDir)));

                        var propertyGroup = new XElement(ns + "PropertyGroup");
                        target.Add(propertyGroup);

                        propertyGroup.Add(new XElement(ns + "TargetFrameworkDirectory", "@(_UpdateTargetFrameworkDirectory)"));

                        // currently RAR doesn't detect when netstandard is referenced, directly set _HasReferenceToSystemRuntime
                        // this will be fixed once netstandard is inbox
                        // ISSUE: https://github.com/Microsoft/msbuild/issues/2199
                        propertyGroup.Add(new XElement(ns + "_HasReferenceToSystemRuntime", "True"));
                    }
                });

            testAsset.Restore(Log, relativePath: AppName);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, AppName));
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = isSdk ?
                buildCommand.GetOutputDirectory("net461") :
                buildCommand.GetNonSDKOutputDirectory();

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.exe",
                "TestApp.pdb",
                "TestLibrary.dll",
                "TestLibrary.pdb"
            });
        }


        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_does_not_include_netstandard_when_libary_targets_netstandard1(bool isSdk)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset(GetTemplateName(isSdk))
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (IsAppProject(projectPath))
                    {
                        AddReferenceToLibrary(project, ReferenceScenario.ProjectReference);
                    }

                    if (IsLibraryProject(projectPath))
                    {
                        var ns = project.Root.Name.Namespace;
                        var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                        var targetFrameworkProperty = propertyGroup.Element(ns + "TargetFramework");
                        targetFrameworkProperty.Value = "netstandard1.6";
                    }
                });

            testAsset.Restore(Log, relativePath: AppName);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, AppName));
            buildCommand
                .Execute()
                .Should()
                .Pass();
            
            var outputDirectory = isSdk ?
                buildCommand.GetOutputDirectory("net461") :
                buildCommand.GetNonSDKOutputDirectory();

            outputDirectory.Should().NotHaveFile("netstandard.dll");
        }

    }
}
