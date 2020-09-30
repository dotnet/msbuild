// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if FEATURE_SYSTEM_CONFIGURATION
using System.Configuration;
#endif
using System.IO;

using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Win32;

#if FEATURE_WIN32_REGISTRY
using RegistryKeyWrapper = Microsoft.Build.Internal.RegistryKeyWrapper;
using RegistryException = Microsoft.Build.Exceptions.RegistryException;
#endif
using InvalidToolsetDefinitionException = Microsoft.Build.Exceptions.InvalidToolsetDefinitionException;
using InternalUtilities = Microsoft.Build.Internal.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests.Definition
{
#if FEATURE_REGISTRY_TOOLSETS

    /// <summary>
    /// Unit tests for ToolsetReader class and its derived classes
    /// </summary>
    [PlatformSpecific(TestPlatforms.Windows)]
    public class ToolsetReaderTests : IDisposable
    {
        // The registry key that is passed as the baseKey parameter to the ToolsetRegistryReader class
        private RegistryKey _testRegistryKey = null;
        // Subkey "4.0"
        private RegistryKey _currentVersionRegistryKey = null;
        // Subkey "ToolsVersions"
        private RegistryKey _toolsVersionsRegistryKey = null;

        // Path to the registry key under HKCU
        // Note that this is a test registry key created solely for unit testing.
        private const string testRegistryPath = @"msbuildUnitTests";

        /// <summary>
        /// Store the value of the "VisualStudioVersion" environment variable here so that 
        /// we can unset it for the duration of the test.
        /// </summary>
        private string _oldVisualStudioVersion;

        /// <summary>
        /// Reset the testRegistryKey
        /// </summary>
        public ToolsetReaderTests()
        {
            Dispose();
            _testRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath);
            _currentVersionRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath + "\\" + Constants.AssemblyVersion);
            _toolsVersionsRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath + "\\ToolsVersions");

            _oldVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            Environment.SetEnvironmentVariable("VisualStudioVersion", null);
        }

        public void Dispose()
        {
#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.CleanUp();
#endif

            DeleteTestRegistryKey();

            Environment.SetEnvironmentVariable("VisualStudioVersion", _oldVisualStudioVersion);
        }

        /// <summary>
        /// Helper class to delete the testRegistryKey tree.
        /// </summary>
        private void DeleteTestRegistryKey()
        {
            if (Registry.CurrentUser.OpenSubKey(testRegistryPath) != null)
            {
                Registry.CurrentUser.DeleteSubKeyTree(testRegistryPath);
            }
        }

#if FEATURE_SYSTEM_CONFIGURATION
        /// <summary>
        /// Test to make sure machine.config file has the section registered
        /// and we are picking it up from there.
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_SectionNotRegisteredInConfigFile()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "The machine.config is only present on Windows"
            }

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Null(msbuildOverrideTasksPath);
            Assert.Null(defaultToolsVersion);
            Assert.Empty(values);
        }
#endif

    #region "Reading from application configuration file tests"

