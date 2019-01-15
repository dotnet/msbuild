// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using System;
using System.IO;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A base class for testing the directory build project import functionality in Microsoft.Common.props and Microsoft.Common.targets.
    /// </summary>
    abstract public class DirectoryBuildProjectImportTestBase : IDisposable
    {
        private const string BasicDirectoryBuildProjectContents = @"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <WasDirectoryBuildProjectImported>true</WasDirectoryBuildProjectImported>
                    </PropertyGroup>
                </Project>";

        private readonly string _projectRelativePath = Path.Combine("src", "foo", "foo.csproj");

        protected DirectoryBuildProjectImportTestBase()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Gets the name of a custom directory build project to use.
        /// </summary>
        protected abstract string CustomBuildProjectFile { get; }

        /// <summary>
        /// Gets the name of the property that represents the base path of the directory build project file.
        /// </summary>
        protected abstract string DirectoryBuildProjectBasePathPropertyName { get; }

        /// <summary>
        /// Gets the name of the default directory build project that will be imported.
        /// </summary>
        protected abstract string DirectoryBuildProjectFile { get; }

        /// <summary>
        /// Gets the name of the property that represents the name of the directory build project file.
        /// </summary>
        protected abstract string DirectoryBuildProjectFilePropertyName { get; }

        /// <summary>
        /// Gets the name of the property that represents the full path to the directory build project file.
        /// </summary>
        protected abstract string DirectoryBuildProjectPathPropertyName { get; }

        /// <summary>
        /// Gets the name of the property used to enable and disable the import functionality.
        /// </summary>
        protected abstract string ImportDirectoryBuildProjectPropertyName { get; }

        public void Dispose()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Ensures that if a directory build project does not exist, it won't be imported and the project can be successfully evaluated.
        /// </summary>
        [Fact(Skip = "Tests always have Directory.Build files in the output directory to prevent them from picking up the Directory.Build files in the root of the repo")]
        public void DoesNotImportDirectoryBuildProjectIfNotExist()
        {
            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("true", project.GetPropertyValue(ImportDirectoryBuildProjectPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(DirectoryBuildProjectBasePathPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(DirectoryBuildProjectFile, project.GetPropertyValue(DirectoryBuildProjectFilePropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(DirectoryBuildProjectPathPropertyName));
        }

        /// <summary>
        /// Ensures that when the user disables the import by setting the corresponding property to "false", then all of the functionality is disabled.
        /// </summary>
        [Fact(Skip = "Tests always have Directory.Build files in the output directory to prevent them from picking up the Directory.Build files in the root of the repo")]
        public void DoesNotImportDirectoryBuildProjectWhenDisabled()
        {
            // ---------------------
            // Directory.Build.props
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(DirectoryBuildProjectFile, BasicDirectoryBuildProjectContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <{ImportDirectoryBuildProjectPropertyName}>false</{ImportDirectoryBuildProjectPropertyName}>
                    </PropertyGroup>

                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("false", project.GetPropertyValue(ImportDirectoryBuildProjectPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue("WasDirectoryBuildProjectImported"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(DirectoryBuildProjectBasePathPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(DirectoryBuildProjectFilePropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue(DirectoryBuildProjectPathPropertyName));
        }

        /// <summary>
        /// Ensures that when the user specifies a custom directory build props file that it is imported correctly.
        /// </summary>
        [Fact]
        public void ImportsDirectoryBuildProjectCustomFile()
        {
            string customFilePath = ObjectModelHelpers.CreateFileInTempProjectDirectory(CustomBuildProjectFile, BasicDirectoryBuildProjectContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <{DirectoryBuildProjectPathPropertyName}>{customFilePath}</{DirectoryBuildProjectPathPropertyName}>
                    </PropertyGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("true", project.GetPropertyValue(ImportDirectoryBuildProjectPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("true", project.GetPropertyValue("WasDirectoryBuildProjectImported"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(customFilePath, project.GetPropertyValue(DirectoryBuildProjectPathPropertyName));
        }

        /// <summary>
        /// Ensures that if a directory build project exists, it will be imported.
        /// </summary>
        [Fact]
        public void ImportsDirectoryBuildProjectIfExists()
        {
            ObjectModelHelpers.CreateFileInTempProjectDirectory(DirectoryBuildProjectFile, BasicDirectoryBuildProjectContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(_projectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("true", project.GetPropertyValue(ImportDirectoryBuildProjectPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("true", project.GetPropertyValue("WasDirectoryBuildProjectImported"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(ObjectModelHelpers.TempProjectDir, project.GetPropertyValue(DirectoryBuildProjectBasePathPropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(DirectoryBuildProjectFile, project.GetPropertyValue(DirectoryBuildProjectFilePropertyName), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(Path.Combine(ObjectModelHelpers.TempProjectDir, DirectoryBuildProjectFile), project.GetPropertyValue(DirectoryBuildProjectPathPropertyName));
        }
    }
}
