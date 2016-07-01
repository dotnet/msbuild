// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using System;
using System.IO;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A base class for testing the global build project import functionality in Microsoft.Common.props and Microsoft.Common.targets.
    /// </summary>
    abstract public class GlobalBuildProjectImportTestBase : IDisposable
    {
        private const string BasicGlobalBuildProjectContents = @"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <WasGlobalBuildProjectImported>true</WasGlobalBuildProjectImported>
                    </PropertyGroup>
                </Project>";

        private const string ProjectRelativePath = @"src\foo\foo.csproj";

        protected GlobalBuildProjectImportTestBase()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Gets the name of a custom global build project to use.
        /// </summary>
        protected abstract string CustomBuildProjectFile { get; }

        /// <summary>
        /// Gets the name of the property that represents the base path of the global build project file.
        /// </summary>
        protected abstract string GlobalBuildProjectBasePathPropertyName { get; }

        /// <summary>
        /// Gets the name of the default global build project that will be imported.
        /// </summary>
        protected abstract string GlobalBuildProjectFile { get; }

        /// <summary>
        /// Gets the name of the property that represents the name of the global build project file.
        /// </summary>
        protected abstract string GlobalBuildProjectFilePropertyName { get; }

        /// <summary>
        /// Gets the name of the property that represents the full path to the global build project file.
        /// </summary>
        protected abstract string GlobalBuildProjectPathPropertyName { get; }

        /// <summary>
        /// Gets the name of the property used to enable and disable the import functionality.
        /// </summary>
        protected abstract string ImportGlobalBuildProjectPropertyName { get; }

        public void Dispose()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Ensures that if a global build project does not exist, it won't be imported and the project can be successfully evaluated.
        /// </summary>
        [Fact]
        public void DoesNotImportGlobalBuildProjectIfNotExist()
        {
            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(ProjectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("true", project.GetPropertyValue(ImportGlobalBuildProjectPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(GlobalBuildProjectBasePathPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(GlobalBuildProjectFile, project.GetPropertyValue(GlobalBuildProjectFilePropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(GlobalBuildProjectPathPropertyName));
        }

        /// <summary>
        /// Ensures that when the user disables the import by setting the corresponding property to "false", then all of the functionality is disabled.
        /// </summary>
        [Fact]
        public void DoesNotImportGlobalBuildProjectWhenDisabled()
        {
            // ---------------------
            // global.build.props
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(GlobalBuildProjectFile, BasicGlobalBuildProjectContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(ProjectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <{ImportGlobalBuildProjectPropertyName}>false</{ImportGlobalBuildProjectPropertyName}>
                    </PropertyGroup>

                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("false", project.GetPropertyValue(ImportGlobalBuildProjectPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue("WasGlobalBuildProjectImported"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(GlobalBuildProjectBasePathPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(GlobalBuildProjectFilePropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(GlobalBuildProjectPathPropertyName));
        }

        /// <summary>
        /// Ensures that when the user specifies a custom global build props file that it is imported correctly.
        /// </summary>
        [Fact]
        public void ImportsGlobalBuildProjectCustomFile()
        {
            string customFilePath = ObjectModelHelpers.CreateFileInTempProjectDirectory(CustomBuildProjectFile, BasicGlobalBuildProjectContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(ProjectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <{GlobalBuildProjectPathPropertyName}>{customFilePath}</{GlobalBuildProjectPathPropertyName}>
                    </PropertyGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("true", project.GetPropertyValue(ImportGlobalBuildProjectPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("true", project.GetPropertyValue("WasGlobalBuildProjectImported"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(customFilePath, project.GetPropertyValue(GlobalBuildProjectPathPropertyName));
        }

        /// <summary>
        /// Ensures that if a global build project exists, it will be imported.
        /// </summary>
        [Fact]
        public void ImportsGlobalBuildProjectIfExists()
        {
            ObjectModelHelpers.CreateFileInTempProjectDirectory(GlobalBuildProjectFile, BasicGlobalBuildProjectContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(ProjectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("true", project.GetPropertyValue(ImportGlobalBuildProjectPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("true", project.GetPropertyValue("WasGlobalBuildProjectImported"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(ObjectModelHelpers.TempProjectDir, project.GetPropertyValue(GlobalBuildProjectBasePathPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(GlobalBuildProjectFile, project.GetPropertyValue(GlobalBuildProjectFilePropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(Path.Combine(ObjectModelHelpers.TempProjectDir, GlobalBuildProjectFile), project.GetPropertyValue(GlobalBuildProjectPathPropertyName));
        }
    }
}