#if FEATURE_SYSTEM_CONFIGURATION

        /// <summary>
        /// Tests that the data is correctly populated using function GetToolsetDataFromConfiguration
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_Basic()
        {
            string v2Folder = NativeMethodsShared.IsWindows
                                  ? @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret"
                                  : Path.Combine(NativeMethodsShared.FrameworkBasePath, "2.0");
            string v4Folder = NativeMethodsShared.IsWindows
                                  ? @"D:\windows\Microsoft.NET\Framework\v4.0.x86ret"
                                  : Path.Combine(NativeMethodsShared.FrameworkBasePath, "4.0");
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"" msbuildOverrideTasksPath=""c:\Cat"" DefaultOverrideToolsVersion=""4.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""" + v2Folder + @"""/>
                     </toolset>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""" + v4Folder + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Equal("c:\\Cat", msbuildOverrideTasksPath);
            Assert.Equal("4.0", defaultOverrideToolsVersion);
            Assert.Equal("2.0", defaultToolsVersion);
            Assert.Equal(2, values.Count);
            Assert.Empty(values["2.0"].Properties);
            Assert.Equal(v2Folder, values["2.0"].ToolsPath);
            Assert.Empty(values["4.0"].Properties);
            Assert.Equal(v4Folder, values["4.0"].ToolsPath);
        }

        /// <summary>
        /// Relative paths can be used in a config file value
        /// </summary>
        [Fact]
        public void RelativePathInValue()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"" msbuildOverrideTasksPath=""..\Foo"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildToolsPath"" value="".." + Path.DirectorySeparatorChar + @"foo""/>
                       <!-- derelativization occurs before comparing toolspath and binpath -->
                       <property name=""MSBuildBinPath"" value="".." + Path.DirectorySeparatorChar + "." + Path.DirectorySeparatorChar + @"foo""/>
                     </toolset>
                     <toolset toolsVersion=""3.0"">
                       <!-- properties are expanded before derelativization-->
                       <property name=""MSBuildBinPath"" value=""$(DotDotSlash)bar""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            PropertyDictionary<ProjectPropertyInstance> pg = new PropertyDictionary<ProjectPropertyInstance>();
            pg.Set(ProjectPropertyInstance.Create("DotDotSlash", @".." + Path.DirectorySeparatorChar));
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), pg, true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            string expected1 = Path.GetFullPath(Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "..", "foo"));
            string expected2 = Path.GetFullPath(Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "..", "bar"));
            Console.WriteLine(values["2.0"].ToolsPath);
            Assert.Equal(expected1, values["2.0"].ToolsPath);
            Assert.Equal(expected2, values["3.0"].ToolsPath);
            Assert.Equal("..\\Foo", msbuildOverrideTasksPath);
        }

        /// <summary>
        /// Invalid relative path in msbuildbinpath value
        /// </summary>
        [Fact]
        public void InvalidRelativePath()
        {
            if (NativeMethodsShared.IsLinux)
            {
                return; // "Cannot force invalid character name on Linux"
            }

            string invalidRelativePath = ".." + Path.DirectorySeparatorChar + ":|invalid|";
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"" msbuildOverrideTasksPath="""">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""" + invalidRelativePath + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();


            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            // Don't crash (consistent with invalid absolute path)
            Assert.Equal(invalidRelativePath, values["2.0"].ToolsPath);
            Assert.Null(msbuildOverrideTasksPath);
        }

        /// <summary>
        /// Tests the case where application configuration file is invalid
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_InvalidXmlFile()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"<INVALIDXML>");

                ToolsetReader reader = GetStandardConfigurationReader();

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                string msbuildOverrideTasksPath = null;
                string defaultOverrideToolsVersion = null;
                reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            }
           );
        }
        /// <summary>
        /// Tests the case where application configuration file is invalid
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_InvalidConfigFile()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolsVersion>
                     <SOMEINVALIDTAG/>
                   </msbuildToolsets>
                 </configuration>");

                ToolsetReader reader = GetStandardConfigurationReader();

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
                string msbuildOverrideTasksPath = null;
                string defaultOverrideToolsVersion = null;
                reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            }
           );
        }
        /// <summary>
        /// Tests the case where application configuration file is empty
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_FileEmpty()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"");

                ToolsetReader reader = new ToolsetConfigurationReader(new ProjectCollection().EnvironmentProperties, new PropertyDictionary<ProjectPropertyInstance>(), ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest);

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                string msbuildOverrideTasksPath = null;
                string defaultOverrideToolsVersion = null;
                reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            }
           );
        }
        /// <summary>
        /// Tests the case when ReadConfiguration throws exception
        /// Make sure that we don't eat it and always throw ConfigurationErrorsException
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_ConfigurationExceptionThrown()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"", new ConfigurationErrorsException());

                ToolsetReader reader = GetStandardConfigurationReader();

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                // this should throw ...
                string msbuildOverrideTasksPath = null;
                string defaultOverrideToolsVersion = null;
                reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            }
           );
        }
        /// <summary>
        /// Tests the case when ReadConfiguration throws exception
        /// Make sure that we don't eat it and always throw ConfigurationErrorsException
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_ConfigurationErrorsExceptionThrown()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"", new ConfigurationErrorsException());

                ToolsetReader reader = GetStandardConfigurationReader();

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                // this should throw ...
                string msbuildOverrideTasksPath = null;
                string defaultOverrideToolsVersion = null;
                reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            }
           );
        }
        /// <summary>
        /// Tests the case where default attribute is not specified in the config file
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_DefaultAttributeNotSpecified()
        {
            string v2Folder = NativeMethodsShared.IsWindows
                                  ? @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret"
                                  : Path.Combine(NativeMethodsShared.FrameworkBasePath, "2.0");
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets msbuildOverrideTasksPath=""C:\Cat"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""" + v2Folder + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Null(defaultToolsVersion);
            Assert.Single(values);
            Assert.Empty(values["2.0"].Properties);
            Assert.Equal(v2Folder, values["2.0"].ToolsPath);
            Assert.Equal("C:\\Cat", msbuildOverrideTasksPath);
        }

        /// <summary>
        /// Default toolset has no toolsVersion element definition
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_DefaultToolsetUndefined()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""nonexistent"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);


            // Should not throw
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
        }

        /// <summary>
        /// Tests the case where msbuildToolsets is not specified in the config file
        /// Basically in the code we should be checking if config.GetSection("msbuildToolsets") returns a null
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_MSBuildToolsetsNodeNotPresent()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Null(defaultToolsVersion);
            Assert.Empty(values);
        }

        /// <summary>
        /// Tests that we handle empty MSBuildToolsets element correctly
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_EmptyMSBuildToolsetsNode()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets/>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Null(defaultToolsVersion);
            Assert.Empty(values);
        }

        /// <summary>
        /// Tests the case where only default ToolsVersion is specified in the application configuration file
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_OnlyDefaultSpecified()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0""/>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);


            // Should not throw
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Empty(values);
        }

        /// <summary>
        /// Tests the case where only one ToolsVersion data is specified in the application configuration file
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_OneToolsVersionNode()
        {
            string v2Folder = NativeMethodsShared.IsWindows
                                  ? @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret"
                                  : Path.Combine(NativeMethodsShared.FrameworkBasePath, "2.0");

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""" + v2Folder + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Equal("2.0", defaultToolsVersion);
            Assert.Equal(v2Folder, values["2.0"].ToolsPath);
            Assert.Single(values);
        }

        /// <summary>
        /// Tests the case when an invalid value of ToolsVersion is specified
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_ToolsVersionIsEmptyString()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion="""">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

                ToolsetReader reader = GetStandardConfigurationReader();

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                //this should throw ...
                string msbuildOverrideTasksPath = null;
                string defaultOverrideToolsVersion = null;
                reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            }
           );
        }
        /// <summary>
        /// If both MSBuildToolsPath and MSBuildBinPath are present, they must match
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_ToolsPathAndBinPathDiffer()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""MSBuildToolsPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

                ToolsetReader reader = GetStandardConfigurationReader();

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                string msbuildOverrideTasksPath = null;
                string defaultOverrideToolsVersion = null;
                reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            }
           );
        }
        /// <summary>
        /// Tests the case when a blank value of PropertyName is specified in the config file
        /// </summary>
        [Fact]
        public void BlankPropertyNameInConfigFile()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name="""" value=""foo""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

                ToolsetReader reader = GetStandardConfigurationReader();

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                //this should throw ...
                string msbuildOverrideTasksPath = null;
                string defaultOverrideToolsVersion = null;
                reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            }
           );
        }
#endif

#if FEATURE_WIN32_REGISTRY
        /// <summary>
        /// Tests the case when a blank property name is specified in the registry
        /// </summary>
        [Fact]
        public void BlankPropertyNameInRegistry()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
                rk.SetValue("MSBuildBinPath", "someBinPath");
                rk.SetValue("", "foo");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                // Should throw ...
                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.Registry
                                                          );
            }
           );
        }
        /// <summary>
        /// Tests the case when a blank property name is specified in the registry in a 
        /// sub-toolset.
        /// </summary>
        [Fact]
        public void BlankPropertyNameInRegistrySubToolset()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
                rk.SetValue("MSBuildBinPath", "someBinPath");

                RegistryKey subToolsetKey = rk.CreateSubKey("11.0");
                subToolsetKey.SetValue("", "foo");

                PropertyDictionary<ProjectPropertyInstance> globalProperties = new PropertyDictionary<ProjectPropertyInstance>();
                globalProperties.Set(ProjectPropertyInstance.Create("VisualStudioVersion", "11.0"));

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                // Should throw ...
                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                               globalProperties,
                                                               ToolsetDefinitionLocations.Registry
                                                          );
            }
           );
        }
#endif
#if FEATURE_SYSTEM_CONFIGURATION
        /// <summary>
        /// Tests the case when a blank property value is specified in the config file
        /// </summary>
        [Fact]
        public void BlankPropertyValueInConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""foo"" value=""""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);


            //this should not throw ...
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
        }
#endif

        /// <summary>
        /// Tests the case when a blank property value is specified in the registry
        /// </summary>
        [Fact]
        public void BlankPropertyValueInRegistry()
        {
            RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "someBinPath");
            rk.SetValue("foo", "");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            // Should not throw ...
            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );
        }

        /// <summary>
        /// Tests the case when a blank property value is specified in the registry
        /// </summary>
        [Fact]
        public void BlankPropertyValueInRegistrySubToolset()
        {
            string binPath = NativeMethodsShared.IsWindows ? @"c:\someBinPath" : "/someBinPath";
            RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", binPath);

            RegistryKey subToolsetKey = rk.CreateSubKey("11.0");
            subToolsetKey.SetValue("foo", "");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            // Should not throw ...
            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );

            Assert.Equal("2.0", defaultToolsVersion);
            Assert.Empty(values["2.0"].Properties);
            Assert.Equal(binPath, values["2.0"].ToolsPath);
            Assert.Single(values["2.0"].SubToolsets);
            Assert.Equal("", values["2.0"].SubToolsets["11.0"].Properties["foo"].EvaluatedValue);
        }

