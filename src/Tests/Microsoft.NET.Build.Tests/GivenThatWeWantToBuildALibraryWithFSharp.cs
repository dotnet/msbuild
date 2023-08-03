// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildALibraryWithFSharp : SdkTest
    {
        public GivenThatWeWantToBuildALibraryWithFSharp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_the_library_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryFS")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset, "TestLibrary");
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netstandard1.6");

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
                .CopyTestAsset("AppWithLibraryFS")
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
            Action<XDocument> projectChanges = null)
        {
            msbuildArgs = msbuildArgs ?? Array.Empty<string>();

            string targetFramework = "netstandard1.6";

            var testAsset = testAssetsManager
                .CopyTestAsset("AppWithLibraryFS", callingMethod)
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

        [Fact]
        public void The_build_fails_if_nuget_restore_has_not_occurred()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryFS")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset, "TestLibrary");
            buildCommand
                .ExecuteWithoutRestore()
                .Should()
                .Fail();
        }

        [Fact]
        public void Restore_succeeds_even_if_the_project_extension_is_for_a_different_language()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryFS")
                .WithSource();

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var oldProjectFile = Path.Combine(libraryProjectDirectory, "TestLibrary.fsproj");
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
                .CopyTestAsset("AppWithLibraryFS", "ImplicitConfigurationConstantsFS", configuration)
                .WithSource();

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Log, libraryProjectDirectory,
                "netstandard1.6", "DefineConstants");

            getValuesCommand.ShouldCompile = true;
            getValuesCommand.Configuration = configuration;

            getValuesCommand
                .Execute("/p:Configuration=" + configuration)
                .Should()
                .Pass();

            var definedConstants = getValuesCommand.GetValues();

            definedConstants.Should().BeEquivalentTo(new[] { expectedDefine, "TRACE", "NETSTANDARD", "NETSTANDARD1_6", "NETSTANDARD1_0_OR_GREATER", "NETSTANDARD1_1_OR_GREATER", 
                "NETSTANDARD1_2_OR_GREATER", "NETSTANDARD1_3_OR_GREATER", "NETSTANDARD1_4_OR_GREATER", "NETSTANDARD1_5_OR_GREATER", "NETSTANDARD1_6_OR_GREATER" });
        }

        [Theory]
        [InlineData("netstandard1.6", new[] { "NETSTANDARD", "NETSTANDARD1_6", "NETSTANDARD1_0_OR_GREATER", "NETSTANDARD1_1_OR_GREATER", "NETSTANDARD1_2_OR_GREATER", 
            "NETSTANDARD1_3_OR_GREATER", "NETSTANDARD1_4_OR_GREATER", "NETSTANDARD1_5_OR_GREATER", "NETSTANDARD1_6_OR_GREATER" })]
        [InlineData("net45", new[] { "NETFRAMEWORK", "NET45", "NET20_OR_GREATER", "NET30_OR_GREATER", "NET35_OR_GREATER", "NET40_OR_GREATER", "NET45_OR_GREATER" })]
        [InlineData("net461", new[] { "NETFRAMEWORK", "NET461", "NET20_OR_GREATER", "NET30_OR_GREATER", "NET35_OR_GREATER", "NET40_OR_GREATER", "NET45_OR_GREATER", 
            "NET451_OR_GREATER", "NET452_OR_GREATER", "NET46_OR_GREATER", "NET461_OR_GREATER" })]
        [InlineData("netcoreapp2.0", new[] { "NETCOREAPP", "NETCOREAPP2_0", "NETCOREAPP1_0_OR_GREATER", "NETCOREAPP1_1_OR_GREATER", "NETCOREAPP2_0_OR_GREATER" })]
        [InlineData("net5.0", new[] { "NETCOREAPP", "NET", "NET5_0", "NETCOREAPP1_0_OR_GREATER", "NETCOREAPP1_1_OR_GREATER", "NETCOREAPP2_0_OR_GREATER", 
            "NETCOREAPP2_1_OR_GREATER", "NETCOREAPP2_2_OR_GREATER", "NETCOREAPP3_0_OR_GREATER", "NETCOREAPP3_1_OR_GREATER", "NET5_0_OR_GREATER" })]
        public void It_implicitly_defines_compilation_constants_for_the_target_framework(string targetFramework, string[] expectedDefines)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryFS", "ImplicitFrameworkConstantsFS", targetFramework)
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
    }
}
