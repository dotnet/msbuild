// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Win32;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Unit tests for ToolsetConfigurationReader class
    /// </summary>
    [TestFixture]
    public class ToolsetConfigurationReaderTests
    {
        private static string msbuildToolsets = "msbuildToolsets";

        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void Teardown()
        {
            ToolsetConfigurationReaderTestHelper.CleanUp();
        }

        #region "msbuildToolsets element tests"

        /// <summary>
        ///  msbuildToolsets element is empty
        /// </summary>
        [Test]
        public void MSBuildToolsetsTest_EmptyElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets />
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;

            Assertion.AssertNotNull(msbuildToolsetSection);
            Assertion.AssertEquals(null, msbuildToolsetSection.Default);
            Assertion.AssertNotNull(msbuildToolsetSection.Toolsets);
            Assertion.AssertEquals(0, msbuildToolsetSection.Toolsets.Count);
        }

        /// <summary>
        ///  tests if ToolsetConfigurationReaderTests is successfully initialized from the config file
        /// </summary>
        [Test]
        public void MSBuildToolsetsTest_Basic()
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

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;

            Assertion.AssertEquals(msbuildToolsetSection.Default, "2.0");
            Assertion.AssertEquals(1, msbuildToolsetSection.Toolsets.Count);

            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion, "2.0");
            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count, 1);
            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value,
                                   @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\");
        }


        /// <summary>
        ///  tests if ToolsetConfigurationReaderTests is successfully initialized from the config file
        /// </summary>
        [Test]
        public void MSBuildToolsetsTest_BasicWithOtherConfigEntries()
        {
            // NOTE: for some reason, <configSections> MUST be the first element under <configuration>
            // for the API to read it. The docs don't make this clear.

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
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
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();
            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;

            Assertion.AssertEquals(msbuildToolsetSection.Default, "2.0");
            Assertion.AssertEquals(1, msbuildToolsetSection.Toolsets.Count);

            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion, "2.0");
            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count, 1);
            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value,
                                   @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\");
        }
        #endregion

        #region "toolsVersion element tests"

        #region "Invalid cases (exception is expected to be thrown)"

        /// <summary>
        /// name attribute is missing from toolset element 
        /// </summary>
        [Test]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void ToolsVersionTest_NameNotSpecified()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset>
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                     </toolset>
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        ///  More than 1 toolset element with the same name
        /// </summary>
        [Test]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void ToolsVersionTest_MultipleElementsWithSameName()
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
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        /// empty toolset element 
        /// </summary>
        [Test]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void ToolsVersionTest_EmptyElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset />
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;
        }

        #endregion

        #region "Valid cases (No exception expected)"

        /// <summary>
        /// only 1 toolset is specified
        /// </summary>
        [Test]
        public void ToolsVersionTest_SingleElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""3.5"">
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;

            Assertion.AssertEquals(msbuildToolsetSection.Default, "3.5");
            Assertion.AssertEquals(1, msbuildToolsetSection.Toolsets.Count);
            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement(0).toolsVersion, "3.5");
            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement("3.5").PropertyElements.Count, 1);
            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement("3.5").PropertyElements.GetElement("MSBuildBinPath").Value,
                                   @"D:\windows\Microsoft.NET\Framework\v3.5.x86ret\");
        }

        #endregion
        #endregion

        #region "Property"

        #region "Invalid cases (exception is expected to be thrown)"

        /// <summary>
        ///  name attribute is missing
        /// </summary>
        [Test]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void PropertyTest_NameNotSpecified()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""3.5"">
                     <toolset toolsVersion=""3.5"">
                       <property value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        /// value attribute is missing
        /// </summary>
        [Test]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void PropertyTest_ValueNotSpecified()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""3.5"">
                     <toolset name=""3.5"">
                       <property name=""MSBuildBinPath"" />
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        /// more than 1 property element with the same name
        /// </summary>
        [Test]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void PropertyTest_MultipleElementsWithSameName()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""3.5"">
                     <toolset ToolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v3.5.x86ret\""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;
        }

        /// <summary>
        ///  property element is an empty element
        /// </summary>
        [Test]
        [ExpectedException(typeof(ConfigurationErrorsException))]
        public void PropertyTest_EmptyElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""3.5"">
                     <toolset toolsVersion=""3.5"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property />
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;
        }
        #endregion

        #region "Valid cases"

        /// <summary>
        /// more than 1 property element specified
        /// </summary>
        [Test]
        public void PropertyTest_MultipleElement()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""SomeOtherPropertyName"" value=""SomeOtherPropertyValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;

            Assertion.AssertEquals(msbuildToolsetSection.Default, "2.0");
            Assertion.AssertEquals(1, msbuildToolsetSection.Toolsets.Count);
            Assertion.AssertEquals(2, msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count);

            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value,
                                   @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\");
            Assertion.AssertEquals(msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("SomeOtherPropertyName").Value,
                                   @"SomeOtherPropertyValue");
        }

        /// <summary>
        /// tests GetElement(string name) function in propertycollection class
        /// </summary>
        [Test]
        public void PropertyTest_GetValueByName()
        {
            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.BuildEngine.ToolsetConfigurationSection, Microsoft.Build.Engine"" />
                   </configSections>
                   <msbuildToolsets default=""2.0"">
                     <toolset toolsVersion=""2.0"">
                       <property name=""MSBuildBinPath"" value=""D:\windows\Microsoft.NET\Framework\v2.0.x86ret\""/>
                       <property name=""SomeOtherPropertyName"" value=""SomeOtherPropertyValue""/>
                     </toolset>
                   </msbuildToolsets>
                 </configuration>");

            Configuration config = ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest();

            ToolsetConfigurationSection msbuildToolsetSection = config.GetSection(msbuildToolsets) as ToolsetConfigurationSection;

            // Verifications
            Assertion.AssertEquals(msbuildToolsetSection.Default, "2.0");
            Assertion.AssertEquals(1, msbuildToolsetSection.Toolsets.Count);
            Assertion.AssertEquals(2, msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.Count);
            Assertion.AssertEquals(@"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\", 
                                   msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("MSBuildBinPath").Value);
            Assertion.AssertEquals(@"SomeOtherPropertyValue",
                                   msbuildToolsetSection.Toolsets.GetElement("2.0").PropertyElements.GetElement("SomeOtherPropertyName").Value);
        }

        #endregion
        #endregion
    }
}
