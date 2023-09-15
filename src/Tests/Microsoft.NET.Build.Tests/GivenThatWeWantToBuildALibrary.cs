// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

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
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_builds_the_library_successfully(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework, "TestLibrary");

            var buildCommand = new BuildCommand(testAsset, "TestLibrary");
            buildCommand
                .Execute()
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
                .WithSource();

            var buildCommand = new BuildCommand(testAsset, "TestLibrary");
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
            Action<XDocument> projectChanges = null,
            string identifier = null)
        {
            msbuildArgs = msbuildArgs ?? Array.Empty<string>();

            string targetFramework = "netstandard1.5";

            var testAsset = testAssetsManager
                .CopyTestAsset("AppWithLibrary", callingMethod, identifier: identifier)
                .WithSource();

            if (projectChanges != null)
            {
                testAsset.WithProjectChanges(projectChanges);
            }

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
                });

            return testAsset;
        }

        [Theory]
        [InlineData("cs")]
        [InlineData("vb")]
        public void It_creates_a_documentation_file(string language)
        {
            var testAsset = CreateDocumentationFileLibraryAsset(true, null, language);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(testAsset, "TestLibrary");

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
            var testAsset = CreateDocumentationFileLibraryAsset(setGenerateDocumentationFileProperty ? (bool?)true : null, "TestLibDoc.xml", language, "OverrideDocFileName");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(testAsset, "TestLibrary");

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
            if (language != "vb")
            {
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

            var buildCommand = new BuildCommand(testAsset, "TestLibrary");

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
                .WithSource();

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

            definedConstants.Should().BeEquivalentTo(new[] { expectedDefine, "TRACE", "NETSTANDARD", "NETSTANDARD1_5", "NETSTANDARD1_0_OR_GREATER", "NETSTANDARD1_1_OR_GREATER", "NETSTANDARD1_2_OR_GREATER",
            "NETSTANDARD1_3_OR_GREATER", "NETSTANDARD1_4_OR_GREATER", "NETSTANDARD1_5_OR_GREATER" });
        }

        [Theory]
        [InlineData(".NETStandard,Version=v1.0", new[] { "NETSTANDARD", "NETSTANDARD1_0", "NETSTANDARD1_0_OR_GREATER" })]
        [InlineData("netstandard1.3", new[] { "NETSTANDARD", "NETSTANDARD1_3", "NETSTANDARD1_0_OR_GREATER", "NETSTANDARD1_1_OR_GREATER", "NETSTANDARD1_2_OR_GREATER", "NETSTANDARD1_3_OR_GREATER" })]
        [InlineData("netstandard1.6", new[] { "NETSTANDARD", "NETSTANDARD1_6", "NETSTANDARD1_0_OR_GREATER", "NETSTANDARD1_1_OR_GREATER", "NETSTANDARD1_2_OR_GREATER",
            "NETSTANDARD1_3_OR_GREATER", "NETSTANDARD1_4_OR_GREATER", "NETSTANDARD1_5_OR_GREATER", "NETSTANDARD1_6_OR_GREATER" })]
        [InlineData("net45", new[] { "NETFRAMEWORK", "NET45", "NET20_OR_GREATER", "NET30_OR_GREATER", "NET35_OR_GREATER", "NET40_OR_GREATER", "NET45_OR_GREATER" })]
        [InlineData("net461", new[] { "NETFRAMEWORK", "NET461", "NET20_OR_GREATER", "NET30_OR_GREATER", "NET35_OR_GREATER", "NET40_OR_GREATER", "NET45_OR_GREATER",
            "NET451_OR_GREATER", "NET452_OR_GREATER", "NET46_OR_GREATER", "NET461_OR_GREATER" })]
        [InlineData("net48", new[] { "NETFRAMEWORK", "NET48", "NET20_OR_GREATER", "NET30_OR_GREATER", "NET35_OR_GREATER", "NET40_OR_GREATER", "NET45_OR_GREATER",
            "NET451_OR_GREATER", "NET452_OR_GREATER", "NET46_OR_GREATER", "NET461_OR_GREATER", "NET462_OR_GREATER", "NET47_OR_GREATER", "NET471_OR_GREATER", "NET472_OR_GREATER", "NET48_OR_GREATER" })]
        [InlineData("net481", new[] { "NETFRAMEWORK", "NET481", "NET20_OR_GREATER", "NET30_OR_GREATER", "NET35_OR_GREATER", "NET40_OR_GREATER", "NET45_OR_GREATER",
            "NET451_OR_GREATER", "NET452_OR_GREATER", "NET46_OR_GREATER", "NET461_OR_GREATER", "NET462_OR_GREATER", "NET47_OR_GREATER", "NET471_OR_GREATER", "NET472_OR_GREATER", "NET48_OR_GREATER", "NET481_OR_GREATER" })]
        [InlineData("netcoreapp1.0", new[] { "NETCOREAPP", "NETCOREAPP1_0", "NETCOREAPP1_0_OR_GREATER" })]
        [InlineData("netcoreapp3.0", new[] { "NETCOREAPP", "NETCOREAPP3_0", "NETCOREAPP1_0_OR_GREATER", "NETCOREAPP1_1_OR_GREATER", "NETCOREAPP2_0_OR_GREATER",
            "NETCOREAPP2_1_OR_GREATER", "NETCOREAPP2_2_OR_GREATER", "NETCOREAPP3_0_OR_GREATER" })]
        [InlineData("net5.0", new[] { "NETCOREAPP", "NETCOREAPP1_0_OR_GREATER", "NETCOREAPP1_1_OR_GREATER", "NETCOREAPP2_0_OR_GREATER", "NETCOREAPP2_1_OR_GREATER",
            "NETCOREAPP2_2_OR_GREATER", "NETCOREAPP3_0_OR_GREATER", "NETCOREAPP3_1_OR_GREATER", "NET", "NET5_0", "NET5_0_OR_GREATER" })]
        [InlineData(".NETPortable,Version=v4.5,Profile=Profile78", new string[] { })]
        [InlineData(".NETFramework,Version=v4.0,Profile=Client", new string[] { "NETFRAMEWORK", "NET40", "NET20_OR_GREATER", "NET30_OR_GREATER", "NET35_OR_GREATER", "NET40_OR_GREATER" })]
        [InlineData("Xamarin.iOS,Version=v1.0", new string[] { "XAMARINIOS", "XAMARINIOS1_0" })]
        [InlineData("UnknownFramework,Version=v3.14", new string[] { "UNKNOWNFRAMEWORK", "UNKNOWNFRAMEWORK3_14" })]
        public void It_implicitly_defines_compilation_constants_for_the_target_framework(string targetFramework, string[] expectedDefines)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", "ImplicitFrameworkConstants", targetFramework, identifier: expectedDefines.GetHashCode().ToString())
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
                        targetFrameworkProperties.Single().SetValue(targetFramework);
                    }
                });

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Log, libraryProjectDirectory,
                targetFramework, "DefineConstants")
            {
                DependsOnTargets = "AddImplicitDefineConstants"
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var definedConstants = getValuesCommand.GetValues();

            definedConstants.Should().BeEquivalentTo(new[] { "DEBUG", "TRACE" }.Concat(expectedDefines).ToArray());
        }

        [Theory]
        [InlineData(new string[] { }, "windows", "10.0.18362.0", new[] { "WINDOWS", "WINDOWS10_0_18362_0", "WINDOWS7_0_OR_GREATER", "WINDOWS8_0_OR_GREATER", "WINDOWS10_0_17763_0_OR_GREATER", "WINDOWS10_0_18362_0_OR_GREATER" })]
        [InlineData(new[] { "1.0", "1.1" }, "ios", "1.1", new[] { "IOS", "IOS1_1", "IOS1_0_OR_GREATER", "IOS1_1_OR_GREATER" })]
        [InlineData(new[] { "11.11", "12.12", "13.13" }, "android", "12.12", new[] { "ANDROID", "ANDROID12_12", "ANDROID11_11_OR_GREATER", "ANDROID12_12_OR_GREATER" })]
        public void It_implicitly_defines_compilation_constants_for_the_target_platform(string[] sdkSupportedTargetPlatformVersion, string targetPlatformIdentifier, string targetPlatformVersion, string[] expectedDefines)
        {
            if (targetPlatformIdentifier.Equals("windows", StringComparison.OrdinalIgnoreCase))
            {
                var sdkVersion = SemanticVersion.Parse(TestContext.Current.ToolsetUnderTest.SdkVersion);
                if (new SemanticVersion(sdkVersion.Major, sdkVersion.Minor, sdkVersion.Patch) < new SemanticVersion(7, 0, 200))
                {
                    //  Fixed in 7.0.200: https://github.com/dotnet/sdk/pull/29009
                    return;
                }
            }

            var targetFramework = "net5.0";
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", "ImplicitFrameworkConstants", targetFramework, identifier: expectedDefines.GetHashCode().ToString())
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    //  Manually set target plaform properties
                    var ns = project.Root.Name.Namespace;
                    var propGroup = new XElement(ns + "PropertyGroup");
                    project.Root.Add(propGroup);

                    var platformIdentifier = new XElement(ns + "TargetPlatformIdentifier", targetPlatformIdentifier);
                    propGroup.Add(platformIdentifier);
                    var platformVersion = new XElement(ns + "TargetPlatformVersion", targetPlatformVersion);
                    propGroup.Add(platformVersion);
                    var platformSupported = new XElement(ns + "TargetPlatformSupported", true);
                    propGroup.Add(platformSupported);
                    var disableUnnecessaryImplicitFrameworkReferencesForThisTest = new XElement(ns + "DisableImplicitFrameworkReferences", "true");
                    propGroup.Add(disableUnnecessaryImplicitFrameworkReferencesForThisTest);

                    //  Disable workloads for this test so we can test iOS and Android TargetFrameworks without having those workloads installed
                    propGroup.Add(new XElement(ns + "MSBuildEnableWorkloadResolver", false));

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);
                    foreach (var targetPlatform in sdkSupportedTargetPlatformVersion)
                    {
                        itemGroup.Add(new XElement(ns + "SdkSupportedTargetPlatformVersion", new XAttribute("Include", targetPlatform)));
                    }
                });

            AssertDefinedConstantsOutput(testAsset, targetFramework,
                new[] { "NETCOREAPP", "NETCOREAPP1_0_OR_GREATER", "NETCOREAPP1_1_OR_GREATER", "NETCOREAPP2_0_OR_GREATER", "NETCOREAPP2_1_OR_GREATER", "NETCOREAPP2_2_OR_GREATER", "NETCOREAPP3_0_OR_GREATER", "NETCOREAPP3_1_OR_GREATER", "NET", "NET5_0", "NET5_0_OR_GREATER" }
                .Concat(expectedDefines).ToArray());
        }

        [WindowsOnlyFact]
        public void It_does_not_generate_or_greater_symbols_on_disabled_implicit_framework_defines()
        {
            var targetFramework = "net5.0-windows10.0.19041.0";
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", "ImplicitFrameworkConstants", targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propGroup = new XElement(ns + "PropertyGroup");
                    project.Root.Add(propGroup);

                    var disableImplicitFrameworkDefines = new XElement(ns + "DisableImplicitFrameworkDefines", "true");
                    propGroup.Add(disableImplicitFrameworkDefines);
                });

            AssertDefinedConstantsOutput(testAsset, targetFramework, Array.Empty<string>());
        }

        private void AssertDefinedConstantsOutput(TestAsset testAsset, string targetFramework, string[] expectedDefines)
        {
            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Log, libraryProjectDirectory,
                targetFramework, "DefineConstants")
            {
                ShouldCompile = false,
                TargetName = "CoreCompile" // Overwrite core compile with our target to get DefineConstants
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var definedConstants = getValuesCommand.GetValues();

            definedConstants.Should().BeEquivalentTo(new[] { "DEBUG", "TRACE" }.Concat(expectedDefines).ToArray());
        }

        [WindowsOnlyTheory]
        [InlineData("netcoreapp3.1", new[] { "NETCOREAPP", "NETCOREAPP3_1", "NETCOREAPP3_1_OR_GREATER" })]
        [InlineData("net5.0", new[] { "NETCOREAPP", "NET", "NETCOREAPP3_1_OR_GREATER", "NET5_0_OR_GREATER", "NET5_0", "WINDOWS", "WINDOWS7_0", "WINDOWS7_0_OR_GREATER" }, "windows", "7.0")]
        public void It_can_use_implicitly_defined_compilation_constants(string targetFramework, string[] expectedOutput, string targetPlatformIdentifier = null, string targetPlatformVersion = null)
        {
            var testProj = new TestProject()
            {
                Name = "CompilationConstants",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };
            if (targetPlatformIdentifier != null)
            {
                testProj.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatformIdentifier;
                testProj.AdditionalProperties["TargetPlatformVersion"] = targetPlatformVersion;
            }

            testProj.SourceFiles[$"{testProj.Name}.cs"] = @"
using System;
class Program
{
    static void Main(string[] args)
    {
        #if NETCOREAPP
            Console.WriteLine(""NETCOREAPP"");
        #endif
        #if NETCOREAPP2_1
            Console.WriteLine(""NETCOREAPP2_1"");
        #endif
        #if NETCOREAPP3_1
            Console.WriteLine(""NETCOREAPP3_1"");
        #endif
        #if NETCOREAPP3_1_OR_GREATER
            Console.WriteLine(""NETCOREAPP3_1_OR_GREATER"");
        #endif
        #if NET
            Console.WriteLine(""NET"");
        #endif
        #if NET5_0
            Console.WriteLine(""NET5_0"");
        #endif
        #if NET5_0_OR_GREATER
            Console.WriteLine(""NET5_0_OR_GREATER"");
        #endif
        #if WINDOWS
            Console.WriteLine(""WINDOWS"");
        #endif
        #if WINDOWS7_0
            Console.WriteLine(""WINDOWS7_0"");
        #endif
        #if WINDOWS7_0_OR_GREATER
            Console.WriteLine(""WINDOWS7_0_OR_GREATER"");
        #endif
        #if IOS
            Console.WriteLine(""IOS"");
        #endif
    }
}";
            var testAsset = _testAssetsManager.CreateTestProject(testProj, targetFramework);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.Path, testProj.Name));
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var runCommand = new RunExeCommand(Log, Path.Combine(buildCommand.GetOutputDirectory(targetFramework).FullName, $"{testProj.Name}.exe"));
            var stdOut = runCommand.Execute().StdOut.Split(Environment.NewLine.ToCharArray()).Where(line => !string.IsNullOrWhiteSpace(line));
            stdOut.Should().BeEquivalentTo(expectedOutput);
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
            string targetFramework = $"{ToolsetInfo.CurrentTargetFramework};net462";
            TestInvalidTargetFramework("InvalidTargetFramework", targetFramework, useSolution,
                $"The TargetFramework value '{targetFramework}' is not valid. To multi-target, use the 'TargetFrameworks' property instead");
        }

        [WindowsOnlyRequiresMSBuildVersionTheory("16.7.0-preview-20310-07")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "", false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "UseWPF", true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "UseWindowsForms", true)]
        [InlineData("netcoreapp3.1", "", true)]
        public void It_defines_target_platform_defaults_correctly(string targetFramework, string propertyName, bool defaultsDefined)
        {
            TestProject testProject = new TestProject()
            {
                Name = "TargetPlatformDefaults",
                TargetFrameworks = targetFramework
            };

            if (!propertyName.Equals(string.Empty))
            {
                testProject.AdditionalProperties[propertyName] = "true";
            }
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), targetFramework, "TargetPlatformIdentifier");
            getValuesCommand
                .Execute()
                .Should()
                .Pass();
            var values = getValuesCommand.GetValues();
            if (defaultsDefined)
            {
                values.Count().Should().Be(1);
                values.FirstOrDefault().Should().Be("Windows");
            }
            else
            {
                values.Count().Should().Be(0);
            }
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        [InlineData("netcoreapp3.1")]
        public void It_defines_windows_version_default_correctly(string targetFramework)
        {
            TestProject testProject = new TestProject()
            {
                Name = "WindowsVersionDefault",
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = "windows";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), targetFramework, "TargetPlatformVersion");
            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            getValuesCommand.GetValues().Should().BeEquivalentTo(new[] { "7.0" });
        }

        private void TestInvalidTargetFramework(string testName, string targetFramework, bool useSolution, string expectedOutput)
        {
            var testProject = new TestProject()
            {
                Name = testName,
                TargetFrameworks = targetFramework,
            };

            string identifier = ((useSolution ? "_Solution" : "") + targetFramework + expectedOutput).GetHashCode().ToString();
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

            RestoreCommand restoreCommand;
            BuildCommand buildCommand;

            if (useSolution)
            {
                new DotnetNewCommand(Log)
                    .WithVirtualHive()
                    .WithWorkingDirectory(testAsset.TestRoot)
                    .Execute("sln")
                    .Should()
                    .Pass();

                var relativePathToProject = Path.Combine(testProject.Name, testProject.Name + ".csproj");
                new DotnetCommand(Log)
                    .WithWorkingDirectory(testAsset.TestRoot)
                    .Execute($"sln", "add", relativePathToProject)
                    .Should()
                    .Pass();

                var relativePathToSln = Path.GetFileName(testAsset.Path) + ".sln";

                restoreCommand = testAsset.GetRestoreCommand(Log, relativePathToSln);
                buildCommand = new BuildCommand(testAsset, relativePathToSln);
            }
            else
            {
                restoreCommand = testAsset.GetRestoreCommand(Log, testProject.Name);
                buildCommand = new BuildCommand(testAsset);
            }

            //  Set RestoreContinueOnError=ErrorAndContinue to force failure on error
            //  See https://github.com/NuGet/Home/issues/5309
            var restore = restoreCommand.Execute("/p:RestoreContinueOnError=ErrorAndContinue");
            // Intentionally not checking the error message on restore here as we can't put ourselves in front of
            // restore and customize the message for invalid target frameworks as that would break restoring packages
            // like MSBuild.Sdk.Extras that add support for extra TFMs.
            restore.Should().Fail();

            buildCommand
                .ExecuteWithoutRestore()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(expectedOutput)
                .And.NotHaveStdOutContaining(">="); // old error about comparing empty string to version when TargetFramework was blank;
        }

        [Theory]
        [InlineData("netcoreapp9.1")]
        [InlineData("netstandard2.2")]
        public void It_fails_to_build_if_targeting_a_higher_framework_than_is_supported(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "TargetFrameworkVersionCap",
                TargetFrameworks = targetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, targetFramework);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: testProject.Name);

            restoreCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("The current .NET SDK does not support targeting");

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("The current .NET SDK does not support targeting");
        }

        [Fact]
        public void It_passes_ridless_target_to_compiler()
        {
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework);

            var testProject = new TestProject()
            {
                Name = "CompileDoesntUseRid",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                RuntimeIdentifier = runtimeIdentifier,
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

            var buildCommand = new BuildCommand(testAsset);

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
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
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
                // references from packages go through a different code path to be marked externally resolved.
                PackageReferences = { new TestPackageReference("NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()) }
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
                });

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

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net5.0", false, false, false, null)]   // Pre .NET 6.0 predefinedCulturesOnly is not supported.
        [InlineData("net5.0", true, false, false, null)]    // Pre .NET 6.0 predefinedCulturesOnly is not supported.
        [InlineData("net5.0", false, true, true, "True")]   // Pre .NET 6.0 predefinedCulturesOnly can end up in the runtime config file but with no effect at runtime.
        [InlineData("net5.0", true, true, true, "True")]    // Pre .NET 6.0 predefinedCulturesOnly can end up in the runtime config file but with no effect at runtime.
        [InlineData(ToolsetInfo.CurrentTargetFramework, false, false, false, null)]   // predefinedCulturesOnly will not be included in the runtime config file if invariant is not defined.
        [InlineData(ToolsetInfo.CurrentTargetFramework, false, false, true, "False")] // predefinedCulturesOnly explicitly defined as false.
        [InlineData(ToolsetInfo.CurrentTargetFramework, false, true, true, "True")]   // predefinedCulturesOnly explicitly defined as true.
        [InlineData(ToolsetInfo.CurrentTargetFramework, true, false, false, "True")]  // predefinedCulturesOnly default value is true when Invariant is true.
        [InlineData(ToolsetInfo.CurrentTargetFramework, true, false, true, "False")]  // predefinedCulturesOnly explicitly defined as false.
        [InlineData(ToolsetInfo.CurrentTargetFramework, true, true, true, "True")]    // predefinedCulturesOnly explicitly defined as true.
        public void It_can_implicitly_define_predefined_Cultures_only(string targetFramework, bool invariantValue, bool predefinedCulturesOnlyValue, bool definePredefinedCulturesOnly, string expectedPredefinedValue)
        {
            var testProj = new TestProject()
            {
                Name = "CheckPredefineCulturesOnly",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };

            testProj.AdditionalProperties["InvariantGlobalization"] = invariantValue ? "true" : "false";

            if (definePredefinedCulturesOnly)
            {
                testProj.AdditionalProperties["PredefinedCulturesOnly"] = predefinedCulturesOnlyValue ? "true" : "false";
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProj, identifier: $"{targetFramework}{invariantValue}{predefinedCulturesOnlyValue}{definePredefinedCulturesOnly}");
            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            string runtimeConfigName = $"{testProj.Name}.runtimeconfig.json";
            var outputDirectory = buildCommand.GetOutputDirectory(testProj.TargetFrameworks);
            outputDirectory.Should().HaveFile(runtimeConfigName);

            string runtimeConfigFile = Path.Combine(outputDirectory.FullName, runtimeConfigName);
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);
            JToken predefinedCulturesOnly = runtimeConfig["runtimeOptions"]["configProperties"]["System.Globalization.PredefinedCulturesOnly"];

            if (expectedPredefinedValue is null)
            {
                predefinedCulturesOnly.Should().BeNull();
            }
            else
            {
                predefinedCulturesOnly.Value<string>().Should().Be(expectedPredefinedValue);
            }
        }

        [Theory]
        [InlineData("True")]
        [InlineData("False")]
        [InlineData(null)]
        public void It_can_evaluate_metrics_support(string value)
        {
            var testProj = new TestProject()
            {
                Name = "CheckMetricsSupport",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            if (value is not null)
            {
                testProj.AdditionalProperties["MetricsSupport"] = value;
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProj, identifier: value);
            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            string runtimeConfigName = $"{testProj.Name}.runtimeconfig.json";
            var outputDirectory = buildCommand.GetOutputDirectory(testProj.TargetFrameworks);
            outputDirectory.Should().HaveFile(runtimeConfigName);

            string runtimeConfigFile = Path.Combine(outputDirectory.FullName, runtimeConfigName);
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);
            JToken metricsSupport = runtimeConfig["runtimeOptions"]["configProperties"]["System.Diagnostics.Metrics.Meter.IsSupported"];

            if (value is null)
            {
                metricsSupport.Should().BeNull();
            }
            else
            {
                metricsSupport.Value<string>().Should().Be(value);
            }
        }

        [Theory]
        [InlineData("netcoreapp2.2", null, false, null, false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, null, true, null, true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "LatestMajor", true, null, true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, null, true, false, false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "LatestMajor", true, false, false)]
        public void It_can_build_with_dynamic_loading_enabled(string targetFramework, string rollForwardValue, bool shouldSetRollForward, bool? copyLocal, bool shouldCopyLocal)
        {
            var testProject = new TestProject()
            {
                Name = "EnableDynamicLoading",
                TargetFrameworks = targetFramework,
            };

            testProject.AdditionalProperties["EnableDynamicLoading"] = "true";
            if (!string.IsNullOrEmpty(rollForwardValue))
            {
                testProject.AdditionalProperties["RollForward"] = rollForwardValue;
            }

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            if (copyLocal.HasValue)
            {
                testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = copyLocal.ToString().ToLower();
            }

            var identifier = targetFramework + shouldSetRollForward + shouldCopyLocal + (rollForwardValue == null ? "Null" : rollForwardValue);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: identifier);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string runtimeConfigName = $"{testProject.Name}.runtimeconfig.json";
            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().HaveFiles(new[] {
                runtimeConfigName,
                $"{testProject.Name}.runtimeconfig.json"
            });

            if (shouldCopyLocal)
            {
                outputDirectory.Should().HaveFile("Newtonsoft.Json.dll");
            }
            else
            {
                outputDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");
            }

            string runtimeConfigFile = Path.Combine(outputDirectory.FullName, runtimeConfigName);
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);
            JToken rollForward = runtimeConfig["runtimeOptions"]["rollForward"];
            if (shouldSetRollForward)
            {
                rollForward.Value<string>().Should().Be(string.IsNullOrEmpty(rollForwardValue) ? "LatestMinor" : rollForwardValue);
            }
            else
            {
                rollForward.Should().BeNull();
            }
        }

        [Theory]
        [InlineData("netcoreapp3.1")]
        [InlineData("netcoreapp5.0")]
        public void It_makes_RootNamespace_safe_when_project_name_has_spaces(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "Project Name With Spaces",
                TargetFrameworks = targetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            // Overwrite the default file. CreateTestProject uses the defined project name for the namespace.
            // We need a buildable project to extract the property to verify it
            // since this issue only surfaces in VS when adding a new class through an item template.
            File.WriteAllText(Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.cs"), @"
using System;
using System.Collections.Generic;

namespace ProjectNameWithSpaces
{
    public class ProjectNameWithSpacesClass
    {
        public static string Name { get { return ""Project Name With Spaces""; } }
        public static List<string> List { get { return null; } }
    }
}");
            string projectFolder = Path.Combine(testAsset.Path, testProject.Name);

            var buildCommand = new BuildCommand(testAsset, $"{testProject.Name}");
            buildCommand
                .Execute()
                .Should()
                .Pass();

            string GetPropertyValue(string propertyName)
            {
                var getValuesCommand = new GetValuesCommand(Log, projectFolder,
                    testProject.TargetFrameworks, propertyName, GetValuesCommand.ValueType.Property)
                {
                    Configuration = "Debug"
                };

                getValuesCommand
                    .Execute()
                    .Should()
                    .Pass();

                var values = getValuesCommand.GetValues();
                values.Count.Should().Be(1);
                return values[0];
            }

            GetPropertyValue("RootNamespace").Should().Be("Project_Name_With_Spaces");
        }

        [WindowsOnlyFact]
        public void It_errors_on_windows_sdk_assembly_version_conflicts()
        {
            var testProjectA = new TestProject()
            {
                Name = "ProjA",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041"
            };
            //  Use a previous version of the Microsoft.Windows.SDK.NET.Ref package, to
            //  simulate the scenario where a project is compiling against a library from NuGet
            //  which was built with a more recent SDK version.
            testProjectA.AdditionalProperties["WindowsSdkPackageVersion"] = "10.0.19041.6-preview";
            testProjectA.SourceFiles.Add("ProjA.cs", @"namespace ProjA
{
    public class ProjAClass
    {
        public static string str { get { return Windows.Media.Devices.ColorTemperaturePreset.Auto.ToString(); } }
        public string ProjBstr { get { return ProjB.ProjBClass.str; } }
    }
}");
            var testProjectB = new TestProject()
            {
                Name = "ProjB",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041",
            };
            testProjectB.SourceFiles.Add("ProjB.cs", @"namespace ProjB
{
    public class ProjBClass
    {
        public static string str { get { return Windows.Media.Devices.ColorTemperaturePreset.Auto.ToString(); } }
    }
}");
            testProjectA.ReferencedProjects.Add(testProjectB);

            var testAsset = _testAssetsManager.CreateTestProject(testProjectA);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1148");
        }

        [Fact]
        public void It_Has_Unescaped_PackageConflictPreferredPackages_Values()
        {
            string targetFramework = ToolsetInfo.CurrentTargetFramework;

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, "TestLibrary"), targetFramework, "PackageConflictPreferredPackages");
            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            List<string> preferredPackages = getValuesCommand.GetValues();
            preferredPackages.Should().NotBeEmpty();
            preferredPackages.Count.Should().BeGreaterThan(1);

            preferredPackages.Should().NotContain(packageName => packageName.Contains(';'),
                because: "No package name should have a semicolon in it--PackageConflictPreferredPackages should be a semicolon delimited list of package names");
        }
    }
}
