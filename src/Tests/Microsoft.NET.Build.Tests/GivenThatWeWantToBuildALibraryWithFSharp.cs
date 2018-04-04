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
                .WithSource()
                .Restore(Log, relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);
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

            string targetFramework = "netstandard1.6";

            var testAsset = testAssetsManager
                .CopyTestAsset("AppWithLibraryFS", callingMethod)
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

        [Fact]
        public void The_build_fails_if_nuget_restore_has_not_occurred()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibraryFS")
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
                .WithSource()
                .Restore(Log, relativePath: "TestLibrary");

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

            definedConstants.Should().BeEquivalentTo(new[] { expectedDefine, "TRACE", "NETSTANDARD", "NETSTANDARD1_6" });
        }

        [Theory]
        [InlineData("netstandard1.6", new[] { "NETSTANDARD", "NETSTANDARD1_6" }, false)]
        [InlineData("net45", new[] { "NETFRAMEWORK", "NET45" }, true)]
        [InlineData("net461", new[] { "NETFRAMEWORK", "NET461" }, true)]
        [InlineData("netcoreapp2.0", new[] { "NETCOREAPP", "NETCOREAPP2_0" }, false)]
        public void It_implicitly_defines_compilation_constants_for_the_target_framework(string targetFramework, string[] expectedDefines, bool buildOnlyOnWindows)
        {
            bool shouldCompile = true;

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
    }
}
