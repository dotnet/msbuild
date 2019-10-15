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
                .WithSource()
                .Restore(Log, relativePath: "TestLibrary");

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
                "TestLibrary.deps.json"
            });
        }

        [Fact]
        public void It_builds_the_library_twice_in_a_row()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryVB")
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

        internal static IEnumerable<string> ExpandSequence(IEnumerable<string> sequence)
        {
            foreach(var item in sequence)
            {
                foreach(var i in item.Split(','))
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

            var itemValues = ExpandSequence(getValuesCommand.GetValues()).ToList();

            return itemValues;
        }

        [Fact]
        public void The_build_fails_if_nuget_restore_has_not_occurred()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryVB")
                .WithSource();

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);
            buildCommand
                .Execute()
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
        [InlineData("Debug", new[] { "CONFIG=\"Debug\"", "DEBUG=-1", "TRACE=-1" })]
        [InlineData("Release", new[] { "CONFIG=\"Release\"", "RELEASE=-1", "TRACE=-1" })]
        [InlineData("CustomConfiguration",  new[] { "CONFIG=\"CustomConfiguration\"", "CUSTOMCONFIGURATION=-1" })]
        [InlineData("Debug-NetCore",  new[] { "CONFIG=\"Debug-NetCore\"", "DEBUG_NETCORE=-1" })]
        public void It_implicitly_defines_compilation_constants_for_the_configuration(string configuration, string[] expectedDefines)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryVB", "ImplicitConfigurationConstantsVB", configuration)
                .WithSource()
                .Restore(Log, relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var getValuesCommand = new GetValuesCommand(Log, libraryProjectDirectory,
                "netstandard1.5", "FinalDefineConstants");

            getValuesCommand.ShouldCompile = true;
            getValuesCommand.Configuration = configuration;

            getValuesCommand
                .Execute("/p:Configuration=" + configuration)
                .Should()
                .Pass();

            var definedConstants = ExpandSequence(getValuesCommand.GetValues()).ToList();

            definedConstants.Should().BeEquivalentTo(expectedDefines.Concat(new[] { "PLATFORM=\"AnyCPU\"", "NETSTANDARD=-1", "NETSTANDARD1_5=-1" }));
        }

        [Theory]
        [InlineData(".NETStandard,Version=v1.0", new[] { "NETSTANDARD=-1", "NETSTANDARD1_0=-1" }, false)]
        [InlineData("netstandard1.3", new[] { "NETSTANDARD=-1", "NETSTANDARD1_3=-1" }, false)]
        [InlineData("netstandard1.6", new[] { "NETSTANDARD=-1", "NETSTANDARD1_6=-1" }, false)]
        [InlineData("net45", new[] { "NETFRAMEWORK=-1", "NET45=-1" }, true)]
        [InlineData("net461", new[] { "NETFRAMEWORK=-1", "NET461=-1" }, true)]
        [InlineData("netcoreapp1.0", new[] { "NETCOREAPP=-1", "NETCOREAPP1_0=-1" }, false)]
        [InlineData(".NETPortable,Version=v4.5,Profile=Profile78", new string[] { }, false)]
        [InlineData(".NETFramework,Version=v4.0,Profile=Client", new string[] { "NETFRAMEWORK=-1", "NET40=-1" }, false)]
        [InlineData("Xamarin.iOS,Version=v1.0", new string[] { "XAMARINIOS=-1", "XAMARINIOS1_0=-1" }, false)]
        [InlineData("UnknownFramework,Version=v3.14", new string[] { "UNKNOWNFRAMEWORK=-1", "UNKNOWNFRAMEWORK3_14=-1" }, false)]
        public void It_implicitly_defines_compilation_constants_for_the_target_framework(string targetFramework, string[] expectedDefines, bool buildOnlyOnWindows)
        {
            bool shouldCompile = true;

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryVB", "ImplicitFrameworkConstantsVB", targetFramework)
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
                targetFramework, "FinalDefineConstants")
            {
                ShouldCompile = shouldCompile
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var definedConstants = ExpandSequence(getValuesCommand.GetValues()).ToList();

            definedConstants.Should().BeEquivalentTo( new[] { "CONFIG=\"Debug\"", "DEBUG=-1", "TRACE=-1", "PLATFORM=\"AnyCPU\"" }.Concat(expectedDefines).ToArray() );
        }
    }
}
