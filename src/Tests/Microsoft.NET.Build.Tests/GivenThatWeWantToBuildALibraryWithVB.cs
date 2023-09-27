// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildALibraryWithVB : SdkTest
    {
        public GivenThatWeWantToBuildALibraryWithVB(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_the_library_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryVB")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset, "TestLibrary");
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
                .CopyTestAsset("AppWithLibraryVB")
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

        internal static IEnumerable<string> ExpandSequence(IEnumerable<string> sequence)
        {
            foreach (var item in sequence)
            {
                foreach (var i in item.Split(','))
                {
                    yield return i;
                }
            }
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
                .CopyTestAsset("AppWithLibraryVB", callingMethod)
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

            var itemValues = ExpandSequence(getValuesCommand.GetValues()).ToList();

            return itemValues;
        }

        [Fact]
        public void The_build_fails_if_nuget_restore_has_not_occurred()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryVB")
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
                .CopyTestAsset("AppWithLibraryVB")
                .WithSource();

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var oldProjectFile = Path.Combine(libraryProjectDirectory, "TestLibrary.vbproj");
            var newProjectFile = Path.Combine(libraryProjectDirectory, "TestLibrary.different_language_proj");

            File.Move(oldProjectFile, newProjectFile);

            var restoreCommand = new RestoreCommand(Log, libraryProjectDirectory, "TestLibrary.different_language_proj");

            restoreCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Theory]
        [InlineData("Debug", new[] { "CONFIG=\"Debug\"", "DEBUG=-1", "TRACE=-1", "_MyType=\"Empty\"" })]
        [InlineData("Release", new[] { "CONFIG=\"Release\"", "RELEASE=-1", "TRACE=-1", "_MyType=\"Empty\"" })]
        [InlineData("CustomConfiguration", new[] { "CONFIG=\"CustomConfiguration\"", "CUSTOMCONFIGURATION=-1", "_MyType=\"Empty\"" })]
        [InlineData("Debug-NetCore", new[] { "CONFIG=\"Debug-NetCore\"", "DEBUG_NETCORE=-1", "_MyType=\"Empty\"" })]
        public void It_implicitly_defines_compilation_constants_for_the_configuration(string configuration, string[] expectedDefines)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryVB", "ImplicitConfigurationConstantsVB", configuration)
                .WithSource();

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Log, libraryProjectDirectory,
                "netstandard1.5", "FinalDefineConstants")
            {
                ShouldCompile = true,
                Configuration = configuration
            };

            getValuesCommand
                .Execute("/p:Configuration=" + configuration)
                .Should()
                .Pass();

            var definedConstants = ExpandSequence(getValuesCommand.GetValues()).ToList();

            definedConstants.Should().BeEquivalentTo(expectedDefines.Concat(new[] { "PLATFORM=\"AnyCPU\"", "NETSTANDARD=-1", "NETSTANDARD1_5=-1", "NETSTANDARD1_0_OR_GREATER=-1",
                "NETSTANDARD1_1_OR_GREATER=-1", "NETSTANDARD1_2_OR_GREATER=-1", "NETSTANDARD1_3_OR_GREATER=-1", "NETSTANDARD1_4_OR_GREATER=-1", "NETSTANDARD1_5_OR_GREATER=-1" }));
        }

        [Theory]
        [InlineData(".NETStandard,Version=v1.0", new[] { "NETSTANDARD=-1", "NETSTANDARD1_0=-1", "NETSTANDARD1_0_OR_GREATER=-1", "_MyType=\"Empty\"" })]
        [InlineData("netstandard1.3", new[] { "NETSTANDARD=-1", "NETSTANDARD1_3=-1", "NETSTANDARD1_0_OR_GREATER=-1", "NETSTANDARD1_1_OR_GREATER=-1", "NETSTANDARD1_2_OR_GREATER=-1", "NETSTANDARD1_3_OR_GREATER=-1", "_MyType=\"Empty\"" })]
        [InlineData("netstandard1.6", new[] { "NETSTANDARD=-1", "NETSTANDARD1_6=-1", "NETSTANDARD1_0_OR_GREATER=-1", "NETSTANDARD1_1_OR_GREATER=-1", "NETSTANDARD1_2_OR_GREATER=-1", "NETSTANDARD1_3_OR_GREATER=-1",
            "NETSTANDARD1_4_OR_GREATER=-1", "NETSTANDARD1_5_OR_GREATER=-1", "NETSTANDARD1_6_OR_GREATER=-1", "_MyType=\"Empty\"" })]
        [InlineData("net45", new[] { "NETFRAMEWORK=-1", "NET45=-1", "NET20_OR_GREATER=-1", "NET30_OR_GREATER=-1", "NET35_OR_GREATER=-1", "NET40_OR_GREATER=-1", "NET45_OR_GREATER=-1" })]
        [InlineData("net461", new[] { "NETFRAMEWORK=-1", "NET461=-1", "NET20_OR_GREATER=-1", "NET30_OR_GREATER=-1", "NET35_OR_GREATER=-1", "NET40_OR_GREATER=-1", "NET45_OR_GREATER=-1", "NET451_OR_GREATER=-1",
            "NET452_OR_GREATER=-1", "NET46_OR_GREATER=-1", "NET461_OR_GREATER=-1" })]
        [InlineData("netcoreapp1.0", new[] { "NETCOREAPP=-1", "NETCOREAPP1_0=-1", "_MyType=\"Empty\"", "NETCOREAPP1_0_OR_GREATER=-1" })]
        [InlineData("net5.0", new[] { "NET=-1", "NET5_0=-1", "NETCOREAPP=-1", "_MyType=\"Empty\"", "NETCOREAPP1_0_OR_GREATER=-1", "NETCOREAPP1_1_OR_GREATER=-1", "NETCOREAPP2_0_OR_GREATER=-1", "NETCOREAPP2_1_OR_GREATER=-1",
            "NETCOREAPP2_2_OR_GREATER=-1", "NETCOREAPP3_0_OR_GREATER=-1", "NETCOREAPP3_1_OR_GREATER=-1", "NET5_0_OR_GREATER=-1" })]
        [InlineData(".NETPortable,Version=v4.5,Profile=Profile78", new string[] { "_MyType=\"Empty\"" })]
        [InlineData(".NETFramework,Version=v4.0,Profile=Client", new string[] { "NETFRAMEWORK=-1", "NET40=-1", "NET20_OR_GREATER=-1", "NET30_OR_GREATER=-1", "NET35_OR_GREATER=-1", "NET40_OR_GREATER=-1" })]
        [InlineData("Xamarin.iOS,Version=v1.0", new string[] { "XAMARINIOS=-1", "XAMARINIOS1_0=-1", "_MyType=\"Empty\"" })]
        [InlineData("UnknownFramework,Version=v3.14", new string[] { "UNKNOWNFRAMEWORK=-1", "UNKNOWNFRAMEWORK3_14=-1", "_MyType=\"Empty\"" })]
        public void It_implicitly_defines_compilation_constants_for_the_target_framework(string targetFramework, string[] expectedDefines)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryVB", "ImplicitFrameworkConstantsVB", targetFramework, identifier: targetFramework)
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
                targetFramework, "FinalDefineConstants")
            {
                DependsOnTargets = "AddImplicitDefineConstants"
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var definedConstants = ExpandSequence(getValuesCommand.GetValues()).ToList();

            definedConstants.Should().BeEquivalentTo(new[] { "CONFIG=\"Debug\"", "DEBUG=-1", "TRACE=-1", "PLATFORM=\"AnyCPU\"" }.Concat(expectedDefines).ToArray());
        }
    }
}
