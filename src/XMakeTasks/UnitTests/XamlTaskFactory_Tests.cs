// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using System.Xml;
using System.IO;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Linq;
using Microsoft.CSharp;
using System.Reflection;
using Microsoft.Build.Shared;
using System.Globalization;
using Microsoft.Build.Tasks.Xaml;
using System.Xaml;

namespace Microsoft.Build.UnitTests.XamlTaskFactory_Tests
{
    #region Tests for Load and Parse methods
    /// <summary>
    /// The text fixture to unit test the task generator.
    /// Creates a new TaskGenerator object and tests the various methods
    /// </summary>
    [TestClass]
    public sealed class LoadAndParseTests
    {
        /// <summary>
        /// Tests the load method. Expects true to be returned.
        /// </summary>
        [TestMethod]
        public void TestLoadXml()
        {
            TaskParser tp = new TaskParser();
            string s = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                           <Rule Name=`TaskGeneratorLoadTest`>
                             <BoolProperty Name=`TestProperty1` Switch=`tp` />
                           </Rule>
                         </ProjectSchemaDefinitions>";
            Assert.IsTrue(tp.Parse(s.Replace("`", "\""), "TaskGeneratorLoadTest"), "File failed to load correctly.");
        }

        /// <summary>
        /// Tests the TaskName property. 
        /// Should get "CL" back for this specific case.
        /// </summary>
        [TestMethod]
        public void TestGetTaskName()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.IsTrue(tp.GeneratedTaskName.Equals("CL"), "Was expecting task name to be CL, but was " + tp.GeneratedTaskName);
        }