#if FEATURE_SYSTEM_CONFIGURATION
        /// <summary>
        /// Tests the case when an invalid value of PropertyName is specified in the config file
        /// </summary>
        [Fact]
        public void InvalidPropertyNameInConfigFile()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""&amp;"" value=""foo""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

                ToolsetReader reader = GetStandardConfigurationReader();

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                //this should throw ...
                string msbuildOverrideTasksPath = null;
                string defaultOverrideToolsVersion = null;
                reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            }
           );
        }
#endif

        /// <summary>
        /// Tests the case when an invalid value of PropertyName is specified in the registry
        /// </summary>
        [Fact]
        public void InvalidPropertyNameInRegistry()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
                rk.SetValue("MSBuildBinPath", "someBinPath");
                rk.SetValue("foo|bar", "x");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                // Should throw ...
                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                               GetStandardConfigurationReader(),
#endif
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.Registry
                                                           );
            }
           );
        }
        /// <summary>
        /// Tests the case when an invalid value of PropertyName is specified in the registry
        /// </summary>
        [Fact]
        public void InvalidPropertyNameInRegistrySubToolset()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");

                RegistryKey subToolsetKey = rk.CreateSubKey("10.0");
                subToolsetKey.SetValue("MSBuildBinPath", "someBinPath");
                subToolsetKey.SetValue("foo|bar", "x");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                // Should throw ...
                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.Registry
                                                           );
            }
           );
        }

#if FEATURE_SYSTEM_CONFIGURATION
        /// <summary>
        /// Tests that empty string is an invalid value for MSBuildBinPath
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_PropertyValueIsEmptyString1()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Empty(values);
        }

        /// <summary>
        /// Tests that empty string is a valid property value for an arbitrary property
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_PropertyValueIsEmptyString2()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildToolsPath"" value=""xxx""/>
                       <property name=""foo"" value=""""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Single(values);
            Assert.Single(values["2.0"].Properties);
            Assert.Equal(String.Empty, values["2.0"].Properties["foo"].EvaluatedValue);
        }

        /// <summary>
        /// Tests that any escaped xml in config file, is treated well
        /// Note that this comes for free with the current implementation using the 
        /// framework api to access section in the config file
        /// </summary>
        [Fact]
        public void GetToolsetDataFromConfiguration_XmlEscapedCharacters()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2&gt;.0"">
                     <toolset toolsVersion=""2&gt;.0"">
                       <property name=""MSBuildBinPath"" value=""x""/>
                       <property name=""foo"" value=""some&gt;value""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = GetStandardConfigurationReader();

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath;
            string defaultOverrideToolsVersion;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), true, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.Equal("2>.0", defaultToolsVersion);
            Assert.Single(values);
            Assert.Equal(@"some>value", values["2>.0"].Properties["foo"].EvaluatedValue);
        }
#endif
    #endregion

    #region "GetToolsetData tests"

        /// <summary>
        /// Tests the case where registry and config file contains different toolsVersion
        /// </summary>
        [Fact]
        public void GetToolsetData_NoConflict()
        {
            string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
            string binPath2 = NativeMethodsShared.IsWindows ? @"D:\somepath2" : "/somepath2";
            string fworkPath2 = NativeMethodsShared.IsWindows
                                    ? @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret"
                                    : "/windows/Microsoft.NET/Framework/v2.0.x86ret";
            string fworkPath4 = NativeMethodsShared.IsWindows
                                    ? @"D:\windows\Microsoft.NET\Framework\v4.0.x86ret"
                                    : "/windows/Microsoft.NET/Framework/v4.0.x86ret";
            // Set up registry with two tools versions and one property each
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", binPath);
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
            key2.SetValue("MSBuildBinPath", binPath2);

#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.5"">
                     <toolset toolsVersion=""4.5"">
                       <property name=""MSBuildBinPath"" value=""" + fworkPath2 + Path.DirectorySeparatorChar + @"""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""" + fworkPath4 + Path.DirectorySeparatorChar + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            // Verifications
            Assert.Equal(4, values.Count);
            Assert.Equal("4.5", defaultToolsVersion);
            Assert.Equal(binPath, values["2.0"].ToolsPath);
            Assert.Equal(binPath2, values["4.0"].ToolsPath);
            Assert.Equal(fworkPath2, values["4.5"].ToolsPath);
            Assert.Equal(fworkPath4, values["5.0"].ToolsPath);
        }

        /// <summary>
        /// Tests that ToolsetInitialization are respected.
        /// </summary>
        [Fact]
        public void ToolsetInitializationFlagsSetToNone()
        {
            // Set up registry with two tools versions and one property each
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "22.0");
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("33.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            RegistryKey key2 = _testRegistryKey.CreateSubKey("55.0");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.5"">
                     <toolset toolsVersion=""5.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""6.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.None
                                                       );

            // Verifications
            Assert.Single(values);

            string expectedDefault = "2.0";
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 == null)
            {
                expectedDefault = ObjectModelHelpers.MSBuildDefaultToolsVersion;
            }

            Assert.Equal(expectedDefault, defaultToolsVersion);
        }

        /// <summary>
        /// Tests that ToolsetInitialization are respected.
        /// </summary>
        [Fact]
        public void ToolsetInitializationFlagsSetToRegistry()
        {
            string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
            string binPath2 = NativeMethodsShared.IsWindows ? @"D:\somepath2" : "/somepath2";
            // Set up registry with two tools versions and one property each
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", binPath);
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
            key2.SetValue("MSBuildBinPath", binPath2);

#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.5"">
                     <toolset toolsVersion=""4.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );

            // Verifications
            Assert.Equal(2, values.Count);
            Assert.Equal("2.0", defaultToolsVersion);
            Assert.Equal(binPath, values["2.0"].ToolsPath);
            Assert.Equal(binPath2, values["4.0"].ToolsPath);
        }

        [Fact]
        public void ThrowOnNonStringRegistryValueTypes()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
                rk.SetValue("MSBuildBinPath", "someBinPath");

                // Non-string
                rk.SetValue("QuadWordValue", 42, RegistryValueKind.QWord);

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                // Should throw ...
                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                               GetStandardConfigurationReader(),
