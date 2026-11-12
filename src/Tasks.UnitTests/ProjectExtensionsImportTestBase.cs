// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public abstract class ProjectExtensionsImportTestBase : IDisposable
    {
        private readonly TestEnvironment _environment;
        private readonly ITestOutputHelper _output;
        private readonly ProjectCollection _projectCollection;
        private readonly TransientTestFolder _testRoot;

        protected readonly string _projectRelativePath = Path.Combine("src", "foo", "foo.csproj");

        protected ProjectExtensionsImportTestBase(ITestOutputHelper output)
        {
            _output = output;
            _environment = TestEnvironment.Create(output);
            _testRoot = _environment.CreateFolder();
            _projectCollection = _environment.CreateProjectCollection().Collection;
            _projectCollection.RegisterLogger(new MockLogger(output));
        }

        protected virtual string BasicProjectImportContents => $"""
            <Project>
                <PropertyGroup>
                    <{PropertyNameToSignalImportSucceeded}>true</{PropertyNameToSignalImportSucceeded}>
                </PropertyGroup>
            </Project>
            """;

        protected abstract string CustomImportProjectRelativePath { get; }
        protected abstract string ImportProjectRelativePath { get; }
        protected abstract string PropertyNameToEnableImport { get; }

        /// <summary>
        /// The name of the property to use in a project that is imported.  This base class will generate a project containing the declaration of the property.
        /// </summary>
        protected abstract string PropertyNameToSignalImportSucceeded { get; }

        public void Dispose()
        {
            _environment.Dispose();
        }

        /// <summary>
        /// Ensures that when the MSBuildProjectExtensionsPath does not exist that nothing is imported.
        /// </summary>
        [Fact]
        public void DoesNotImportProjectIfNotExist()
        {
            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = CreateProject("""
                <Project DefaultTargets="Build" ToolsVersion="msbuilddefaulttoolsversion">
                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
                </Project>
                """);

            string projectExtensionsPath = project.GetPropertyValue("MSBuildProjectExtensionsPath");
            _output.WriteLine($"MSBuildProjectExtensionsPath evaluated to '{projectExtensionsPath}'.");

            projectExtensionsPath.ShouldNotBeNullOrWhiteSpace();
            Directory.Exists(projectExtensionsPath).ShouldBeFalse(
                $"Expected MSBuildProjectExtensionsPath not to exist: {projectExtensionsPath}");
            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe("true");
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBeEmpty();
        }

        [Fact]
        public void DoesNotImportProjectIfRestoring()
        {
            CreateFile(ImportProjectRelativePath, BasicProjectImportContents);

            Project project = CreateProject($"""
                <Project DefaultTargets="Build" ToolsVersion="msbuilddefaulttoolsversion">
                    <PropertyGroup>
                        <{MSBuildConstants.MSBuildIsRestoring}>true</{MSBuildConstants.MSBuildIsRestoring}>
                    </PropertyGroup>

                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
                </Project>
                """);

            string projectExtensionsPath = project.GetPropertyValue("MSBuildProjectExtensionsPath");

            projectExtensionsPath.ShouldNotBeNullOrWhiteSpace();
            Directory.Exists(projectExtensionsPath).ShouldBeTrue(
                $"Expected MSBuildProjectExtensionsPath to exist: {projectExtensionsPath}");
            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe(bool.FalseString, StringCompareShould.IgnoreCase);
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBeEmpty();
        }

        [Fact]
        public void ImportsProjectIfRestoringAndExplicitlySet()
        {
            CreateFile(ImportProjectRelativePath, BasicProjectImportContents);

            Project project = CreateProject($"""
                <Project DefaultTargets="Build" ToolsVersion="msbuilddefaulttoolsversion">
                    <PropertyGroup>
                        <{PropertyNameToEnableImport}>true</{PropertyNameToEnableImport}>
                        <{MSBuildConstants.MSBuildIsRestoring}>true</{MSBuildConstants.MSBuildIsRestoring}>
                    </PropertyGroup>

                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
                </Project>
                """);

            string projectExtensionsPath = project.GetPropertyValue("MSBuildProjectExtensionsPath");

            projectExtensionsPath.ShouldNotBeNullOrWhiteSpace();
            Directory.Exists(projectExtensionsPath).ShouldBeTrue(
                $"Expected MSBuildProjectExtensionsPath to exist: {projectExtensionsPath}");
            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe(bool.TrueString, StringCompareShould.IgnoreCase);
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBe(bool.TrueString, StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Ensures that even if the MSBuildProjectExtensionsPath exists, the extensions are not imported if the functionality is disabled via the <see cref="PropertyNameToEnableImport"/>.
        /// </summary>
        [Fact]
        public void DoesNotImportProjectWhenDisabled()
        {
            // ---------------------
            // Directory.Build.props
            // ---------------------
            CreateFile(ImportProjectRelativePath, BasicProjectImportContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = CreateProject($"""
                <Project DefaultTargets="Build" ToolsVersion="msbuilddefaulttoolsversion">
                    <PropertyGroup>
                        <{PropertyNameToEnableImport}>false</{PropertyNameToEnableImport}>
                    </PropertyGroup>

                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
                </Project>
                """);

            string projectExtensionsDirectory = Path.Combine(_testRoot.Path, Path.GetDirectoryName(ImportProjectRelativePath));

            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe("false");
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBeEmpty();
            Directory.Exists(projectExtensionsDirectory).ShouldBeTrue(
                $"Expected MSBuildProjectExtensionsPath to exist: {projectExtensionsDirectory}");
            project.GetPropertyValue("MSBuildProjectExtensionsPath").ShouldBe($@"{projectExtensionsDirectory}{Path.DirectorySeparatorChar}");
        }

        /// <summary>
        /// Ensures that if the user set a custom MSBuildProjectExtensionsPath that the import will still succeed.
        /// </summary>
        [Fact]
        public void ImportsProjectIfCustomPath()
        {
            string customImportProjectPath = CreateFile(CustomImportProjectRelativePath, BasicProjectImportContents);
            string customImportDirectory = Path.GetDirectoryName(customImportProjectPath);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = CreateProject($"""
                <Project DefaultTargets="Build" ToolsVersion="msbuilddefaulttoolsversion">
                    <PropertyGroup>
                        <MSBuildProjectExtensionsPath>{customImportDirectory}</MSBuildProjectExtensionsPath>
                    </PropertyGroup>
                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
                </Project>
                """);

            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe("true");
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBe("true");
        }

        /// <summary>
        /// Ensures that if the default MSBuildProjectExtensions directory is used, that the projects will be imported.
        /// </summary>
        [Fact]
        public void ImportsProjectIfExists()
        {
            CreateFile(ImportProjectRelativePath, BasicProjectImportContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = CreateProject("""
                <Project DefaultTargets="Build" ToolsVersion="msbuilddefaulttoolsversion">
                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
                </Project>
                """);

            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe("true");
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBe("true");
        }

        /// <summary>
        /// Ensures that an error is logged if MSBuildProjectExtensionsPath is modified after it was set by Microsoft.Common.props.
        /// </summary>
        [Fact]
        public void ErrorIfChangedInBodyOfProject()
        {
            Project project = CreateProject("""
                <Project DefaultTargets="Build" ToolsVersion="msbuilddefaulttoolsversion">
                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <PropertyGroup>
                        <MSBuildProjectExtensionsPath>foo</MSBuildProjectExtensionsPath>
                    </PropertyGroup>

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
                </Project>
                """);

            MockLogger logger = new MockLogger(_output);

            project.Build("_CheckForInvalidConfigurationAndPlatform", [logger]).ShouldBeFalse();

            logger.Errors.ShouldHaveSingleItem().Code.ShouldBe("MSB3540");
        }

        /// <summary>
        /// Ensures that an error is logged if BaseIntermediateOutputPath is modified after it was set by Microsoft.Common.props and
        /// EnableBaseIntermediateOutputPathMismatchWarning is 'true'.
        /// </summary>
        [Fact]
        public void WarningIfBaseIntermediateOutputPathIsChangedInBodyOfProject()
        {
            Project project = CreateProject("""
                <Project DefaultTargets="Build" ToolsVersion="msbuilddefaulttoolsversion">
                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <PropertyGroup>
                        <EnableBaseIntermediateOutputPathMismatchWarning>true</EnableBaseIntermediateOutputPathMismatchWarning>
                        <BaseIntermediateOutputPath>foo</BaseIntermediateOutputPath>
                    </PropertyGroup>

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
                </Project>
                """);

            MockLogger logger = new MockLogger(_output);

            project.Build("_CheckForInvalidConfigurationAndPlatform", [logger]).ShouldBeTrue();

            logger.Warnings.ShouldHaveSingleItem().Code.ShouldBe("MSB3539");
        }

        private string CreateFile(string relativePath, string contents)
        {
            string directory = Path.GetDirectoryName(relativePath);
            TransientTestFolder folder = string.IsNullOrEmpty(directory)
                ? _testRoot
                : _testRoot.CreateDirectory(directory);

            return folder.CreateFile(Path.GetFileName(relativePath), contents.Cleanup()).Path;
        }

        private Project CreateProject(string projectContents)
        {
            string projectFile = CreateFile(_projectRelativePath, projectContents);
            _output.WriteLine($"Evaluating project '{projectFile}'.");

            return new Project(projectFile, null, null, _projectCollection);
        }
    }
}
