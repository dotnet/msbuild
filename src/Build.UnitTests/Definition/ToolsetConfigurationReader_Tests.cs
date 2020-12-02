// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if FEATURE_SYSTEM_CONFIGURATION

using System.Configuration;
using Microsoft.Win32;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

using ToolsetConfigurationSection = Microsoft.Build.Evaluation.ToolsetConfigurationSection;
using Xunit;
using System;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    /// Unit tests for ToolsetConfigurationReader class
    /// </summary>
    public class ToolsetConfigurationReaderTests : IDisposable
    {
        private static string s_msbuildToolsets = "msbuildToolsets";

        public void Dispose()
        {
            ToolsetConfigurationReaderTestHelper.CleanUp();
        }

        #region "msbuildToolsets element tests"

        /// <summary>
        ///  msbuildToolsets element is empty
        /// </summary>
        [Fact]
        public void MSBuildToolsetsTest_EmptyElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets />
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            Assert.Null(msbuildToolsetSection.MSBuildOverrideTasksPath);
            Assert.NotNull(msbuildToolsetSection);
            Assert.Null(msbuildToolsetSection.Default);
            Assert.NotNull(msbuildToolsetSection.Toolsets);
            Assert.Empty(msbuildToolsetSection.Toolsets);
        }

        /// <summary>
        ///  tests if ToolsetConfigurationReaderTests is successfully initialized from the config file
        /// </summary>
        [Fact]
        public void MSBuildToolsetsTest_Basic()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ConfigurationSection section = config.GetSection(s_msbuildToolsets);
            ToolsetConfigurationSection msbuildToolsetSection = section as ToolsetConfigurationSection;

            Assert.Null(msbuildToolsetSection.MSBuildOverrideTasksPath);
            Assert.Equal("2.0", msbuildToolsetSection.Default);
            Assert.Single(msbuildToolsetSection.Toolsets);

            Assert.Equal("2.0", msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion);
            Assert.Single(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements);
            Assert.Equal(
              @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\",
              msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value);

            Assert.Empty(msbuildToolsetSection.Toolsets.GetElement(0).AllProjectImportSearchPaths);
        }

        /// <summary>
        ///  Tests if ToolsetConfigurationReaderTests is successfully initialized from the config file when msbuildOVerrideTasksPath is set.
        ///  Also verify the msbuildOverrideTasksPath is properly read in.
        /// </summary>
        [Fact]
        public void MSBuildToolsetsTest_Basic2()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"" msbuildOverrideTasksPath=""c:\foo"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ConfigurationSection section = config.GetSection(s_msbuildToolsets);
            ToolsetConfigurationSection msbuildToolsetSection = section as ToolsetConfigurationSection;

            Assert.Equal("c:\\foo", msbuildToolsetSection.MSBuildOverrideTasksPath);
        }

        /// <summary>
        ///  Tests if ToolsetConfigurationReaderTests is successfully initialized from the config file and that msbuildOVerrideTasksPath 
        ///  is correctly read in when the value is empty.
        /// </summary>
        [Fact]
        public void MSBuildToolsetsTest_Basic3()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"" msbuildOverrideTasksPath="""">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ConfigurationSection section = config.GetSection(s_msbuildToolsets);
            ToolsetConfigurationSection msbuildToolsetSection = section as ToolsetConfigurationSection;

            Assert.Null(msbuildToolsetSection.MSBuildOverrideTasksPath);
        }

        /// <summary>
        ///  tests if ToolsetConfigurationReaderTests is successfully initialized from the config file
        /// </summary>
        [Fact]
        public void MSBuildToolsetsTest_BasicWithOtherConfigEntries()
        {
            // NOTE: for some reason, <configSections> MUST be the first element under <configuration>
            // for the API to read it. The docs don't make this clear.

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                    <startup>
                     <supportedRuntime imageVersion=""v2.0.60510"" version=""v2.0.x86chk""/>
                     <requiredRuntime imageVersion=""v2.0.60510"" version=""v2.0.x86chk"" safemode=""true""/>
                   </startup>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                   <runtime>
                     <assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
                       <dependentAssembly>
                          <assemblyIdentity name=""Microsoft.Build.Framework"" publicKeyToken=""b03f5f7f11d50a3a"" culture=""neutral""/>
                          <bindingRedirect oldVersion=""0.0.0.0-99.9.9.9"" newVersion=""2.0.0.0""/>
                       </dependentAssembly>
                     </assemblyBinding>
                   </runtime>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            Assert.Equal("2.0", msbuildToolsetSection.Default);
            Assert.Single(msbuildToolsetSection.Toolsets);

            Assert.Equal("2.0", msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion);
            Assert.Single(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements);
            Assert.Equal(
              @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\",
              msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value);

            Assert.Empty(msbuildToolsetSection.Toolsets.GetElement(0).AllProjectImportSearchPaths);
        }
        #endregion

        #region "toolsVersion element tests"

        #region "Invalid cases (exception is expected to be thrown)"

        /// <summary>
        /// name attribute is missing from toolset element 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void ToolsVersionTest_NameNotSpecified()
        {
            Assert.Throws<ConfigurationErrorsException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset>
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

                Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

                config.GetSection(s_msbuildToolsets);
            }
           );
        }
        /// <summary>
        ///  More than 1 toolset element with the same name
        /// </summary>
        [Fact]
        public void ToolsVersionTest_MultipleElementsWithSameName()
        {
            Assert.Throws<ConfigurationErrorsException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

                Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

                config.GetSection(s_msbuildToolsets);
            }
           );
        }
        /// <summary>
        /// empty toolset element 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void ToolsVersionTest_EmptyElement()
        {
            Assert.Throws<ConfigurationErrorsException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset />
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

                Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

                config.GetSection(s_msbuildToolsets);
            }
           );
        }
        #endregion

        #region "Valid cases (No exception expected)"

        /// <summary>
        /// only 1 toolset is specified
        /// </summary>
        [Fact]
        public void ToolsVersionTest_SingleElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            Assert.Equal("4.0", msbuildToolsetSection.Default);
            Assert.Single(msbuildToolsetSection.Toolsets);
            Assert.Equal("4.0", msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion);
            Assert.Single(msbuildToolsetSection.Toolsets.GetElement("4.0").PropertyElements);
            Assert.Equal(
              @"D:\windows\Microsoft.NET\Framework\v3.5.x86ret\",
              msbuildToolsetSection.Toolsets.GetElement("4.0").PropertyElements.GetElement("MSBuildBinPath").Value);
        }
        #endregion
        #endregion

        #region "Property"

        #region "Invalid cases (exception is expected to be thrown)"

        /// <summary>
        ///  name attribute is missing
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void PropertyTest_NameNotSpecified()
        {
            Assert.Throws<ConfigurationErrorsException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset toolsVersion=""4.0"">
                       <property value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

                Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

                config.GetSection(s_msbuildToolsets);
            }
           );
        }
        /// <summary>
        /// value attribute is missing
        /// </summary>
        [Fact]
        public void PropertyTest_ValueNotSpecified()
        {
            Assert.Throws<ConfigurationErrorsException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset name=""4.0"">
                       <property name=""MSBuildBinPath"" />
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

                Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

                config.GetSection(s_msbuildToolsets);
            }
           );
        }
        /// <summary>
        /// more than 1 property element with the same name
        /// </summary>
        [Fact]
        public void PropertyTest_MultipleElementsWithSameName()
        {
            Assert.Throws<ConfigurationErrorsException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset ToolsVersion=""msbuilddefaulttoolsversion"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

                Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

                config.GetSection(s_msbuildToolsets);
            }
           );
        }
        /// <summary>
        ///  property element is an empty element
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void PropertyTest_EmptyElement()
        {
            Assert.Throws<ConfigurationErrorsException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property />
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

                Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

                config.GetSection(s_msbuildToolsets);
            }
           );
        }
        #endregion

        #region "Valid cases"

        /// <summary>
        /// more than 1 property element specified
        /// </summary>
        [Fact]
        public void PropertyTest_MultipleElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""SomeOtherPropertyName"" value=""SomeOtherPropertyValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            Assert.Equal("2.0", msbuildToolsetSection.Default);
            Assert.Single(msbuildToolsetSection.Toolsets);
            Assert.Equal(2, msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count);

            Assert.Equal(
              @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\",
              msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value);
            Assert.Equal(
              @"SomeOtherPropertyValue",
              msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("SomeOtherPropertyName").Value);
        }

        /// <summary>
        /// tests GetElement(string name) function in propertycollection class
        /// </summary>
        [Fact]
        public void PropertyTest_GetValueByName()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""SomeOtherPropertyName"" value=""SomeOtherPropertyValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            // Verifications
            Assert.Equal("2.0", msbuildToolsetSection.Default);
            Assert.Single(msbuildToolsetSection.Toolsets);
            Assert.Equal(2, msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count);
            Assert.Equal(@"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\",
                                   msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value);
            Assert.Equal(@"SomeOtherPropertyValue",
                                   msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("SomeOtherPropertyName").Value);
        }

        #endregion
        #endregion

        #region Extensions Paths
        /// <summary>
        ///  Tests multiple extensions paths from the config file, specified for multiple OSes
        /// </summary>
        [Fact]
        public void ExtensionPathsTest_Basic1()
        {
            // NOTE: for some reason, <configSections> MUST be the first element under <configuration>
            // for the API to read it. The docs don't make this clear.

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""MSBuildToolsPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <projectImportSearchPaths>
                         <searchPaths os=""windows"">
                            <property name=""MSBuildExtensionsPath"" value=""c:\foo""/>
                            <property name=""MSBuildExtensionsPath64"" value=""c:\foo64;c:\bar64""/>
                         </searchPaths>
                         <searchPaths os=""osx"">
                            <property name=""MSBuildExtensionsPath"" value=""/tmp/foo""/>
                            <property name=""MSBuildExtensionsPath32"" value=""/tmp/foo32;/tmp/bar32""/>
                         </searchPaths>
                         <searchPaths os=""unix"">
                            <property name=""MSBuildExtensionsPath"" value=""/tmp/bar""/>
                         </searchPaths>
                       </projectImportSearchPaths>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(s_msbuildToolsets) as ToolsetConfigurationSection;

            Assert.Equal("2.0", msbuildToolsetSection.Default);
            Assert.Single(msbuildToolsetSection.Toolsets);

            Assert.Equal("2.0", msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion);
            Assert.Equal(2, msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count);
            Assert.Equal(
              @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\",
              msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value);

            Assert.Equal(3, msbuildToolsetSection.Toolsets.GetElement(0).AllProjectImportSearchPaths.Count);
            var allPaths = msbuildToolsetSection.Toolsets.GetElement(0).AllProjectImportSearchPaths;
            Assert.Equal("windows", allPaths.GetElement(0).OS);
            Assert.Equal(2, allPaths.GetElement(0).PropertyElements.Count);
            Assert.Equal(@"c:\foo", allPaths.GetElement(0).PropertyElements.GetElement("MSBuildExtensionsPath").Value);
            Assert.Equal(@"c:\foo64;c:\bar64", allPaths.GetElement(0).PropertyElements.GetElement("MSBuildExtensionsPath64").Value);

            Assert.Equal("osx", allPaths.GetElement(1).OS);
            Assert.Equal(2, allPaths.GetElement(1).PropertyElements.Count);
            Assert.Equal(@"/tmp/foo", allPaths.GetElement(1).PropertyElements.GetElement("MSBuildExtensionsPath").Value);
            Assert.Equal(@"/tmp/foo32;/tmp/bar32", allPaths.GetElement(1).PropertyElements.GetElement("MSBuildExtensionsPath32").Value);

            Assert.Equal("unix", allPaths.GetElement(2).OS);
            Assert.Single(allPaths.GetElement(2).PropertyElements);
            Assert.Equal( @"/tmp/bar", allPaths.GetElement(2).PropertyElements.GetElement("MSBuildExtensionsPath").Value);

            var reader = GetStandardConfigurationReader();
            Dictionary<string, Toolset> toolsets = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            reader.ReadToolsets(toolsets, new PropertyDictionary<ProjectPropertyInstance>(),
                                new PropertyDictionary<ProjectPropertyInstance>(), true,
                                out string msbuildOverrideTasksPath, out string defaultOverrideToolsVersion);

            Dictionary<string, ProjectImportPathMatch> pathsTable = toolsets["2.0"].ImportPropertySearchPathsTable;
            if (NativeMethodsShared.IsWindows)
            {
                CheckPathsTable(pathsTable, "MSBuildExtensionsPath", new string[] {"c:\\foo"});
                CheckPathsTable(pathsTable, "MSBuildExtensionsPath64", new string[] {"c:\\foo64", "c:\\bar64"});
            }
            else if (NativeMethodsShared.IsOSX)
            {
                CheckPathsTable(pathsTable, "MSBuildExtensionsPath", new string[] {"/tmp/foo"});
                CheckPathsTable(pathsTable, "MSBuildExtensionsPath32", new string[] {"/tmp/foo32", "/tmp/bar32"});
            }
            else
            {
                CheckPathsTable(pathsTable, "MSBuildExtensionsPath", new string[] {"/tmp/bar"});
            }
        }

        private void CheckPathsTable(Dictionary<string, ProjectImportPathMatch> pathsTable, string kind, string[] expectedPaths)
        {
            Assert.True(pathsTable.ContainsKey(kind));
            var paths = pathsTable[kind];
            Assert.Equal(paths.SearchPaths.Count, expectedPaths.Length);

            for (int i = 0; i < paths.SearchPaths.Count; i ++)
            {
                Assert.Equal(paths.SearchPaths[i], expectedPaths[i]);
            }
        }

        /// <summary>
        /// more than 1 searchPaths elements with the same OS
        /// </summary>
        [Fact]
        public void ExtensionsPathsTest_MultipleElementsWithSameOS()
        {
            Assert.Throws<ConfigurationErrorsException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset ToolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>

                       <projectImportSearchPaths>
                         <searchPaths os=""windows"">
                            <property name=""MSBuildExtensionsPath"" value=""c:\foo""/>
                         </searchPaths>
                         <searchPaths os=""windows"">
                            <property name=""MSBuildExtensionsPath"" value=""c:\bar""/>
                         </searchPaths>
                       </projectImportSearchPaths>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

                Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

                config.GetSection(s_msbuildToolsets);
            }
           );
        }

        /// <summary>
        /// more than value is element found for a the same extensions path property name+os
        /// </summary>
        [Fact]
        public void ExtensionsPathsTest_MultipleElementsWithSamePropertyNameForSameOS()
        {
            Assert.Throws<ConfigurationErrorsException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(ObjectModelHelpers.CleanupFileContents(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.0"">
                     <toolset ToolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>

                       <projectImportSearchPaths>
                         <searchPaths os=""windows"">
                            <property name=""MSBuildExtensionsPath"" value=""c:\foo""/>
                            <property name=""MSBuildExtensionsPath"" value=""c:\bar""/>
                         </searchPaths>
                       </projectImportSearchPaths>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>"));

                Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

                config.GetSection(s_msbuildToolsets);
            }
           );
        }

        private ToolsetConfigurationReader GetStandardConfigurationReader()
        {
            return new ToolsetConfigurationReader(new ProjectCollection().EnvironmentProperties, new PropertyDictionary<ProjectPropertyInstance>(), ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest);
        }
        #endregion

    }
}
#endif