#endif
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.Registry
                                                           );
            }
           );
        }
        [Fact]
        public void PropertiesInRegistryCannotReferToOtherPropertiesInRegistry()
        {
            string binPath = NativeMethodsShared.IsWindows ? "c:\\x" : "/x";
            RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", binPath + "$(p1)");
            rk.SetValue("p0", "$(p1)");
            rk.SetValue("p1", "v");
            rk.SetValue("p2", "$(p1)");
            rk.SetValue("MSBuildToolsPath", binPath + "$(p1)");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );

            Assert.Equal("", values["2.0"].Properties["p0"].EvaluatedValue);
            Assert.Equal("v", values["2.0"].Properties["p1"].EvaluatedValue);
            Assert.Equal("", values["2.0"].Properties["p2"].EvaluatedValue);
            Assert.Equal(binPath, values["2.0"].ToolsPath);
        }

        [Fact]
        public void SubToolsetPropertiesInRegistryCannotReferToOtherPropertiesInRegistry()
        {
            RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "c:\\x$(p1)");
            rk.SetValue("p0", "$(p1)");
            rk.SetValue("p1", "v");

            RegistryKey subToolsetKey = rk.CreateSubKey("dogfood");
            subToolsetKey.SetValue("p2", "$(p1)");
            subToolsetKey.SetValue("p3", "c:\\x$(p1)");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );

            Assert.Equal("", values["2.0"].Properties["p0"].EvaluatedValue);
            Assert.Equal("v", values["2.0"].Properties["p1"].EvaluatedValue);
            Assert.Equal("", values["2.0"].SubToolsets["dogfood"].Properties["p2"].EvaluatedValue);
            Assert.Equal("c:\\x", values["2.0"].SubToolsets["dogfood"].Properties["p3"].EvaluatedValue);
        }

        [Fact]
        public void SubToolsetsCannotDefineMSBuildToolsPath()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
                rk.SetValue("p0", "$(p1)");
                rk.SetValue("p1", "v");

                RegistryKey subToolsetKey = rk.CreateSubKey("dogfood");
                subToolsetKey.SetValue("p2", "$(p1)");
                subToolsetKey.SetValue("MSBuildToolsPath", "c:\\x$(p1)");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                // throws
                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                               GetStandardConfigurationReader(),
