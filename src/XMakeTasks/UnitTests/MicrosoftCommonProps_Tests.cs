// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class MicrosoftCommonProps_Tests : IDisposable
    {
        private const string BasicGlobalBuildPropsContents = @"
                <Project xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <WasGlobalBuildPropsImported>true</WasGlobalBuildPropsImported>
                    </PropertyGroup>
                </Project>";

        private const string DefaultGlobalBuildPropsFile = "global.build.props";

        private const string ProjectRelativePath = @"src\foo\foo.csproj";

        public MicrosoftCommonProps_Tests()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Ensures that if a global.build.props exists, it will be imported.
        /// </summary>
        [Fact]
        public void ImportsGlobalBuildPropsIfExists()
        {
            // ---------------------
            // global.build.props
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(DefaultGlobalBuildPropsFile, BasicGlobalBuildPropsContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(ProjectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("true", project.GetPropertyValue("ImportGlobalBuildProps"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("true", project.GetPropertyValue("WasGlobalBuildPropsImported"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(ObjectModelHelpers.TempProjectDir, project.GetPropertyValue("_GlobalBuildPropsBasePath"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(DefaultGlobalBuildPropsFile, project.GetPropertyValue("_GlobalBuildPropsFile"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(Path.Combine(ObjectModelHelpers.TempProjectDir, DefaultGlobalBuildPropsFile), project.GetPropertyValue("GlobalBuildPropsPath"));
        }

        /// <summary>
        /// Ensures that if a global.build.props does not exist, it won't be imported and the project can be successfully evaluated.
        /// </summary>
        [Fact]
        public void DoesNotImportGlobalBuildPropsIfNotExist()
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

            Assert.Equal("true", project.GetPropertyValue("ImportGlobalBuildProps"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue("_GlobalBuildPropsBasePath"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(DefaultGlobalBuildPropsFile, project.GetPropertyValue("_GlobalBuildPropsFile"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue("GlobalBuildPropsPath"));
        }

        /// <summary>
        /// Ensures that when the user sets $(ImportGlobalBuildProps) to "false", then all of the functionality is disabled.
        /// </summary>
        [Fact]
        public void DoesNotImportGlobalBuildPropsWhenDisabled()
        {
            // ---------------------
            // global.build.props
            // ---------------------
            ObjectModelHelpers.CreateFileInTempProjectDirectory(DefaultGlobalBuildPropsFile, BasicGlobalBuildPropsContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(ProjectRelativePath, @"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <ImportGlobalBuildProps>false</ImportGlobalBuildProps>
                    </PropertyGroup>

                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("false", project.GetPropertyValue("ImportGlobalBuildProps"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue("WasGlobalBuildPropsImported"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue("_GlobalBuildPropsBasePath"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue("_GlobalBuildPropsFile"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(String.Empty, project.GetPropertyValue("GlobalBuildPropsPath"));
        }

        /// <summary>
        /// 
        /// </summary>
        [Fact]
        public void ImportsGlobalBuildPropsCustomFile()
        {
            const string customFileName = "customfile.props";

            // ---------------------
            // customFile.props
            // ---------------------
            string customFilePath = ObjectModelHelpers.CreateFileInTempProjectDirectory(customFileName, BasicGlobalBuildPropsContents);

            // ---------------------
            // src\Foo\Foo.csproj
            // ---------------------

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(ObjectModelHelpers.CreateFileInTempProjectDirectory(@"src\foo\foo.csproj", $@"
                <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <PropertyGroup>
                        <GlobalBuildPropsPath>{customFilePath}</GlobalBuildPropsPath>
                    </PropertyGroup>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.Common.props` />

                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                </Project>
            "));

            Assert.Equal("true", project.GetPropertyValue("ImportGlobalBuildProps"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("true", project.GetPropertyValue("WasGlobalBuildPropsImported"), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(customFilePath, project.GetPropertyValue("GlobalBuildPropsPath"));
        }

        public void Dispose()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }
    }
}
