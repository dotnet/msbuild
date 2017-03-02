// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Win32;
using System.Text;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Unit tests for ToolsetReader class and its derived classes
    /// </summary>
    [TestFixture]
    public class ToolsetReaderTests
    {
        // The registry key that is passed as the baseKey parameter to the ToolsetRegistryReader class
        private RegistryKey testRegistryKey = null;
        // Subkey "4.0"
        private RegistryKey currentVersionRegistryKey = null;
        // Subkey "ToolsVersions"
        private RegistryKey toolsVersionsRegistryKey = null;

        // Path to the registry key under HKCU
        // Note that this is a test registry key created solely for unit testing.
        private const string testRegistryPath = @"msbuildUnitTests";

        /// <summary>
        /// Reset the testRegistryKey
        /// </summary>
        [SetUp]
        public void Setup()
        {
            TearDown();
            testRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath);
            currentVersionRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath + "\\" + Constants.AssemblyVersion);
            toolsVersionsRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath + "\\ToolsVersions");
        }

        [TearDown]
        public void TearDown()
        {
            ToolsetConfigurationReaderTestHelper.CleanUp();

            DeleteTestRegistryKey();
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

        /// <summary>
        /// Test to make sure machine.config file has the section registered
        /// and we are picking it up from there.
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_SectionNotRegisteredInConfigFile()
        {
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

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            Assertion.AssertEquals(null, defaultToolsVersion);
            Assertion.AssertEquals(0, values.Count);
        }

        #region "Reading from application configuration file tests"

        /// <summary>
        /// Tests that the data is correctly populated using function GetToolsetDataFromConfiguration
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_Basic()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            Assertion.AssertEquals("2.0", defaultToolsVersion);
            Assertion.AssertEquals(2, values.Count);
            Assertion.AssertEquals(0, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(@"D:\windows\Microsoft.NET\Framework\v2.0.x86ret", values["2.0"].ToolsPath);
            Assertion.AssertEquals(0, values["3.5"].BuildProperties.Count);
            Assertion.AssertEquals(@"D:\windows\Microsoft.NET\Framework\v3.5.x86ret", values["3.5"].ToolsPath);
        }

        /// <summary>
        /// Relative paths can be used in a config file value
        /// </summary>
        [Test]
        public void RelativePathInValue()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildToolsPath"" value=""..\foo""/>
                       <!-- derelativization occurs before comparing toolspath and binpath -->
                       <property name=""MSBuildBinPath"" value=""..\.\foo""/>
                     </toolset>
                     <toolset toolsVersion=""3.0"">
                       <!-- properties are expanded before derelativization-->
                       <property name=""MSBuildBinPath"" value=""$(DotDotSlash)bar""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("DotDotSlash", @"..\");
            reader.ReadToolsets(values, new BuildPropertyGroup(), pg, true);

            string expected1 = Path.GetFullPath(Path.Combine(FileUtilities.CurrentExecutableDirectory, @"..\foo"));
            string expected2 = Path.GetFullPath(Path.Combine(FileUtilities.CurrentExecutableDirectory, @"..\bar"));
            Console.WriteLine(values["2.0"].ToolsPath);
            Assertion.AssertEquals(expected1, values["2.0"].ToolsPath);
            Assertion.AssertEquals(expected2, values["3.0"].ToolsPath);
        }

        /// <summary>
        /// Invalid relative path in msbuildbinpath value
        /// </summary>
        [Test]
        public void InvalidRelativePath()
        {
            string invalidRelativePath = @"..\|invalid|";
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""" + invalidRelativePath + @"""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            // Don't crash (consistent with invalid absolute path)
            Assertion.AssertEquals(invalidRelativePath, values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Tests the case where application configuration file is invalid
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetToolsetDataFromConfiguration_InvalidXmlFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"<INVALIDXML>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case where application configuration file is invalid
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetToolsetDataFromConfiguration_InvalidConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolsVersion>
                     <SOMEINVALIDTAG/>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case where application configuration file is empty
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetToolsetDataFromConfiguration_FileEmpty()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case when ReadConfiguration throws exception
        /// Make sure that we don't eat it and always throw ConfigurationErrorsException
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetToolsetDataFromConfiguration_ConfigurationExceptionThrown()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"", new ConfigurationException());

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            // this should throw ...
            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case when ReadConfiguration throws exception
        /// Make sure that we don't eat it and always throw ConfigurationErrorsException
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetToolsetDataFromConfiguration_ConfigurationErrorsExceptionThrown()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"", new ConfigurationErrorsException());

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            // this should throw ...
            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case where default attribute is not specified in the config file
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_DefaultAttributeNotSpecified()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            Assertion.AssertEquals(null, defaultToolsVersion);
            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(0, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(@"D:\windows\Microsoft.NET\Framework\v2.0.x86ret", values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Default toolset has no toolsVersion element definition
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_DefaultToolsetUndefined()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""nonexistent"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            // Does not throw
            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case where msbuildToolsets is not specified in the config file
        /// Basically in the code we should be checking if config.GetSection("msbuildToolsets") returns a null
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_MSBuildToolsetsNodeNotPresent()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            Assertion.AssertEquals(null, defaultToolsVersion);
            Assertion.AssertEquals(0, values.Count);
        }

        /// <summary>
        /// Tests that we handle empty MSBuildToolsets element correctly
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_EmptyMSBuildToolsetsNode()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets/>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            Assertion.AssertEquals(null, defaultToolsVersion);
            Assertion.AssertEquals(0, values.Count);
        }

        /// <summary>
        /// Tests the case where only default ToolsVersion is specified in the application configuration file
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_OnlyDefaultSpecified()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0""/>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case where only one ToolsVersion data is specified in the application configuration file
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_OneToolsVersionNode()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            Assertion.AssertEquals("2.0", defaultToolsVersion);
            Assertion.AssertEquals(@"D:\windows\Microsoft.NET\Framework\v2.0.x86ret", values["2.0"].ToolsPath);
            Assertion.AssertEquals(1, values.Count);
        }

        /// <summary>
        /// Tests the case when an invalid value of ToolsVersion is specified
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetToolsetDataFromConfiguration_ToolsVersionIsEmptyString()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion="""">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            //this should throw ...
            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// If both MSBuildToolsPath and MSBuildBinPath are present, they must match
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_ToolsPathAndBinPathDiffer()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""MSBuildToolsPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            //this should throw ...
            bool caught = false;
            try
            {
                reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
            }
            catch (InvalidToolsetDefinitionException)
            {
                caught = true;
            }
            Assertion.Assert(caught);
        }

        /// <summary>
        /// Tests the case when a blank value of PropertyName is specified in the config file
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void BlankPropertyNameInConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name="""" value=""foo""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            //this should throw ...
            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case when a blank property name is specified in the registry
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void BlankPropertyNameInRegistry()
        {
            RegistryKey rk = toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "someBinPath");
            rk.SetValue("", "foo");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            // Should throw ...
            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );
        }

        /// <summary>
        /// Tests the case when a blank property value is specified in the config file
        /// </summary>
        [Test]
        public void BlankPropertyValueInConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""foo"" value=""""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            //this should not throw ...
            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case when a blank property value is specified in the registry
        /// </summary>
        [Test]
        public void BlankPropertyValueInRegistry()
        {
            RegistryKey rk = toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "someBinPath");
            rk.SetValue("foo", "");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            // Should not throw ...
            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );
        }

        /// <summary>
        /// Tests the case when an invalid value of PropertyName is specified in the config file
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void InvalidPropertyNameInConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""&amp;"" value=""foo""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            //this should throw ...
            reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);
        }

        /// <summary>
        /// Tests the case when an invalid value of PropertyName is specified in the registry
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void InvalidPropertyNameInRegistry()
        {
            RegistryKey rk = toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "someBinPath");
            rk.SetValue("foo|bar", "x");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            // Should throw ...
            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );
        }

        /// <summary>
        /// Tests that empty string is an invalid value for MSBuildBinPath
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetToolsetDataFromConfiguration_PropertyValueIsEmptyString1()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(0, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(String.Empty, values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Tests that empty string is a valid property value for an arbitrary property
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_PropertyValueIsEmptyString2()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildToolsPath"" value=""xxx""/>
                       <property name=""foo"" value=""""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(1, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(String.Empty, values["2.0"].BuildProperties["foo"].Value);
        }

        /// <summary>
        /// Tests that any escaped xml in config file, is treated well
        /// Note that this comes for free with the current implementation using the 
        /// framework api to access section in the config file
        /// </summary>
        [Test]
        public void GetToolsetDataFromConfiguration_XmlEscapedCharacters()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2&gt;.0"">
                     <toolset toolsVersion=""2&gt;.0"">
                       <property name=""MSBuildBinPath"" value=""x""/>
                       <property name=""foo"" value=""some&gt;value""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetReader reader = new ToolsetConfigurationReader(new ReadApplicationConfiguration(
                ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), true);

            Assertion.AssertEquals("2>.0", defaultToolsVersion);
            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(@"some>value", values["2>.0"].BuildProperties["foo"].Value);
        }
        #endregion

        #region "GetToolsetData tests"

        /// <summary>
        /// Tests the case where registry and config file contains different toolsVersion
        /// </summary>
        [Test]
        public void GetToolsetData_NoConflict()
        {
            // Set up registry with two tools versions and one property each
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            RegistryKey key2 = toolsVersionsRegistryKey.CreateSubKey("3.5");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""4.5"">
                     <toolset toolsVersion=""4.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            // Verifications
            Assertion.AssertEquals(4, values.Count);
            Assertion.AssertEquals("4.5", defaultToolsVersion);
            Assertion.AssertEquals(@"D:\somepath", values["2.0"].ToolsPath);
            Assertion.AssertEquals(@"D:\somepath2", values["3.5"].ToolsPath);
            Assertion.AssertEquals(@"D:\windows\Microsoft.NET\Framework\v2.0.x86ret", values["4.5"].ToolsPath);
            Assertion.AssertEquals(@"D:\windows\Microsoft.NET\Framework\v3.5.x86ret", values["5.0"].ToolsPath);
        }

        /// <summary>
        /// Tests that ToolsetInitialization are respected.
        /// </summary>
        [Test]
        public void ToolsetInitializationFlagsSetToNone()
        {
            // Set up registry with two tools versions and one property each
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            RegistryKey key2 = testRegistryKey.CreateSubKey("3.5");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""4.5"">
                     <toolset toolsVersion=""4.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.None
                                                       );

            // Verifications
            Assertion.AssertEquals(1, values.Count);

            string expectedDefault = "2.0";
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 == null)
            {
                expectedDefault = "4.0";
            }

            Assertion.AssertEquals(expectedDefault, defaultToolsVersion);
        }

        /// <summary>
        /// Tests that ToolsetInitialization are respected.
        /// </summary>
        [Test]
        public void ToolsetInitializationFlagsSetToRegistry()
        {
            // Set up registry with two tools versions and one property each
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            RegistryKey key2 = toolsVersionsRegistryKey.CreateSubKey("3.5");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""4.5"">
                     <toolset toolsVersion=""4.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );

            // Verifications
            Assertion.AssertEquals(2, values.Count);
            Assertion.AssertEquals("2.0", defaultToolsVersion);
            Assertion.AssertEquals(@"D:\somepath", values["2.0"].ToolsPath);
            Assertion.AssertEquals(@"D:\somepath2", values["3.5"].ToolsPath);
        }

        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void ThrowOnNonStringRegistryValueTypes()
        {
            RegistryKey rk = toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "someBinPath");

            // Non-string
            rk.SetValue("QuadWordValue", 42, RegistryValueKind.QWord);

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            // Should throw ...
            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );
        }

        [Test]
        public void PropertiesInRegistryCannotReferToOtherPropertiesInRegistry()
        {
            RegistryKey rk = toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "c:\\x$(p1)");
            rk.SetValue("p0", "$(p1)");
            rk.SetValue("p1", "v");
            rk.SetValue("p2", "$(p1)");
            rk.SetValue("MSBuildToolsPath", "c:\\x$(p1)");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals("", values["2.0"].BuildProperties["p0"].Value);
            Assertion.AssertEquals("v", values["2.0"].BuildProperties["p1"].Value);
            Assertion.AssertEquals("", values["2.0"].BuildProperties["p2"].Value);
            Assertion.AssertEquals("c:\\x", values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Tests that ToolsetInitialization are respected.
        /// </summary>
        [Test]
        public void ToolsetInitializationFlagsSetToConfigurationFile()
        {
            // Set up registry with two tools versions and one property each
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            RegistryKey key2 = toolsVersionsRegistryKey.CreateSubKey("3.5");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""4.5"">
                     <toolset toolsVersion=""4.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile
                                                       );

            // Verifications
            Assertion.AssertEquals(2, values.Count);
            Assertion.AssertEquals("4.5", defaultToolsVersion);
            Assertion.AssertEquals(@"D:\windows\Microsoft.NET\Framework\v2.0.x86ret", values["4.5"].ToolsPath);
            Assertion.AssertEquals(@"D:\windows\Microsoft.NET\Framework\v3.5.x86ret", values["5.0"].ToolsPath);
        }

        /// <summary>
        /// Properties in the configuration file may refer to a registry location by using the syntax for example
        /// "$(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)", where "HKEY_LOCAL_MACHINE\Software\Vendor\Tools" is the key and
        /// "TaskLocation" is the name of the value.  The name of the value and the preceding "@" may be omitted if
        /// the default value is desired.
        /// </summary>
        [Test]
        public void PropertyInConfigurationFileReferencesRegistryLocation()
        {
            // Registry Read
            RegistryKey key1 = Registry.CurrentUser.CreateSubKey(@"Software\Vendor\Tools");
            key1.SetValue("TaskLocation", @"somePathToTasks");
            key1.SetValue("TargetsLocation", @"D:\somePathToTargets");
            key1.SetValue("SchemaLocation", @"Schemas");
            key1.SetValue(null, @"D:\somePathToDefault");  //this sets the default value for this key

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation)""/>
                       <property name=""p1"" value=""$(p2)$(REGISTRY:HKEY_CURRENT_USER\Software\Vendor\Tools)""/>
                       <property name=""p2"" value=""$(p1)\$(Registry:hkey_current_user\Software\Vendor\Tools@TaskLocation)\$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@SchemaLocation)\2.0""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(@"D:\somePathToTasks", values["2.0"].ToolsPath);
            Assertion.AssertEquals(2, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(@"D:\somePathToDefault", values["2.0"].BuildProperties["p1"].Value);
            Assertion.AssertEquals(@"D:\somePathToDefault\somePathToTasks\Schemas\2.0", values["2.0"].BuildProperties["p2"].Value);

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Vendor");
        }

        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void ToolsPathInRegistryHasInvalidPathChars()
        {
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\some\foo|bar\path\");
            
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            // should throw... 
            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           null,
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );
        }

        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void SamePropertyDefinedMultipleTimesForSingleToolsVersionInConfigurationFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
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

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           null,
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile
                                                       );
        }

        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void SamePropertyDifferentCaseDefinedMultipleTimesForSingleToolsVersionInConfigurationFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
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

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           null,
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(), 
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile
                                                       );
        }

        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void SameToolsVersionDefinedMultipleTimesInConfigurationFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                     </toolset>
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\folder""/>
                       <property name=""p2"" value=""anotherValue""/>
                     </toolset>
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           null,
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile
                                                       );
        }

        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void SameToolsVersionDifferentCaseDefinedMultipleTimesInConfigurationFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""DevDiv"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                     </toolset>
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\folder""/>
                       <property name=""p2"" value=""anotherValue""/>
                     </toolset>
                     <toolset toolsVersion=""DEVDIV"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           null,
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile
                                                       );
        }
        
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void CannotSetReservedPropertyInConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\some\folder\on\disk""/>
                       <property name=""MSBuildProjectFile"" value=""newValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           null,
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile
                                                       );
        }

        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void CannotSetReservedPropertyInRegistry()
        {
            // Registry Read
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            key1.SetValue("MSBuildProjectFile", @"SomeRegistryValue");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           null,
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.Registry
                                                       );
        }
        
        /// <summary>
        /// Properties defined in previously processed toolset definitions should
        /// not affect the evaluation of subsequent toolset definitions.
        /// </summary>
        [Test]
        public void NoInterferenceBetweenToolsetDefinitions()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\20\some\folder\on\disk""/>
                       <property name=""p1"" value=""another""/>
                       <property name=""p4"" value=""fourth$(p3)Value"" />
                     </toolset>
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\35\some\folder\on\disk""/>
                       <property name=""p2"" value=""some$(p1)value""/>
                       <property name=""p3"" value=""propertyValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           null,
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile
                                                       );

            Assertion.AssertEquals(2, values.Count);
            
            Assertion.AssertEquals(@"D:\20\some\folder\on\disk", values["2.0"].ToolsPath);
            Assertion.AssertEquals(2, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(@"another", values["2.0"].BuildProperties["p1"].Value);
            Assertion.AssertEquals(@"fourthValue", values["2.0"].BuildProperties["p4"].Value);

            Assertion.AssertEquals(@"D:\35\some\folder\on\disk", values["3.5"].ToolsPath);
            Assertion.AssertEquals(2, values["3.5"].BuildProperties.Count);
            Assertion.AssertEquals(@"somevalue", values["3.5"].BuildProperties["p2"].Value);
            Assertion.AssertEquals(@"propertyValue", values["3.5"].BuildProperties["p3"].Value);
        }

        /// <summary>
        /// Properties in the configuration file may refer to a registry location by using the syntax for example
        /// "$(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)", where "HKEY_LOCAL_MACHINE\Software\Vendor\Tools" is the key and
        /// "TaskLocation" is the name of the value.  The name of the value and the preceding "@" may be omitted if
        /// the default value is desired.
        /// </summary>

        [Test]
        public void ConfigFileInvalidRegistryExpression1()
        {
            // No location
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:)""/>");
        }

        [Test]
        public void ConfigFileInvalidRegistryExpression2()
        {
            // Bogus key expression
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:__bogus__)""/>");
        }

        [Test]
        public void ConfigFileInvalidRegistryExpression3()
        {
            // No registry location just @
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:@)""/>");
        }

        [Test]
        public void ConfigFileInvalidRegistryExpression4()
        {
            // Double @
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@@TaskLocation)""/>");
        }

        [Test]
        public void ConfigFileInvalidRegistryExpression5()
        {
            // Trailing @
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation@)""/>");
        }

        [Test]
        public void ConfigFileInvalidRegistryExpression6()
        {
            // Leading @
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:@HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation)""/>");
        }

        [Test]
        public void ConfigFileInvalidRegistryExpression7()
        {
            // Bogus hive
            ConfigFileInvalidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:BOGUS_HIVE\Software\Vendor\Tools@TaskLocation)""/>");
        }

        [Test]
        public void ConfigFileStringEmptyRegistryExpression1()
        {
            // Regular undefined property beginning with "Registry"
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry)""/>",
                                          String.Empty);
        }

        [Test]
        public void ConfigFileStringEmptyRegistryExpression2()
        {
            // Nonexistent key
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:HKEY_CURRENT_USER\Nonexistent_Key\Software\Vendor\Tools@TaskLocation)""/>",
                                          String.Empty);
        }

        [Test]
        public void ConfigFileNonPropertyRegistryExpression1()
        {
            // Property not terminated with paren, does not look like property
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation""/>",
                                          @"$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation");
        }

        [Test]
        public void ConfigFileNonPropertyRegistryExpression2()
        {
            // Missing colon, looks like regular property (admittedly with invalid property name chars, but we don't
            // error for that today)
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""$(RegistryHKEY_CURRENT_USER\Software\Vendor\Tools@@TaskLocation)""/>",
                                          String.Empty);
        }

        [Test]
        public void ConfigFileItemExpressionsDoNotExpandInConfigurationProperties()
        {
            // Expect that item expressions such as '@(SomeItem)' are not evaluated in any way, e.g., they are treated literally
            ConfigFileValidRegistryExpressionHelper(@"<property name=""p"" value=""@(SomeItem)""/>",
                                          @"@(SomeItem)");
        }

        [Test]
        public void RegistryInvalidRegistryExpression1()
        {
            // Bogus key expression
            RegistryInvalidRegistryExpressionHelper("$(Registry:__bogus__)");
        }

        [Test]
        public void RegistryValidRegistryExpression1()
        {
            // Regular undefined property beginning with "Registry"
            RegistryValidRegistryExpressionHelper("$(Registry)", String.Empty);
        }

        [Test]
        public void RegistryValidRegistryExpressionRecursive()
        {
            // Property pointing to itself - should not hang :-)
            RegistryValidRegistryExpressionHelper
                (@"$(Registry:HKEY_CURRENT_USER\" + testRegistryPath + @"\ToolsVersions\2.0@p)", 
                 @"$(Registry:HKEY_CURRENT_USER\" + testRegistryPath + @"\ToolsVersions\2.0@p)");
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

            Assertion.AssertEquals(true, caught);
        }

        private void RegistryValidRegistryExpressionHelper(string propertyExpression, string expectedValue)
        {
            // Registry Read
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", "xxxx");
            key1.SetValue("p", propertyExpression);

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            ToolsetReader.ReadAllToolsets
                                       (
                                           values,
                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                           new BuildPropertyGroup(),
                                           new BuildPropertyGroup(),
                                           ToolsetDefinitionLocations.Registry
                                       );

            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(expectedValue, values["2.0"].BuildProperties["p"].Value);
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

            Assertion.AssertEquals(true, caught);
        }

        /// <summary>
        /// Tests that a specified registry property expression evaluates to specified value
        /// </summary>
        /// <param name="propertyExpression"></param>
        private void ConfigFileValidRegistryExpressionHelper(string propertyExpression, string expectedValue)
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""xxxx""/>
                       " + propertyExpression + @"
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));


            ToolsetReader.ReadAllToolsets
                                       (
                                           values,
                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                           new BuildPropertyGroup(),
                                           new BuildPropertyGroup(),
                                           ToolsetDefinitionLocations.ConfigurationFile
                                       );

            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(expectedValue, values["2.0"].BuildProperties["p"].Value);
        }

        /// <summary>
        /// Tests the case where application configuration file overrides a value already specified in the registry
        /// </summary>
        [Test]
        public void GetToolsetData_ConflictingPropertyValuesSameCase()
        {
            // Registry Read
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\somedifferentpath""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(0, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(@"D:\somedifferentpath", values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Tests when properties are defined in the registry as
        /// well as in the config file for the same tools version.
        /// We should not merge them; we should take the config file ones only
        /// </summary>
        [Test]
        public void GetToolsetData_NoMerging()
        {
            // Registry Read
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            key1.SetValue("SomeRegistryProperty", @"SomeRegistryValue");

            // Set the config file contents as needed
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\someotherpath""/>
                       <property name=""SomeConfigProperty"" value=""SomeConfigValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(1, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(@"D:\someotherpath", values["2.0"].ToolsPath);
            Assertion.AssertEquals(null, values["2.0"].BuildProperties["SomeRegistryProperty"]); // Was zapped
            Assertion.AssertEquals(@"SomeConfigValue", values["2.0"].BuildProperties["SomeConfigProperty"].Value);
        }

        /// <summary>
        /// The absence of the ToolsVersion attribute on the main Project element in a project file means
        /// that the engine's default tools version should be used.
        /// </summary>
        [Test]
        public void ToolsVersionAttributeNotSpecifiedOnProjectElementAndDefaultVersionSpecifiedInRegistry()
        {
            Engine e = new Engine();
            e.AddToolset(new Toolset("2.0", "20toolsPath"));
            e.AddToolset(new Toolset("4.0", "40toolsPath"));

            string projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("x.proj", @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />");

            Project project = e.CreateNewProject();
            project.Load(projectPath);

            Assertion.AssertEquals("2.0", project.ToolsVersion);
            Assertion.AssertEquals("2.0", project.DefaultToolsVersion);
        }

        /// <summary>
        /// Tests the case when no values are specified in the registry
        /// </summary>
        [Test]
        public void GetToolsetData_RegistryNotPresent()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBUILDBINPATH"" value=""D:\somedifferentpath""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(0, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(@"D:\somedifferentpath", values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Test the case where nothing is specified in the config file
        /// Note that config file not present is same as config file 
        /// with no MSBuildToolsets Section 
        /// </summary>
        [Test]
        public void GetToolsetData_ConfigFileNotPresent()
        {
            // Registry Read
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(0, values["2.0"].BuildProperties.Count);
            Assertion.AssertEquals(@"D:\somepath", values["2.0"].ToolsPath);
        }

        /// <summary>
        /// Tests the case where nothing is specified in registry and config file
        /// </summary>
        [Test]
        public void GetToolsetData_RegistryAndConfigNotPresent()
        {
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals(1 /* fallback */, values.Count);
        }

        /// <summary>
        /// Tests the case when reading config file throws an exception
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetToolsetData_ReadConfigThrowsException()
        {
            // Registry Read
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");

            // Set the config helper to throw exception
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"", new ConfigurationException());

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            ToolsetReader.ReadAllToolsets
                       (
                           values,
                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                           new BuildPropertyGroup(),
                           new BuildPropertyGroup(),
                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                       );
        }

        /// <summary>
        /// Tests the case where reading from registry throws exception
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetToolsetData_ReadRegistryOpenSubKeyThrowsException()
        {
            RegistryKeyWrapper mockRegistryKey =
                new MockRegistryKey(testRegistryPath, MockRegistryKey.WhereToThrow.OpenSubKey);

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\somedifferentpath""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(mockRegistryKey),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );
        }

        #endregion

        #region "SetDefaultToolsetVersion tests"

        /// <summary>
        /// Tests that the default ToolsVersion is correctly resolved when specified
        /// in registry and config file
        /// </summary>
        [Test]
        public void SetDefaultToolsetVersion_SpecifiedInRegistryAndConfigFile()
        {
            // Set up registry with two tools versions and one property each
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            RegistryKey key2 = toolsVersionsRegistryKey.CreateSubKey("3.5");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""5.0"">
                     <toolset toolsVersion=""4.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals("5.0", defaultToolsVersion);
        }

        /// <summary>
        /// Tests that the default ToolsVersion is correctly resolved when specified in registry only
        /// </summary>
        [Test]
        public void SetDefaultToolsetVersion_SpecifiedOnlyInRegistry()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""3.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            // Set up registry with two tools versions and one property each
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", "3.5");
            RegistryKey key2 = toolsVersionsRegistryKey.CreateSubKey("3.5");
            key2.SetValue("MSBuildBinPath", @"D:\somepath2");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals("3.5", defaultToolsVersion);
        }

        /// <summary>
        /// Tests that the default ToolsVersion is correctly resolved
        /// when specified in config file only
        /// </summary>
        [Test]
        public void SetDefaultToolsetVersion_SpecifiedOnlyInConfigFile()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""5.0"">
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );


            Assertion.AssertEquals("5.0", defaultToolsVersion);
        }

        /// <summary>
        /// Tests that the default ToolsVersion is correctly resolved when specified nowhere
        /// </summary>
        [Test]
        public void SetDefaultToolsetVersion_SpecifiedNowhere()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""5.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           new BuildPropertyGroup(),
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            string expectedDefault = "2.0";
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 == null)
            {
                expectedDefault = "4.0";
            }

            Assertion.AssertEquals(expectedDefault, defaultToolsVersion); // built-in default
        }

        /// <summary>
        /// Tests that properties are properly expanded when reading them from the config file
        /// </summary>
        [Test]
        public void PropertiesInToolsetsFromConfigFileAreExpanded()
        {
            // $(COMPUTERNAME) is just a convenient env var. $(NUMBER_OF_PROCESSORS) isn't defined on Longhorn
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""p1"" value=""v1$(p2)""/>
                       <property name=""p2"" value=""__$(p1)__""/>
                       <property name=""p3"" value=""$(COMPUTERNAME)""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            Engine e = new Engine();
            BuildPropertyGroup initialProperties = new BuildPropertyGroup();
            initialProperties.ImportProperties(e.EnvironmentProperties);
            initialProperties.ImportProperties(e.GlobalProperties);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           initialProperties,
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals("v1", values["3.5"].BuildProperties["p1"].Value);
            // Properties can refer to other properties also defined in the config file
            Assertion.AssertEquals("__v1__", values["3.5"].BuildProperties["p2"].Value);
            Assertion.AssertEquals(Environment.MachineName, values["3.5"].BuildProperties["p3"].Value);
        }

        /// <summary>
        /// Tests that properties in MSBuildToolsPath are properly expanded when reading them from the config file
        /// </summary>
        [Test]
        public void PropertiesInToolsetsFromConfigFileAreExpandedInToolsPath()
        {
            // $(COMPUTERNAME) is just a convenient env var. $(NUMBER_OF_PROCESSORS) isn't defined on Longhorn
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""3.5"">
                       <property name=""p1"" value=""Microsoft.NET""/>
                       <property name=""p2"" value=""windows""/>
                       <property name=""MSBuildBinPath"" value=""D:\windows\$(p1)\Framework\v2.0.x86ret\$(COMPUTERNAME)""/>
                       <property name=""MSBuildToolsPath"" value=""D:\$(p2)\$(p1)\Framework\v2.0.x86ret\$(COMPUTERNAME)""/>
                       <property name=""p3"" value=""v3$(MSBuildToolsPath)""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            Engine e = new Engine();
            BuildPropertyGroup initialProperties = new BuildPropertyGroup();
            initialProperties.ImportProperties(e.EnvironmentProperties);
            initialProperties.ImportProperties(e.GlobalProperties);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           new BuildPropertyGroup(),
                                                           initialProperties,
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals("Microsoft.NET", values["3.5"].BuildProperties["p1"].Value);
            Assertion.AssertEquals("windows", values["3.5"].BuildProperties["p2"].Value);
            string expectedToolsPath = @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\" + Environment.MachineName;
            Assertion.AssertEquals(expectedToolsPath, values["3.5"].ToolsPath);
            Assertion.AssertEquals("v3" + expectedToolsPath, values["3.5"].BuildProperties["p3"].Value);
        }

        /// <summary>
        /// Global properties are available, but they cannot be overwritten by other toolset properties, just as they cannot
        /// be overwritten by project file properties.
        /// </summary>
        [Test]
        public void GlobalPropertiesInToolsetsAreExpandedButAreNotOverwritten()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets>
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""p1"" value=""$(gp1)""/>
                       <property name=""gp1"" value=""v2""/>
                       <property name=""p2"" value=""$(gp1)""/>
                     </toolset>
                   </msbuildToolsets> 
                 </configuration>");

            Engine e = new Engine(ToolsetDefinitionLocations.None);
            ToolsetCollection values = new ToolsetCollection(e);

            BuildPropertyGroup globalProperties = e.GlobalProperties;
            globalProperties.SetProperty("gp1", "gv1");
            
            BuildPropertyGroup initialProperties = new BuildPropertyGroup();
            initialProperties.ImportProperties(e.EnvironmentProperties);
            initialProperties.ImportProperties(globalProperties);

            string defaultToolsVersion = ToolsetReader.ReadAllToolsets
                                                       (
                                                           values,
                                                           new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath)),
                                                           new ToolsetConfigurationReader(new ReadApplicationConfiguration(ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest)),
                                                           globalProperties,
                                                           initialProperties,
                                                           ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry
                                                       );

            Assertion.AssertEquals("gv1", values["3.5"].BuildProperties["p1"].Value);
            Assertion.AssertEquals("gv1", values["3.5"].BuildProperties["p2"].Value);
        }


        #endregion
    }

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

        private WhereToThrow whereToThrow = WhereToThrow.None;
        private string subKeyThatDoesNotExist = null;

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
            this.whereToThrow = whereToThrow;
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
            this.subKeyThatDoesNotExist = subKeyThatDoesNotExist;
        }

        /// <summary>
        /// Name of the registry key
        /// </summary>
        public override string Name
        {
            get
            {
                if (whereToThrow == WhereToThrow.Name)
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
            if (whereToThrow == WhereToThrow.GetValue)
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
            if (whereToThrow == WhereToThrow.GetValueNames)
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
            if (whereToThrow == WhereToThrow.GetSubKeyNames)
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
            if (whereToThrow == WhereToThrow.OpenSubKey)
            {
                throw new RegistryException("registryException", "registry");
            }

            if (subKeyThatDoesNotExist == name)
            {
                // Return wrapper around null key
                return new MockRegistryKey((RegistryKey)null, Registry.LocalMachine);
            }

            return base.OpenSubKey(name);
        }

    }
}