#endif
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.Registry
                                                           );
            }
           );
        }
        /// <summary>
        /// Tests that ToolsetInitialization are respected.
        /// </summary>
        [Fact]
        public void ToolsetInitializationFlagsSetToConfigurationFile()
        {
            string v2Dir = NativeMethodsShared.IsWindows ? "D:\\windows\\Microsoft.NET\\Framework\\v2.0.x86ret" : "/windows/Microsoft.NET/Framework/v2.0.x86ret";
            string v4Dir = NativeMethodsShared.IsWindows ? "D:\\windows\\Microsoft.NET\\Framework\\v4.0.x86ret" : "/windows/Microsoft.NET/Framework/v4.0.x86ret";

            // Set up registry with two tools versions and one property each
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""4.5"">
                     <toolset toolsVersion=""4.5"">
                       <property name=""MSBuildBinPath"" value=""" + v2Dir + @"""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""" + v4Dir + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.ConfigurationFile
                                                       );

            // Verifications
            Assert.Equal(2, values.Count);
            Assert.Equal("4.5", defaultToolsVersion);
            Assert.Equal(v2Dir, values["4.5"].ToolsPath);
            Assert.Equal(v4Dir, values["5.0"].ToolsPath);
        }

        /// <summary>
        /// Properties in the configuration file may refer to a registry location by using the syntax for example
        /// "$(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)", where "HKEY_LOCAL_MACHINE\Software\Vendor\Tools" is the key and
        /// "TaskLocation" is the name of the value.  The name of the value and the preceding "@" may be omitted if
        /// the default value is desired.
        /// </summary>
        [Fact]
        public void PropertyInConfigurationFileReferencesRegistryLocation()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "Registry access is only supported under Windows."
            }

            // Registry Read
            RegistryKey key1 = Registry.CurrentUser.CreateSubKey(@"Software\Vendor\Tools");
            key1.SetValue("TaskLocation", @"somePathToTasks");
            key1.SetValue("TargetsLocation", @"D:\somePathToTargets");
            key1.SetValue("SchemaLocation", @"Schemas");
            key1.SetValue(null, @"D:\somePathToDefault");  //this sets the default value for this key

#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation)""/>
                       <property name=""p1"" value=""$(p2)$(REGISTRY:HKEY_CURRENT_USER\Software\Vendor\Tools)""/>
                       <property name=""p2"" value=""$(p1)\$(Registry:hkey_current_user\Software\Vendor\Tools@TaskLocation)\$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@SchemaLocation)\2.0""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Single(values);
            Assert.Equal(@"D:\somePathToTasks", values["2.0"].ToolsPath);
            Assert.Equal(2, values["2.0"].Properties.Count);
            Assert.Equal(@"D:\somePathToDefault", values["2.0"].Properties["p1"].EvaluatedValue);
            Assert.Equal(@"D:\somePathToDefault\somePathToTasks\Schemas\2.0", values["2.0"].Properties["p2"].EvaluatedValue);

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Vendor");
        }

        [Fact]
        public void ToolsPathInRegistryHasInvalidPathChars()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
                RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
                key1.SetValue("MSBuildBinPath", @"D:\some\foo|bar\path\");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                // should throw... 
                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                               null,
#endif
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.Registry
                                                           );
            }
           );
        }

#if FEATURE_SYSTEM_CONFIGURATION
        [Fact]
        public void SamePropertyDefinedMultipleTimesForSingleToolsVersionInConfigurationFile()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                       <property name=""MSBuildBinPath"" value=""C:\$(p1)\folder""/>
                       <property name=""p1"" value=""newValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               null,
                                                               GetStandardConfigurationReader(),
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.ConfigurationFile
                                                           );
            }
           );
        }

        [Fact]
        public void SamePropertyDifferentCaseDefinedMultipleTimesForSingleToolsVersionInConfigurationFile()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                       <property name=""mSbUiLdBiNpAtH"" value=""C:\$(p1)\folder""/>
                       <property name=""P1"" value=""newValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               null,
                                                               GetStandardConfigurationReader(),
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.ConfigurationFile
                                                           );
            }
           );
        }


        [Fact]
        public void SameToolsVersionDefinedMultipleTimesInConfigurationFile()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                     </toolset>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\folder""/>
                       <property name=""p2"" value=""anotherValue""/>
                     </toolset>
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               null,
                                                               GetStandardConfigurationReader(),
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.ConfigurationFile
                                                           );
            }
           );
        }

        [Fact]
        public void SameToolsVersionDifferentCaseDefinedMultipleTimesInConfigurationFile()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""DevDiv"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                     </toolset>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\folder""/>
                       <property name=""p2"" value=""anotherValue""/>
                     </toolset>
                     <toolset toolsVersion=""DEVDIV"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               null,
                                                               GetStandardConfigurationReader(),
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.ConfigurationFile
                                                           );
            }
           );
        }

        [Fact]
        public void CannotSetReservedPropertyInConfigFile()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""MSBuildProjectFile"" value=""newValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               null,
                                                               GetStandardConfigurationReader(),
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.ConfigurationFile
                                                           );
            }
           );
        }
#endif

        [Fact]
        public void CannotSetReservedPropertyInRegistry()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                // Registry Read
                RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
                key1.SetValue("MSBuildBinPath", @"D:\somepath");
                key1.SetValue("MSBuildProjectFile", @"SomeRegistryValue");


                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                               null,
#endif
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.Registry
                                                           );
            }
           );
        }

        [Fact]
        public void CannotSetReservedPropertyInRegistrySubToolset()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                // Registry Read
                RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
                RegistryKey subKey1 = key1.CreateSubKey("Foo");

                subKey1.SetValue("MSBuildBinPath", @"D:\somepath");
                subKey1.SetValue("MSBuildProjectFile", @"SomeRegistryValue");

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                               null,
#endif
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.Registry
                                                           );
            }
           );
        }

#if FEATURE_SYSTEM_CONFIGURATION
        /// <summary>
        /// Properties defined in previously processed toolset definitions should
        /// not affect the evaluation of subsequent toolset definitions.
        /// </summary>
        [Fact]
        public void NoInterferenceBetweenToolsetDefinitions()
        {
            string v20Dir = NativeMethodsShared.IsWindows ? @"D:\20\some\folder\on\disk" : "/20/some/folder/on/disk";
            string v35Dir = NativeMethodsShared.IsWindows ? @"D:\35\some\folder\on\disk" : "/35/some/folder/on/disk";
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""" + v20Dir + @"""/>
                       <property name=""p1"" value=""another""/>
                       <property name=""p4"" value=""fourth$(p3)Value"" />
                     </toolset>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""" + v35Dir + @"""/>
                       <property name=""p2"" value=""some$(p1)value""/>
                       <property name=""p3"" value=""propertyValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           null,
                                                           GetStandardConfigurationReader(),
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),

                                                           ToolsetDefinitionLocations.ConfigurationFile
                                                       );

            Assert.Equal(2, values.Count);

            Assert.Equal(v20Dir, values["2.0"].ToolsPath);
            Assert.Equal(2, values["2.0"].Properties.Count);
            Assert.Equal(@"another", values["2.0"].Properties["p1"].EvaluatedValue);
            Assert.Equal(@"fourthValue", values["2.0"].Properties["p4"].EvaluatedValue);

            Assert.Equal(v35Dir, values["4.0"].ToolsPath);
            Assert.Equal(2, values["4.0"].Properties.Count);
            Assert.Equal(@"somevalue", values["4.0"].Properties["p2"].EvaluatedValue);
            Assert.Equal(@"propertyValue", values["4.0"].Properties["p3"].EvaluatedValue);
        }
#endif

        /// <summary>
        /// Properties in the configuration file may refer to a registry location by using the syntax for example
        /// "$(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)", where "HKEY_LOCAL_MACHINE\Software\Vendor\Tools" is the key and
        /// "TaskLocation" is the name of the value.  The name of the value and the preceding "@" may be omitted if
        /// the default value is desired.
        /// </summary>
        [Fact]
        public void ConfigFileInvalidRegistryExpression1()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "Access local machine registry is for Windows only"
            }

            // No location
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:)""/>");
        }

        [Fact]
        public void ConfigFileInvalidRegistryExpression2()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "Access local machine registry is for Windows only"
            }

            // Bogus key expression
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:__bogus__)""/>");
        }

        [Fact]
        public void ConfigFileInvalidRegistryExpression3()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "Access local machine registry is for Windows only"
            }

            // No registry location just @
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:@)""/>");
        }

        [Fact]
        public void ConfigFileInvalidRegistryExpression4()
        {
            // Double @
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@@TaskLocation)""/>");
        }

        [Fact]
        public void ConfigFileInvalidRegistryExpression5()
        {
            // Trailing @
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation@)""/>");
        }

        [Fact]
        public void ConfigFileInvalidRegistryExpression6()
        {
            // Leading @
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:@HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation)""/>");
        }

        [Fact]
        public void ConfigFileInvalidRegistryExpression7()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "Access registry is for Windows only"
            }

            // Bogus hive
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:BOGUS_HIVE\Software\Vendor\Tools@TaskLocation)""/>");
        }

        [Fact]
        public void ConfigFileStringEmptyRegistryExpression1()
        {
            // Regular undefined property beginning with "Registry"
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry)""/>",
                                          String.Empty);
        }

        [Fact]
        public void ConfigFileStringEmptyRegistryExpression2()
        {
            // Nonexistent key
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:HKEY_CURRENT_USER\Nonexistent_Key\Software\Vendor\Tools@TaskLocation)""/>",
                                          String.Empty);
        }

        [Fact]
        public void ConfigFileNonPropertyRegistryExpression1()
        {
            // Property not terminated with paren, does not look like property
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation""/>",
                                          @"$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation");
        }

        [Fact]
        public void ConfigFileNonPropertyRegistryExpression2()
        {
            // Missing colon, looks like regular property (but with invalid property name chars, we will return blank as a result)
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""$(RegistryHKEY_CURRENT_USER\Software\Vendor\Tools@@TaskLocation)""/>",
                                          String.Empty);
        }

        [Fact]
        public void ConfigFileItemExpressionsDoNotExpandInConfigurationProperties()
        {
            // Expect that item expressions such as '@(SomeItem)' are not evaluated in any way, e.g., they are treated literally
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""@(SomeItem)""/>",
                                          @"@(SomeItem)");
        }

        [Fact]
        public void RegistryInvalidRegistryExpression1()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "Access local machine registry is for Windows only"
            }

            // Bogus key expression
            RegistryInvalidRegistryExpressionHelper("$(Registry:__bogus__)");
        }

        [Fact]
        public void RegistryValidRegistryExpression1()
        {
            // Regular undefined property beginning with "Registry"
            RegistryValidRegistryExpressionHelper("$(Registry)", String.Empty);
        }

        private void RegistryInvalidRegistryExpressionHelper(string propertyExpression)
        {
            bool caught = false;
            try
            {
                // this should throw...
                RegistryValidRegistryExpressionHelper(propertyExpression, String.Empty);
            }
            catch (InvalidToolsetDefinitionException ex)
            {
                Console.WriteLine(ex.Message);
                caught = true;
            }

            Assert.True(caught);
        }

        private void RegistryValidRegistryExpressionHelper(string propertyExpression, string expectedValue)
        {
            // Registry Read
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", "xxxx");
            key1.SetValue("p", propertyExpression);

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                       (
                                           values,
                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                           GetStandardConfigurationReader(),
#endif
                                           new ProjectCollection().EnvironmentProperties,
                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                           ToolsetDefinitionLocations.Registry
                                       );

            Assert.Single(values);
            Assert.Equal(expectedValue, values["2.0"].Properties["p"].EvaluatedValue);
        }

        /// <summary>
        /// Tests that an invalid registry property expression causes an exception (resulting in a project load error)
        /// </summary>
        /// <param name="propertyExpression"></param>
        private void ConfigFileInvalidRegistryExpressionHelper(string propertyExpression)
        {
            bool caught = false;
            try
            {
                // this should throw...
                ConfigFileValidRegistryExpressionHelper(propertyExpression, String.Empty);
            }
            catch (InvalidToolsetDefinitionException ex)
            {
                Console.WriteLine(ex.Message);
                caught = true;
            }

            Assert.True(caught);
        }

        /// <summary>
        /// Tests that a specified registry property expression evaluates to specified value
        /// </summary>
        /// <param name="propertyExpression"></param>
        /// <param name="expectedValue"></param>
        private void ConfigFileValidRegistryExpressionHelper(string propertyExpression, string expectedValue)
        {
#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""xxxx""/>
                       " + propertyExpression + @"
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                       (
                                           values,
                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                           GetStandardConfigurationReader(),
#endif
                                           new ProjectCollection().EnvironmentProperties,
                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                           ToolsetDefinitionLocations.ConfigurationFile
                                       );

            Assert.Single(values);
            Assert.Equal(expectedValue, values["2.0"].Properties["p"].EvaluatedValue);
        }

        /// <summary>
        /// Tests the case where application configuration file overrides a value already specified in the registry
        /// </summary>
        [Fact]
        public void GetToolsetData_ConflictingPropertyValuesSameCase()
        {
            string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
            string overrideBinPath = NativeMethodsShared.IsWindows ? @"D:\somedifferentpath" : "/somedifferentpath";
            // Registry Read
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", binPath);

#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""" + overrideBinPath + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Single(values);
            Assert.Empty(values["2.0"].Properties);
            Assert.Equal(overrideBinPath, values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Tests the case where application configuration file overrides a value already specified in the registry, 
        /// where that registry value is bogus and would otherwise throw.  However, since the config file also 
        /// contains an entry for that toolset, the registry toolset never gets read, and thus never throws.  
        /// </summary>
        [Fact]
        public void GetToolsetData_ConflictingPropertyValuesRegistryThrows()
        {
            string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
            // Registry Read
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"$([MSBuild]::SomeNonexistentPropertyFunction())");

#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""" + binPath + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Single(values);
            Assert.Empty(values["2.0"].Properties);
            Assert.Equal(binPath, values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Tests when properties are defined in the registry as
        /// well as in the config file for the same tools version.
        /// We should not merge them; we should take the config file ones only
        /// </summary>
        [Fact]
        public void GetToolsetData_NoMerging()
        {
            string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
            string overrideBinPath = NativeMethodsShared.IsWindows ? @"D:\someotherpath" : "/someotherpath";
            // Registry Read
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", binPath);
            key1.SetValue("SomeRegistryProperty", @"SomeRegistryValue");

            // Set the config file contents as needed
#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""" + overrideBinPath + @"""/>
                       <property name=""SomeConfigProperty"" value=""SomeConfigValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Single(values);
            Assert.Single(values["2.0"].Properties);
            Assert.Equal(overrideBinPath, values["2.0"].ToolsPath);
            Assert.Null(values["2.0"].Properties["SomeRegistryProperty"]); // Was zapped
            Assert.Equal(@"SomeConfigValue", values["2.0"].Properties["SomeConfigProperty"].EvaluatedValue);
        }

        /// <summary>
        /// The absence of the ToolsVersion attribute on the main Project element in a project file means
        /// that the engine's default tools version should be used.
        /// </summary>
        [Fact]
        public void ToolsVersionAttributeNotSpecifiedOnProjectElementAndDefaultVersionSpecifiedInRegistry()
        {
            string oldValue = Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION");

            try
            {
                // In the new world of figuring out the ToolsVersion to use, we completely ignore the default
                // ToolsVersion in the ProjectCollection.  However, this test explicitly depends on modifying 
                // that, so we need to turn the new defaulting behavior off in order to verify that this still works.  
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
                InternalUtilities.RefreshInternalEnvironmentValues();

                ProjectCollection projectCollection = new ProjectCollection();

                string msbuildOverrideTasksPath = null;
                projectCollection.AddToolset(new Toolset("2.0", "20toolsPath", projectCollection, msbuildOverrideTasksPath));
                projectCollection.AddToolset(new Toolset(ObjectModelHelpers.MSBuildDefaultToolsVersion, "120toolsPath", projectCollection, msbuildOverrideTasksPath));

                string projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("x.proj", @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />");

                Project project = projectCollection.LoadProject(projectPath);

                string defaultExpected = "Current";
                if (FrameworkLocationHelper.PathToDotNetFrameworkV20 == null)
                {
                    defaultExpected = ObjectModelHelpers.MSBuildDefaultToolsVersion;
                }

                Assert.Equal(defaultExpected, project.ToolsVersion);
                Assert.Equal(defaultExpected, projectCollection.DefaultToolsVersion);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", oldValue);
                InternalUtilities.RefreshInternalEnvironmentValues();
            }
        }

        /// <summary>
        /// Tests the case when no values are specified in the registry
        /// </summary>
        [Fact]
        public void GetToolsetData_RegistryNotPresent()
        {
            string binPath = NativeMethodsShared.IsWindows ? @"D:\somedifferentpath" : "/somedifferentpath";
#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBUILDBINPATH"" value=""" + binPath + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Single(values);
            Assert.Empty(values["2.0"].Properties);
            Assert.Equal(binPath, values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Test the case where nothing is specified in the config file
        /// Note that config file not present is same as config file 
        /// with no MSBuildToolsets Section 
        /// </summary>
        [Fact]
        public void GetToolsetData_ConfigFileNotPresent()
        {
            // Registry Read
            string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", binPath);

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Single(values);
            Assert.Empty(values["2.0"].Properties);
            Assert.Equal(binPath, values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Tests the case where nothing is specified in registry and config file
        /// </summary>
        [Fact]
        public void GetToolsetData_RegistryAndConfigNotPresent()
        {
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            // Should either be the last-ditch 2.0 toolset, or if 2.0 is not installed, then the last-last-ditch of 4.0
            Assert.Single(values);
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 != null)
            {
                Assert.Equal("2.0", defaultToolsVersion);
            }
            else
            {
                Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, defaultToolsVersion);
            }
        }

        /// <summary>
        /// Tests the case when reading config file throws an exception
        /// </summary>
        [Fact]
        public void GetToolsetData_ReadConfigThrowsException()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                // Registry Read
                RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
                key1.SetValue("MSBuildBinPath", @"D:\somepath");

                // Set the config helper to throw exception
#if FEATURE_SYSTEM_CONFIGURATION
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"", new ConfigurationErrorsException());
#endif

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                ToolsetReader.ReadAllToolsets
                           (
                               values,
                               GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                               GetStandardConfigurationReader(),
#endif
                               new ProjectCollection().EnvironmentProperties,
                               new PropertyDictionary<ProjectPropertyInstance>(),
                               ToolsetDefinitionLocations.Default
                           );
            }
           );
        }
        /// <summary>
        /// Tests the case where reading from registry throws exception
        /// </summary>
        [Fact]
        public void GetToolsetData_ReadRegistryOpenSubKeyThrowsException()
        {
            Assert.Throws<InvalidToolsetDefinitionException>(() =>
            {
                RegistryKeyWrapper mockRegistryKey =
                    new MockRegistryKey(testRegistryPath, MockRegistryKey.WhereToThrow.OpenSubKey);

#if FEATURE_SYSTEM_CONFIGURATION
                ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\somedifferentpath""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

                Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

                ToolsetReader.ReadAllToolsets
                                                           (
                                                               values,
                                                               new ToolsetRegistryReader(new ProjectCollection().EnvironmentProperties, new PropertyDictionary<ProjectPropertyInstance>(), mockRegistryKey),
#if FEATURE_SYSTEM_CONFIGURATION
                                                               GetStandardConfigurationReader(),
#endif
                                                               new ProjectCollection().EnvironmentProperties,
                                                               new PropertyDictionary<ProjectPropertyInstance>(),
                                                               ToolsetDefinitionLocations.Default
                                                           );
            });
        }

    #endregion

    #region "SetDefaultToolsetVersion tests"

        /// <summary>
        /// Tests that the default ToolsVersion is correctly resolved when specified
        /// in registry and config file
        /// </summary>
        [Fact]
        public void SetDefaultToolsetVersion_SpecifiedInRegistryAndConfigFile()
        {
            // Set up registry with two tools versions and one property each
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""5.0"">
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Equal("5.0", defaultToolsVersion);
        }

        /// <summary>
        /// Tests that the default ToolsVersion is correctly resolved when specified in registry only
        /// </summary>
        [Fact]
        public void SetDefaultToolsetVersion_SpecifiedOnlyInRegistry()
        {
#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""3.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            // Set up registry with two tools versions and one property each
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "4.0");
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Equal("4.0", defaultToolsVersion);
        }

        /// <summary>
        /// Tests that the override task path is correctly resolved when specified in registry only
        /// </summary>
        [Fact]
        public void SetOverrideTasks_SpecifiedOnlyInRegistry()
        {
#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""3.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            string overridePath = NativeMethodsShared.IsWindows ? "c:\\TaskOverridePath" : "/TaskOverridePath";
            // Set up registry with two tools versions and one property each
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "4.0");
            _currentVersionRegistryKey.SetValue("msbuildOverrideTasksPath", overridePath);
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");
            key2.SetValue("MSBuildBinPath", @"D:\OtherTaskOverridePath");
            RegistryKey key3 = _toolsVersionsRegistryKey.CreateSubKey("5.0");
            key3.SetValue("MSBuildBinPath", @"D:\somepath3");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Equal("4.0", defaultToolsVersion);
            Assert.Equal(overridePath, values["4.0"].OverrideTasksPath);
            // Assert.Equal("c:\\OtherTaskOverridePath", values["5.0"].OverrideTasksPath); // UNDONE: Per-toolset override paths don't work.
        }

        /// <summary>
        /// Tests that the override default toolsversion is correctly resolved when specified in registry only
        /// </summary>
        [Fact]
        public void SetDefaultOverrideToolsVersion_SpecifiedOnlyInRegistry()
        {
#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""3.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            // Set up registry with two tools versions and one property each
            _currentVersionRegistryKey.SetValue("DefaultOverrideToolsVersion", "13.0");
            RegistryKey key = _toolsVersionsRegistryKey.CreateSubKey("13.0");
            key.SetValue("MSBuildBinPath", @"D:\somepath2");
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
            key2.SetValue("MSBuildBinPath", @"D:\somepath3");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Equal("13.0", values["4.0"].DefaultOverrideToolsVersion);
        }

