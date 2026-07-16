// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Evaluation;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Test the NuGet.props import functionality in Microsoft.Common.props.
    /// </summary>
    [TestClass]
    public sealed class NuGetPropsImportTests : IDisposable
    {
        private const string NuGetPropsContent = @"
                <Project>
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
        [MSBuildTestMethod]
        public void ImportNuGetPropsWhenExists()
        {
            var projectRelativePath = Path.Combine("src", "foo1", "foo1.csproj");
            var nugetPropsRelativePath = ObjectModelHelpers.CreateFileInTempProjectDirectory(NuGetPropsProjectFile, NuGetPropsContent);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(projectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <{NuGetPropsPropertyName}>{nugetPropsRelativePath}</{NuGetPropsPropertyName}>
                    </PropertyGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                </Project>
            ");

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectRelativePath);

            Assert.AreEqual("true", project.GetPropertyValue("NuGetPropsIsImported"), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures that if the NuGet.props does not exists no exception will be produced.
        /// </summary>
        [MSBuildTestMethod]
        public void ImportNuGetPropsWhenDoesNotExists()
        {
            var projectRelativePath = Path.Combine("src", "foo1", "foo1.csproj");
            var nugetPropsRelativePath = Path.Combine(Path.GetDirectoryName(projectRelativePath), NuGetPropsProjectFile);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(projectRelativePath, $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <PropertyGroup>
                        <{NuGetPropsPropertyName}>{nugetPropsRelativePath}</{NuGetPropsPropertyName}>
                    </PropertyGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />
                </Project>
            ");

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectRelativePath);

            Assert.IsEmpty(project.GetPropertyValue("NuGetPropsIsImported"));
        }
    }
}
