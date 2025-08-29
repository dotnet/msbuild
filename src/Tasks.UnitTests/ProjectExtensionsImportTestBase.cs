﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public abstract class ProjectExtensionsImportTestBase : IDisposable
    {
        protected readonly string _projectRelativePath = Path.Combine("src", "foo", "foo.csproj");

        protected ProjectExtensionsImportTestBase()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        protected virtual string BasicProjectImportContents => $@"
            <Project>
                <PropertyGroup>
                <{PropertyNameToSignalImportSucceeded}>true</{PropertyNameToSignalImportSucceeded}>
                </PropertyGroup>
            </Project>";

        protected abstract string CustomImportProjectPath { get; }
        protected abstract string ImportProjectPath { get; }
        protected abstract string PropertyNameToEnableImport { get; }

        /// <summary>
        /// The name of the property to use in a project that is imported.  This base class will generate a project containing the declaration of the property.
        /// </summary>
        protected abstract string PropertyNameToSignalImportSucceeded { get; }

        public void Dispose()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
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

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            string projectExtensionsPath = project.GetPropertyValue("MSBuildProjectExtensionsPath");

            projectExtensionsPath.ShouldNotBeNullOrWhiteSpace();
            Directory.Exists(projectExtensionsPath).ShouldBeFalse();
            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe("true");
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBeEmpty();
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
            ObjectModelHelpers.CreateFileInTempProjectDirectory(ImportProjectPath, BasicProjectImportContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <{PropertyNameToEnableImport}>false</{PropertyNameToEnableImport}>
                    </PropertyGroup>

                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            string projectExtensionsDirectory = Path.Combine(ObjectModelHelpers.TempProjectDir, Path.GetDirectoryName(ImportProjectPath));

            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe("false");
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBeEmpty();
            Directory.Exists(projectExtensionsDirectory).ShouldBeTrue();
            project.GetPropertyValue("MSBuildProjectExtensionsPath").ShouldBe($@"{projectExtensionsDirectory}{Path.DirectorySeparatorChar}");
        }

        /// <summary>
        /// Ensures that if the user set a custom MSBuildProjectExtensionsPath that the import will still succeed.
        /// </summary>
        [Fact]
        public void ImportsProjectIfCustomPath()
        {
            ObjectModelHelpers.CreateFileInTempProjectDirectory(CustomImportProjectPath, BasicProjectImportContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <MSBuildProjectExtensionsPath>{Path.GetDirectoryName(CustomImportProjectPath)}</MSBuildProjectExtensionsPath>
                    </PropertyGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe("true");
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBe("true");
        }

        /// <summary>
        /// Ensures that if the default MSBuildProjectExtensions directory is used, that the projects will be imported.
        /// </summary>
        [Fact]
        public void ImportsProjectIfExists()
        {
            ObjectModelHelpers.CreateFileInTempProjectDirectory(ImportProjectPath, BasicProjectImportContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            project.GetPropertyValue(PropertyNameToEnableImport).ShouldBe("true");
            project.GetPropertyValue(PropertyNameToSignalImportSucceeded).ShouldBe("true");
        }

        /// <summary>
        /// Ensures that an error is logged if MSBuildProjectExtensionsPath is modified after it was set by Microsoft.Common.props.
        /// </summary>
        [Fact]
        public void ErrorIfChangedInBodyOfProject()
        {
            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <PropertyGroup>
                        <MSBuildProjectExtensionsPath>foo</MSBuildProjectExtensionsPath>
                    </PropertyGroup>

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            MockLogger logger = new MockLogger();

            project.Build("_CheckForInvalidConfigurationAndPlatform", new[] { logger }).ShouldBeFalse();

            logger.Errors.Select(i => i.Code).FirstOrDefault().ShouldBe("MSB3540");
        }

        /// <summary>
        /// Ensures that an error is logged if BaseIntermediateOutputPath is modified after it was set by Microsoft.Common.props and
        /// EnableBaseIntermediateOutputPathMismatchWarning is 'true'.
        /// </summary>
        [Fact]
        public void WarningIfBaseIntermediateOutputPathIsChangedInBodyOfProject()
        {
            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <PropertyGroup>
                        <EnableBaseIntermediateOutputPathMismatchWarning>true</EnableBaseIntermediateOutputPathMismatchWarning>
                        <BaseIntermediateOutputPath>foo</BaseIntermediateOutputPath>
                    </PropertyGroup>

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            MockLogger logger = new MockLogger();

            project.Build("_CheckForInvalidConfigurationAndPlatform", new[] { logger }).ShouldBeTrue();

            logger.Warnings.Select(i => i.Code).FirstOrDefault().ShouldBe("MSB3539");
        }
    }
}