#if FEATURE_SYSTEM_CONFIGURATION
        /// <summary>
        /// Tests that the default ToolsVersion is correctly resolved
        /// when specified in config file only
        /// </summary>
        [Fact]
        public void SetDefaultToolsetVersion_SpecifiedOnlyInConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""5.0"">
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );


            Assert.Equal("5.0", defaultToolsVersion);
        }

        /// <summary>
        /// Tests that the override tasks path ToolsVersion is correctly resolved
        /// when specified in config file only.
        /// Also, that MSBuildOverrideTasksPath can be overridden.
        /// </summary>
        [Fact]
        public void SetOverrideTaskPath_SpecifiedOnlyInConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""5.0"" msbuildOverrideTasksPath=""C:\TaskOverride"">
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                       <property name=""MSBuildOverrideTasksPath"" value=""c:\OtherTaskOverride""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );


            Assert.Equal("5.0", defaultToolsVersion);
            Assert.Equal("C:\\TaskOverride", values["4.0"].OverrideTasksPath);
            // Assert.Equal("C:\\OtherTaskOverride", values["5.0"].OverrideTasksPath); // UNDONE: Per-toolset override paths aren't working
        }

        /// <summary>
        /// Tests that the override default ToolsVersion is correctly resolved
        /// when specified in config file only.
        /// </summary>
        [Fact]
        public void SetDefaultOverrideToolsVersion_SpecifiedOnlyInConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""5.0"" DefaultOverrideToolsVersion=""3.0"">
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Equal("5.0", defaultToolsVersion);
            Assert.Equal("3.0", values["4.0"].DefaultOverrideToolsVersion);
        }
