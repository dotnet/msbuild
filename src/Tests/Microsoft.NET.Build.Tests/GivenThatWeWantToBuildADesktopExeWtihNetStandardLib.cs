// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopExeWithNetStandardLib : SdkTest
    {
        private const string AppName = "TestApp";
        private const string LibraryName = "TestLibrary";

        private const string TemplateName = "DesktopAppWithLibrary";
        private const string TemplateNamePackagesConfig = "DesktopAppWithLibrary-PackagesConfig";
        private const string TemplateNameNonSdk = "DesktopAppWithLibrary-NonSDK";

        public GivenThatWeWantToBuildADesktopExeWithNetStandardLib(ITestOutputHelper log) : base(log)
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
                .CopyTestAsset(GetTemplateName(isSdk), identifier: (isSdk ? "sdk_" : "") + scenario.ToString())
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

                var libBuildCommand = new BuildCommand(testAsset, LibraryName);
                libBuildCommand
                    .Execute()
                    .Should()
                    .Pass();
            }

            var buildCommand = new BuildCommand(testAsset, AppName);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = isSdk ? 
                buildCommand.GetOutputDirectory("net462") :
                buildCommand.GetNonSDKOutputDirectory();

            outputDirectory.Should().HaveFiles(new[] {
                "netstandard.dll",
                $"{AppName}.exe.config"
            });
        }

        [FullMSBuildOnlyFact]
        public void It_includes_netstandard_in_design_time_builds()
        {
            //  Verify that a P2P reference to a .NET Standard 2.0 project is correctly detected
            //  even if doing a design-time build where there is no output on disk to examine
            //  See https://github.com/dotnet/sdk/issues/1403

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopAppWithLibrary-NonSDK")
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (IsAppProject(projectPath))
                    {
                        AddReferenceToLibrary(project, ReferenceScenario.ProjectReference);
                    }
                });

            var getCommandLineCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, AppName), "", "CscCommandLineArgs", GetValuesCommand.ValueType.Item);

            getCommandLineCommand
                .Execute("/p:SkipCompilerExecution=true /p:ProvideCommandLineArgs=true /p:BuildingInsideVisualStudio=true /p:DesignTimeBuild=true".Split())
                .Should()
                .Pass();


            //  Verify that neither of the projects were actually built
            string valuesFilename = "CscCommandLineArgsValues.txt";

            var outputDirectory = getCommandLineCommand.GetNonSDKOutputDirectory();
            outputDirectory.Should().OnlyHaveFiles(new[] { valuesFilename });

            var testLibraryDirectory = new DirectoryInfo(Path.Combine(testAsset.TestRoot, "TestLibrary"));
            testLibraryDirectory.Should().NotHaveSubDirectories("bin");

            //  Verify that netstandard.dll was passed to compiler
            var references = getCommandLineCommand.GetValues()
                .Where(arg => arg.StartsWith("/reference:"))
                .Select(arg => arg.Substring("/reference:".Length))
                .Select(r => r.Trim('"'))
                .ToList();

            references.Select(r => Path.GetFileName(r))
                .Should().Contain("netstandard.dll");
        }

        [WindowsOnlyTheory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public void It_resolves_conflicts(bool isSdk, bool usePackagesConfig)
        {
            var successMessage = "No conflicts found for support libs";

            var testAsset = _testAssetsManager
                .CopyTestAsset(GetTemplateName(isSdk, usePackagesConfig),
                               identifier: isSdk.ToString() + "_" + usePackagesConfig.ToString())
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
                            new XAttribute("Path", TestContext.Current.ToolsetUnderTest.GetMicrosoftNETBuildExtensionsPath()),
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

            if (usePackagesConfig)
            {
                new NuGetExeRestoreCommand(Log, testAsset.TestRoot, AppName)
                    .Execute()
                    .Should()
                    .Pass();
            }
            else
            {
            }

            // build should succeed without duplicates
            var buildCommand = new BuildCommand(testAsset, AppName);
            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("duplicate")
                .And
                .HaveStdOutContainingIgnoreCase(successMessage);

            var outputDirectory = isSdk ?
                buildCommand.GetOutputDirectory("net462") :
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
                .CopyTestAsset(GetTemplateName(isSdk), identifier: isSdk.ToString())
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (IsAppProject(projectPath))
                    {
                        AddReferenceToLibrary(project, ReferenceScenario.ProjectReference);

                        var ns = project.Root.Name.Namespace;
                        if (isSdk)
                        {
                            project.Root.Element(ns + "PropertyGroup")
                                        .Element(ns + "TargetFramework")
                                        .Value = "net471";
                        }
                        else
                        {
                            project.Root.Element(ns + "PropertyGroup")
                                        .Element(ns + "TargetFrameworkVersion")
                                        .Value = "v4.7.1";
                        }
                    }
                });

            var buildCommand = new BuildCommand(testAsset, AppName);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = isSdk ?
                buildCommand.GetOutputDirectory("net471") :
                buildCommand.GetNonSDKOutputDirectory();

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.exe",
                "TestApp.pdb",
                "TestLibrary.dll",
                "TestLibrary.pdb",

                //  These DLLs are still included in the output folder when targeting .NET 4.7.1
                //  With .NET 4.7.2, they are not included anymore
                "System.Data.Common.dll",
                "System.Diagnostics.StackTrace.dll",
                "System.Diagnostics.Tracing.dll",
                "System.Globalization.Extensions.dll",
                "System.IO.Compression.dll",
                "System.Net.Http.dll",
                "System.Net.Sockets.dll",
                "System.Runtime.Serialization.Primitives.dll",
                "System.Security.Cryptography.Algorithms.dll",
                "System.Security.SecureString.dll",
                "System.Threading.Overlapped.dll",
                "System.Xml.XPath.XDocument.dll",

                //  Binding redirects are generated for .NET 4.7.1
                "TestApp.exe.config",
            });
        }


        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_does_not_include_netstandard_when_library_targets_netstandard14(bool isSdk)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset(GetTemplateName(isSdk), identifier: isSdk.ToString())
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
                        targetFrameworkProperty.Value = "netstandard1.4";
                    }
                });

            var buildCommand = new BuildCommand(testAsset, AppName);
            buildCommand
                .Execute()
                .Should()
                .Pass();
            
            var outputDirectory = isSdk ?
                buildCommand.GetOutputDirectory("net462") :
                buildCommand.GetNonSDKOutputDirectory();

            outputDirectory.Should().NotHaveFile("netstandard.dll");
        }


        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_includes_netstandard_when_library_targets_netstandard15(bool isSdk)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset(GetTemplateName(isSdk), identifier: isSdk.ToString())
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
                        targetFrameworkProperty.Value = "netstandard1.5";
                    }
                });

            var buildCommand = new BuildCommand(testAsset, AppName);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = isSdk ?
                buildCommand.GetOutputDirectory("net462") :
                buildCommand.GetNonSDKOutputDirectory();

            // net462 didn't originally support netstandard2.0, (nor netstandard1.5 or netstandard1.6)
            // Since support was added after we need to ensure we apply the shims for netstandard1.5 projects as well.

            outputDirectory.Should().HaveFiles(new[] {
                "netstandard.dll",
                $"{AppName}.exe.config"
            });
        }

    }
}
