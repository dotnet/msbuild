// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using System;
using System.IO;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Test the NuGet.props import functionality in Microsoft.Common.props.
    /// </summary>
    public sealed class NuGetPropsImportTests : IDisposable
    {
        private const string NuGetPropsContent = @"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <NuGetPropsIsImported>true</NuGetPropsIsImported>
                    </PropertyGroup>
                </Project>";

        private const string NuGetPropsProjectFile = "NuGet.props";
        private const string NuGetPropsPropertyName = "NuGetPropsFile";

        public void Dispose()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Ensures that if a NuGet.props exists, it will be imported.
        /// </summary>
        [Fact]
        public void ImportNuGetPropsWhenExists()
        {
            var projectRelativePath = Path.Combine("src", "foo1", "foo1.csproj");
            var nugetPropsRelativePath = ObjectModelHelpers.CreateFileInTempProjectDirectory(NuGetPropsProjectFile, NuGetPropsContent);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(projectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <{NuGetPropsPropertyName}>{nugetPropsRelativePath}</{NuGetPropsPropertyName}>
                    </PropertyGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                </Project>
            ");

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectRelativePath);

            Assert.Equal("true", project.GetPropertyValue("NuGetPropsIsImported"), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures that if the NuGet.props does not exists no exception will be produced.
        /// </summary>
        [Fact]
        public void ImportNuGetPropsWhenDoesNotExists()
        {
            var projectRelativePath = Path.Combine("src", "foo1", "foo1.csproj");
            var nugetPropsRelativePath = Path.Combine(Path.GetDirectoryName(projectRelativePath), NuGetPropsProjectFile);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(projectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <{NuGetPropsPropertyName}>{nugetPropsRelativePath}</{NuGetPropsPropertyName}>
                    </PropertyGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                </Project>
            ");

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectRelativePath);

            Assert.Empty(project.GetPropertyValue("NuGetPropsIsImported"));
        }
    }
}