#endif

        /// <summary>
        /// Tests that the default ToolsVersion is correctly resolved when specified nowhere
        /// </summary>
        [Fact]
        public void SetDefaultToolsetVersion_SpecifiedNowhere()
        {
#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v4.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new PropertyDictionary<ProjectPropertyInstance>(),
                                                           ToolsetDefinitionLocations.Default
                                                       );

            string expectedDefault = "2.0";
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 == null)
            {
                expectedDefault = ObjectModelHelpers.MSBuildDefaultToolsVersion;
            }

            Assert.Equal(expectedDefault, defaultToolsVersion); // built-in default
            Assert.Null(values[expectedDefault].OverrideTasksPath);
            Assert.Null(values[expectedDefault].DefaultOverrideToolsVersion);
        }

#if FEATURE_SYSTEM_CONFIGURATION
        /// <summary>
        /// Tests that properties are properly expanded when reading them from the config file
        /// </summary>
        [Fact]
        public void PropertiesInToolsetsFromConfigFileAreExpanded()
        {
            // $(COMPUTERNAME) is just a convenient env var. $(NUMBER_OF_PROCESSORS) isn't defined on Longhorn
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""p1"" value=""v1$(p2)""/>
                       <property name=""p2"" value=""__$(p1)__""/>
                       <property name=""p3"" value=""$(COMPUTERNAME)""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new ProjectCollection().GlobalPropertiesCollection,
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Equal("v1", values["4.0"].Properties["p1"].EvaluatedValue);
            // Properties can refer to other properties also defined in the config file
            Assert.Equal("__v1__", values["4.0"].Properties["p2"].EvaluatedValue);
            Assert.Equal(Environment.MachineName, values["4.0"].Properties["p3"].EvaluatedValue);
        }

        /// <summary>
        /// Tests that properties in MSBuildToolsPath are properly expanded when reading them from the config file
        /// </summary>
        [Fact]
        public void PropertiesInToolsetsFromConfigFileAreExpandedInToolsPath()
        {
            string binPathConfig = NativeMethodsShared.IsWindows ?
                @"D:\windows\$(p1)\Framework\v2.0.x86ret\$(COMPUTERNAME)" :
                "/windows/$(p1)/Framework/v2.0.x86ret";
            string toolsPathConfig = NativeMethodsShared.IsWindows ?
                @"D:\$(p2)\$(p1)\Framework\v2.0.x86ret\$(COMPUTERNAME)" :
                "/$(p2)/$(p1)/Framework/v2.0.x86ret";

            // $(COMPUTERNAME) is just a convenient env var. $(NUMBER_OF_PROCESSORS) isn't defined on Longhorn
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""4.0"">
                       <property name=""p1"" value=""Microsoft.NET""/>
                       <property name=""p2"" value=""windows""/>
                       <property name=""MSBuildBinPath"" value=""" + binPathConfig + @"""/>
                       <property name=""MSBuildToolsPath"" value=""" + toolsPathConfig + @"""/>
                       <property name=""p3"" value=""v3$(MSBuildToolsPath)""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           new ProjectCollection().EnvironmentProperties,
                                                           new ProjectCollection().GlobalPropertiesCollection,
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Equal("Microsoft.NET", values["4.0"].Properties["p1"].EvaluatedValue);
            Assert.Equal("windows", values["4.0"].Properties["p2"].EvaluatedValue);
            string expectedToolsPath = NativeMethodsShared.IsWindows
                                           ? @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"
                                             + Environment.MachineName
                                           : "/windows/Microsoft.NET/Framework/v2.0.x86ret";
            Assert.Equal(expectedToolsPath, values["4.0"].ToolsPath);
            Assert.Equal("v3" + expectedToolsPath, values["4.0"].Properties["p3"].EvaluatedValue);
        }