        /// <summary>
        /// Tests the BaseClass property. XamlTaskFactory does not currently support setting the BaseClass.
        /// </summary>
        [TestMethod]
        [Ignore] // "Should probably translate XamlObjectWriterException for this case into XamlParseException, but I want to minimize the code changes in this initial unit test checkin"
        public void TestGetBaseClass()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL` BaseClass=`mybase.class`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.AreEqual("DataDrivenToolTask", tp.BaseClass);
        }

        /// <summary>
        /// Tests the ResourceNamespace property. XamlTaskFactory does not currently support setting the ResourceNamespace.
        /// </summary>
        [TestMethod]
        [Ignore] // "Should probably translate XamlObjectWriterException for this case into XamlParseException, but I want to minimize the code changes in this initial unit test checkin"
        public void TestGetResourceNamespace()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL` ResourceNamespace=`Microsoft.Build.NativeTasks.Strings`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.AreEqual(null, tp.ResourceNamespace);
        }

        /// <summary>
        /// Tests the Namespace property. XamlTaskFactory does not currently support setting the Namespace.
        /// </summary>
        [TestMethod]
        [Ignore] // "Should probably translate XamlObjectWriterException for this case into XamlParseException, but I want to minimize the code changes in this initial unit test checkin"
        public void TestGetNamespace()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL` Namespace=`mynamespace`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.AreEqual("XamlTaskNamespace", tp.Namespace);
        }

        /// <summary>
        /// See what happens when the name is missing from the task element
        /// </summary>
        [TestMethod]
        public void TestParseIncorrect_NoName()
        {
            bool exceptionCaught = false;

            try
            {
                string incorrectXmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                           <Rule>
                             <BoolProperty Name=`TestProperty1` Switch=`tp` />
                           </Rule>
                         </ProjectSchemaDefinitions>";
                TaskParser tp = XamlTestHelpers.LoadAndParse(incorrectXmlContents, "CL");
            }
            catch (XamlParseException)
            {
                exceptionCaught = true;
            }

            Assert.IsTrue(exceptionCaught, "Should have caught a XamlParseException");
        }

        /// <summary>
        /// See what happens when the task element is valid, but we're searching for a different rule that's not in the file.
        /// </summary>
        [TestMethod]
        public void TestParseIncorrect_NoMatchingRule()
        {
            bool exceptionCaught = false;

            try
            {
                string incorrectXmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                           <Rule Name=`TaskGeneratorLoadTest`>
                             <BoolProperty Name=`TestProperty1` Switch=`tp` />
                           </Rule>
                         </ProjectSchemaDefinitions>";
                TaskParser tp = XamlTestHelpers.LoadAndParse(incorrectXmlContents, "CL");
            }
            catch (XamlParseException)
            {
                exceptionCaught = true;
            }

            Assert.IsTrue(exceptionCaught, "Should have caught a XamlParseException");
        }

        /// <summary>
        /// Basic test of several reversible boolean switches, to verify that everything gets passed through correctly.
        /// </summary>
        [TestMethod]
        public void TestBasicReversibleBooleanSwitches()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`GlobalOptimizations` Switch=`Og` ReverseSwitch=`Og-` />
                                       <BoolProperty Name=`IntrinsicFunctions` Switch=`Oi` ReverseSwitch=`Oi:NO` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.AreEqual(2, properties.Count, "Expected two properties but there were " + properties.Count);
            Assert.IsNotNull(properties.First.Value, "GlobalOptimizations switch should exist");
            Assert.AreEqual("GlobalOptimizations", properties.First.Value.Name);
            Assert.AreEqual("Og", properties.First.Value.SwitchName);
            Assert.AreEqual("Og-", properties.First.Value.ReverseSwitchName);
            Assert.AreEqual("true", properties.First.Value.Reversible, "Switch should be marked as reversible");

            properties.RemoveFirst();

            Assert.IsNotNull(properties.First.Value, "IntrinsicFunctions switch should exist");
            Assert.AreEqual("IntrinsicFunctions", properties.First.Value.Name);
            Assert.AreEqual("Oi", properties.First.Value.SwitchName);
            Assert.AreEqual("Oi:NO", properties.First.Value.ReverseSwitchName);
            Assert.AreEqual("true", properties.First.Value.Reversible, "Switch should be marked as reversible");
            Assert.AreEqual(PropertyType.Boolean, properties.First.Value.Type);
        }

        /// <summary>
        /// Tests a basic non-reversible booleans switch
        /// </summary>
        [TestMethod]
        public void TestBasicNonReversibleBooleanSwitch()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`KeepComments` Switch=`C` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.AreEqual(1, properties.Count, "Expected one property but there were " + properties.Count);
            Assert.IsNotNull(properties.First.Value, "KeepComments switch should exist");
            Assert.AreEqual("KeepComments", properties.First.Value.Name);
            Assert.AreEqual("C", properties.First.Value.SwitchName);
            Assert.IsNull(properties.First.Value.ReverseSwitchName, "KeepComments shouldn't have a reverse switch value");
            Assert.AreEqual(String.Empty, properties.First.Value.Reversible, "Switch should NOT marked as reversible");
            Assert.AreEqual(String.Empty, properties.First.Value.DefaultValue, "Switch should NOT have a default value");
            Assert.AreEqual(PropertyType.Boolean, properties.First.Value.Type);
        }

        /// <summary>
        /// Tests a basic non-reversible booleans switch that has a default value set. 
        /// </summary>
        [TestMethod]
        public void TestBasicNonReversibleBooleanSwitch_WithDefault()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`SuppressStartupBanner` Switch=`nologo` Default=`true` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.AreEqual(1, properties.Count, "Expected one property but there were " + properties.Count);
            Assert.IsNotNull(properties.First.Value, "SuppressStartupBanner switch should exist");
            Assert.AreEqual("SuppressStartupBanner", properties.First.Value.Name);
            Assert.AreEqual("nologo", properties.First.Value.SwitchName);
            Assert.IsNull(properties.First.Value.ReverseSwitchName, "SuppressStartupBanner shouldn't have a reverse switch value");
            Assert.AreEqual(String.Empty, properties.First.Value.Reversible, "Switch should NOT be marked as reversible");
            Assert.AreEqual("true", properties.First.Value.DefaultValue, "Switch should default to true");
            Assert.AreEqual(PropertyType.Boolean, properties.First.Value.Type);
        }

        /// <summary>
        /// Test for a basic string property switch
        /// </summary>
        [TestMethod]
        public void TestBasicEnumProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <EnumProperty Name=`GeneratePreprocessedFile` Switch=`nologo`>
                                         <EnumValue Name=`Disabled` />
                                         <EnumValue Name=`Yes` Switch=`P` />
                                         <EnumValue Name=`NoLineNumbers` Switch=`EP` />
                                       </EnumProperty>
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.AreEqual(1, properties.Count, "Expected one property but there were " + properties.Count);
            Assert.IsNotNull(properties.First.Value, "GeneratePreprocessedFile switch should exist");
            Assert.AreEqual("GeneratePreprocessedFile", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.String, properties.First.Value.Type); // Enum properties are represented as string types
            Assert.AreEqual(3, properties.First.Value.Values.Count, "GeneratePreprocessedFile should have three values");
        }

        /// <summary>
        /// Tests XamlTaskFactory support for DynamicEnumProperties.  These are primarily of use as a visualization in the property pages; as far as the 
        /// XamlTaskFactory and XamlDataDrivenToolTask are concerned, they are treated as StringProperties.  
        /// </summary>
        [TestMethod]
        public void TestDynamicEnumProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <DynamicEnumProperty Name=`CLBeforeTargets` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.AreEqual(1, properties.Count, "Expected one property but there were " + properties.Count);
            Assert.IsNotNull(properties.First.Value, "CLBeforeTargets switch should exist");
            Assert.AreEqual("CLBeforeTargets", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.String, properties.First.Value.Type); // Enum properties are represented as string types
        }

        /// <summary>
        /// Tests a simple string property. 
        /// </summary>
        [TestMethod]
        public void TestBasicStringProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.AreEqual(1, properties.Count, "Expected one property but there were " + properties.Count);
            Assert.IsNotNull(properties.First.Value, "TargetAssembly switch should exist");
            Assert.AreEqual("TargetAssembly", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.String, properties.First.Value.Type);
            Assert.AreEqual("/target:\"[value]\"", properties.First.Value.SwitchName);
        }

        /// <summary>
        /// Tests a simple string array property. 
        /// </summary>
        [TestMethod]
        public void TestBasicStringArrayProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringListProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` Separator=`;` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.AreEqual(1, properties.Count, "Expected one property but there were " + properties.Count);
            Assert.IsNotNull(properties.First.Value, "TargetAssembly switch should exist");
            Assert.AreEqual("TargetAssembly", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.StringArray, properties.First.Value.Type);
            Assert.AreEqual("/target:\"[value]\"", properties.First.Value.SwitchName);
            Assert.AreEqual(";", properties.First.Value.Separator);
        }

        /// <summary>
        /// Tests a simple string array property. 
        /// </summary>
        [TestMethod]
        public void TestStringArrayPropertyWithDataSource()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringListProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` Separator=`;`>
                                         <StringListProperty.DataSource>
                                           <DataSource SourceType=`Property` Persistence=`ProjectFile` ItemType=`AssemblyName` />
                                         </StringListProperty.DataSource>
                                       </StringListProperty>
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.AreEqual(1, properties.Count, "Expected one property but there were " + properties.Count);
            Assert.IsNotNull(properties.First.Value, "TargetAssembly switch should exist");
            Assert.AreEqual("TargetAssembly", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.StringArray, properties.First.Value.Type);
            Assert.AreEqual("/target:\"[value]\"", properties.First.Value.SwitchName);
            Assert.AreEqual(";", properties.First.Value.Separator);
        }

        /// <summary>
        /// Tests a simple string array property. 
        /// </summary>
        [TestMethod]
        public void TestStringArrayPropertyWithDataSource_DataSourceIsItem()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringListProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` Separator=`;`>
                                         <StringListProperty.DataSource>
                                           <DataSource SourceType=`Item` Persistence=`ProjectFile` ItemType=`AssemblyName` />
                                         </StringListProperty.DataSource>
                                       </StringListProperty>
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.AreEqual(1, properties.Count, "Expected one property but there were " + properties.Count);
            Assert.IsNotNull(properties.First.Value, "TargetAssembly switch should exist");
            Assert.AreEqual("TargetAssembly", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.ItemArray, properties.First.Value.Type);  // Although it's a String array property, DataSource.SourceType overrides that
            Assert.AreEqual("/target:\"[value]\"", properties.First.Value.SwitchName);
            Assert.AreEqual(";", properties.First.Value.Separator);
        }
    }

    #endregion

    #region Tests for compilation

    [TestClass]
    public class CompilationTests
    {
        /// <summary>
        /// Tests to see if the generated stream compiles
        /// Code must be compilable on its own.
        /// </summary>
        [TestMethod]
        public void TestGenerateCodeToStream()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <EnumProperty Name=`GeneratePreprocessedFile` Switch=`nologo`>
                                         <EnumValue Name=`Disabled` />
                                         <EnumValue Name=`Yes` Switch=`P` />
                                         <EnumValue Name=`NoLineNumbers` Switch=`EP` />
                                       </EnumProperty>
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            TaskGenerator tg = new TaskGenerator(tp);
            CodeCompileUnit compileUnit = tg.GenerateCode();
            CodeDomProvider codeGenerator = CodeDomProvider.CreateProvider("CSharp");

            using (StringWriter sw = new StringWriter(CultureInfo.CurrentCulture))
            {
                CodeGeneratorOptions options = new CodeGeneratorOptions();
                options.BlankLinesBetweenMembers = true;
                options.BracingStyle = "C";

                codeGenerator.GenerateCodeFromCompileUnit(compileUnit, sw, options);

                CSharpCodeProvider provider = new CSharpCodeProvider();
                // Build the parameters for source compilation.
                CompilerParameters cp = new CompilerParameters();

                // Add an assembly reference.
                cp.ReferencedAssemblies.Add("System.dll");
                cp.ReferencedAssemblies.Add("System.XML.dll");
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "microsoft.build.utilities.core.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "microsoft.build.tasks.core.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "microsoft.build.framework.dll"));
                cp.ReferencedAssemblies.Add("System.Data.dll");

                // Generate an executable instead of 
                // a class library.
                cp.GenerateExecutable = false;
                // Set the assembly file name to generate.
                cp.GenerateInMemory = true;
                // Invoke compilation
                CompilerResults cr = provider.CompileAssemblyFromSource(cp, sw.ToString());
                // put in finally block
                Assert.IsTrue(cr.Errors.Count == 0, "Compilation Failed");
            }
        }

        /// <summary>
        /// Tests to make sure the file generated compiles
        /// </summary>
        [TestMethod]
        public void TestGenerateToFile()
        {
            string xml = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                             <Rule Name=`CL`>
                               <EnumProperty Name=`GeneratePreprocessedFile` Switch=`nologo`>
                                 <EnumValue Name=`Disabled` />
                                 <EnumValue Name=`Yes` Switch=`P` />
                                 <EnumValue Name=`NoLineNumbers` Switch=`EP` />
                               </EnumProperty>
                             </Rule>
                           </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xml, "CL");
            TaskGenerator tg = new TaskGenerator(tp);
            CodeCompileUnit compileUnit = tg.GenerateCode();
            CodeDomProvider codeGenerator = CodeDomProvider.CreateProvider("CSharp");

            try
            {
                using (StreamWriter sw = new StreamWriter("XamlTaskFactory_Tests_TestGenerateToFile.cs"))
                {
                    CodeGeneratorOptions options = new CodeGeneratorOptions();
                    options.BlankLinesBetweenMembers = true;
                    options.BracingStyle = "C";

                    codeGenerator.GenerateCodeFromCompileUnit(compileUnit, sw, options);
                }

                CSharpCodeProvider provider = new CSharpCodeProvider();
                // Build the parameters for source compilation.
                CompilerParameters cp = new CompilerParameters();

                // Add an assembly reference.
                cp.ReferencedAssemblies.Add("System.dll");
                cp.ReferencedAssemblies.Add("System.XML.dll");
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "microsoft.build.utilities.core.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "microsoft.build.tasks.core.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "microsoft.build.framework.dll"));
                cp.ReferencedAssemblies.Add("System.Data.dll");

                // Generate an executable instead of 
                // a class library.
                cp.GenerateExecutable = false;
                // Set the assembly file name to generate.
                cp.GenerateInMemory = true;
                // Invoke compilation
                CompilerResults cr = provider.CompileAssemblyFromFile(cp, "XamlTaskFactory_Tests_TestGenerateToFile.cs");
                Assert.IsTrue(cr.Errors.Count == 0, "Compilation Failed");
            }
            finally
            {
                if (File.Exists("XamlTaskFactory_Tests_TestGenerateToFile.cs"))
                {
                    File.Delete("XamlTaskFactory_Tests_TestGenerateToFile.cs");
                }
            }
        }
    }
    #endregion

    #region Tests Generated code based on one xml file
    [TestClass]
    public sealed class GeneratedTaskTests
    {
        private Assembly _fakeTaskDll;

        /// <summary>
        /// Tests that quotes are correctly escaped
        /// </summary>
        [TestMethod]
        public void TestQuotingQuotes()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode(XamlTestHelpers.QuotingQuotesXml);
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
        }

        /// <summary>
        /// Tests that backslashes are correctly escaped
        /// </summary>
        [TestMethod]
        public void TestQuotingBackslashes()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode(XamlTestHelpers.QuotingBackslashXml);
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
        }

        /// <summary>
        /// Tests the GenerateReversible method
        /// </summary>
        [TestMethod]
        public void TestGenerateReversible()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicReversible");
            Assert.IsNotNull(pi, "Shouldn't be null");
            Assert.IsTrue(pi.PropertyType == typeof(bool), "PropertyType should be a boolean");
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.IsTrue((attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString()) == "/Br");
            }
        }

        /// <summary>
        /// Tests the GenerateNonreversible method
        /// </summary>
        [TestMethod]
        public void TestGenerateNonreversible()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicNonreversible");
            Assert.IsNotNull(pi, "Shouldn't be null");
            Assert.IsTrue(pi.PropertyType == typeof(bool), "PropertyType should be a boolean");
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.IsTrue((attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString()) == "/Bn");
            }
        }

        /// <summary>
        /// Tests the GenerateStrings method
        /// </summary>
        [TestMethod]
        public void TestGenerateStrings()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicString");
            Assert.IsNotNull(pi, "Shouldn't be null");
            Assert.IsTrue(pi.PropertyType == typeof(string), "PropertyType should be a string");
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.IsTrue((attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString()) == "/Bs");
            }
        }

        /// <summary>
        /// Tests the GenerateIntegers method
        /// </summary>
        [TestMethod]
        public void TestGenerateIntegers()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicInteger");
            Assert.IsNotNull(pi, "Shouldn't be null");
            Assert.IsTrue(pi.PropertyType == typeof(int), "PropertyType should be an int");
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.IsTrue((attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString()) == "/Bi");
            }
        }

        /// <summary>
        /// Tests the GenerateStringArrays method
        /// </summary>
        [TestMethod]
        public void TestGenerateStringArrays()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicStringArray");
            Assert.IsNotNull(pi, "Shouldn't be null");
            Assert.IsTrue(pi.PropertyType == typeof(string[]), "PropertyType should be a stringarray");
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                PropertyInfo documentationAttribute = attribute.GetType().GetProperty("SwitchName");
                if (documentationAttribute != null)
                {
                    Assert.IsTrue((attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString()) == "/Bsa");
                }
                else
                {
                    // required attribute
                    Assert.IsTrue(attribute is RequiredAttribute);
                }
            }
        }

        [TestMethod]
        public void TestBasicReversibleTrue()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicReversible", true);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            bool booleanValue = switchList["BasicReversible"].BooleanValue;
            string toolSwitchValue;
            if (booleanValue)
            {
                toolSwitchValue = switchList["BasicReversible"].SwitchValue;
            }
            else
            {
                toolSwitchValue = switchList["BasicReversible"].SwitchValue + switchList["BasicReversible"].FalseSuffix;
            }
            Assert.IsTrue(toolSwitchValue == "/Br", "Expected /Br, got " + toolSwitchValue);
        }

        [TestMethod]
        public void TestBasicReversibleFalse()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicReversible", false);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            bool booleanValue = switchList["BasicReversible"].BooleanValue;
            string toolSwitchValue;
            if (booleanValue)
            {
                toolSwitchValue = switchList["BasicReversible"].SwitchValue;
            }
            else
            {
                toolSwitchValue = switchList["BasicReversible"].ReverseSwitchValue;
            }
            Assert.IsTrue(toolSwitchValue == "/BrF", "Expected /BrF, got " + toolSwitchValue);
        }

        [TestMethod]
        public void TestBasicNonreversible()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicNonreversible", true);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            bool booleanValue = switchList["BasicNonreversible"].BooleanValue;
            Assert.IsTrue(booleanValue, "Actual BooleanValue is " + booleanValue.ToString());
            string toolSwitchValue = switchList["BasicNonreversible"].SwitchValue;
            Assert.IsTrue(toolSwitchValue == "/Bn", "Expected /Bn, got " + toolSwitchValue);
        }

        [TestMethod]
        public void TestBasicString()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicString", "Enum1");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string CommandLineToolSwitchOutput = switchList["BasicString"].SwitchValue;
            Assert.IsTrue(CommandLineToolSwitchOutput == "/Bs1", "Expected /Bs1, got " + CommandLineToolSwitchOutput);
            obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicString", "Enum2");
            switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsTrue(switchList != null);
            CommandLineToolSwitchOutput = switchList["BasicString"].SwitchValue;
            Assert.IsTrue(CommandLineToolSwitchOutput == "/Bs2", "Expected /Bs2, got " + CommandLineToolSwitchOutput);
        }

        [TestMethod]
        public void TestBasicStringArray()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicStringArray", new string[1]);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string toolSwitchValue = switchList["BasicStringArray"].SwitchValue;
            Assert.IsTrue(toolSwitchValue == "/Bsa", "Expected /Bsa, got " + toolSwitchValue);
        }

        [TestMethod]
        public void TestBasicFileWSwitch()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicFileWSwitch", "File");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string toolSwitchValue = switchList["BasicFileWSwitch"].SwitchValue;
            Assert.IsTrue(toolSwitchValue == "/Bfws", "Expected /Bfws, got " + toolSwitchValue);
        }

        [TestMethod]
        public void TestBasicFileWOSwitch()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicFileWOSwitch", "File");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string toolSwitchValue = switchList["BasicFileWOSwitch"].SwitchValue;
            Assert.IsTrue(String.IsNullOrEmpty(toolSwitchValue), "Expected nothing, got " + toolSwitchValue);
        }

        [TestMethod]
        public void TestBasicDynamicEnum()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicDynamicEnum", "MySpecialBeforeTarget");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string toolSwitchValue = switchList["BasicDynamicEnum"].SwitchValue;
            Assert.IsTrue(String.IsNullOrEmpty(toolSwitchValue), "Expected nothing, got " + toolSwitchValue);
        }

        [TestMethod]
        public void TestBasicDirectory()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicDirectory", "FakeDirectory");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string toolSwitchValue = switchList["BasicDirectory"].SwitchValue;
            Assert.IsTrue(String.IsNullOrEmpty(toolSwitchValue), "Expected nothing, got " + toolSwitchValue);
        }

        [TestMethod]
        public void TestBasicInteger()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicInteger", 1);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string CommandLineToolSwitchOutput = switchList["BasicInteger"].SwitchValue + switchList["BasicInteger"].Separator + switchList["BasicInteger"].Number;
            Assert.IsTrue(CommandLineToolSwitchOutput == "/Bi1", "Expected /Bi1, got " + CommandLineToolSwitchOutput);
        }

        #endregion
    }
}