#endif

        /// <summary>
        /// Global properties are available, but they cannot be overwritten by other toolset properties, just as they cannot
        /// be overwritten by project file properties.
        /// </summary>
        [Fact]
        public void GlobalPropertiesInToolsetsAreExpandedButAreNotOverwritten()
        {
#if FEATURE_SYSTEM_CONFIGURATION
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""p1"" value=""$(gp1)""/>
                       <property name=""gp1"" value=""v2""/>
                       <property name=""p2"" value=""$(gp1)""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");
#endif

            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties["gp1"] = "gv1";
            ProjectCollection e = new ProjectCollection(globalProperties, null, ToolsetDefinitionLocations.None);
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           GetStandardRegistryReader(),
#if FEATURE_SYSTEM_CONFIGURATION
                                                           GetStandardConfigurationReader(),
#endif
                                                           e.EnvironmentProperties,
                                                           e.GlobalPropertiesCollection,
                                                           ToolsetDefinitionLocations.Default
                                                       );

            Assert.Equal("gv1", values["4.0"].Properties["p1"].EvaluatedValue);
            Assert.Equal("gv1", values["4.0"].Properties["p2"].EvaluatedValue);
        }

    #endregion

        private ToolsetRegistryReader GetStandardRegistryReader()
        {
            return new ToolsetRegistryReader(new ProjectCollection().EnvironmentProperties, new PropertyDictionary<ProjectPropertyInstance>(), new MockRegistryKey(testRegistryPath));
        }

#if FEATURE_SYSTEM_CONFIGURATION
        private ToolsetConfigurationReader GetStandardConfigurationReader()
        {
            return new ToolsetConfigurationReader(new ProjectCollection().EnvironmentProperties, new PropertyDictionary<ProjectPropertyInstance>(), ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest);
        }
#endif
    }

#endif

#if FEATURE_WIN32_REGISTRY
    internal class MockRegistryKey : RegistryKeyWrapper
    {
        public enum WhereToThrow
        {
            None,
            Name,
            GetValue,
            GetValueNames,
            GetSubKeyNames,
            OpenSubKey
        }

        private WhereToThrow _whereToThrow = WhereToThrow.None;
        private string _subKeyThatDoesNotExist = null;

        /// <summary>
        /// Construct the mock key with a specified key
        /// </summary>
        /// <param name="path"></param>
        protected MockRegistryKey(RegistryKey wrappedKey, RegistryKey registryHive)
            : base(wrappedKey, registryHive)
        { }

        /// <summary>
        /// Construct the mock key with a wrapper
        /// </summary>
        /// <param name="path"></param>
        public MockRegistryKey(string path)
            : base(path, Registry.CurrentUser)
        { }

        /// <summary>
        /// Construct the mock key with a wrapper and a designated method
        /// to throw from
        /// </summary>
        /// <param name="path"></param>
        /// <param name="whereToThrow"></param>
        public MockRegistryKey(string path, WhereToThrow whereToThrow)
            : base(path, Registry.CurrentUser)
        {
            _whereToThrow = whereToThrow;
        }

        /// <summary>
        /// Construct the mock key with a wrapper and a designated subkey
        /// to refuse to open
        /// </summary>
        /// <param name="path"></param>
        /// <param name="whereToThrow"></param>
        public MockRegistryKey(string path, string subKeyThatDoesNotExist)
            : base(path, Registry.CurrentUser)
        {
            _subKeyThatDoesNotExist = subKeyThatDoesNotExist;
        }

        /// <summary>
        /// Name of the registry key
        /// </summary>
        public override string Name
        {
            get
            {
                if (_whereToThrow == WhereToThrow.Name)
                {
                    throw new RegistryException("registryException", "registry");
                }
                return base.Name;
            }
        }

        /// <summary>
        /// Gets the value with name "name" stored under this registry key
        /// </summary>
        public override object GetValue(string name)
        {
            if (_whereToThrow == WhereToThrow.GetValue)
            {
                throw new RegistryException("registryException", "registry");
            }
            return base.GetValue(name);
        }

        /// <summary>
        /// Gets the names of all values underneath this registry key
        /// </summary>
        public override string[] GetValueNames()
        {
            if (_whereToThrow == WhereToThrow.GetValueNames)
            {
                throw new RegistryException("registryException", "registry");
            }
            return base.GetValueNames();
        }

        /// <summary>
        /// Gets the names of all sub keys immediately below this registry key
        /// </summary>
        /// <returns></returns>
        public override string[] GetSubKeyNames()
        {
            if (_whereToThrow == WhereToThrow.GetSubKeyNames)
            {
                throw new RegistryException("registryException", "registry");
            }
            return base.GetSubKeyNames();
        }

        /// <summary>
        /// Returns the sub key with name "name" as a read only key
        /// </summary>
        public override RegistryKeyWrapper OpenSubKey(string name)
        {
            if (_whereToThrow == WhereToThrow.OpenSubKey)
            {
                throw new RegistryException("registryException", "registry");
            }

            if (_subKeyThatDoesNotExist == name)
            {
                // Return wrapper around null key
                return new MockRegistryKey((RegistryKey)null, Registry.LocalMachine);
            }

            return base.OpenSubKey(name);
        }
    }
#endif
}

