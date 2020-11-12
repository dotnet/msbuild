// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using System.IO;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Reflection;
using Microsoft.Build.Shared;
using System.Globalization;
using Microsoft.Build.Tasks.Xaml;
using System.Xaml;
using Xunit;
using Shouldly;

namespace Microsoft.Build.UnitTests.XamlTaskFactory_Tests
{
    #region Tests for Load and Parse methods
    /// <summary>
    /// The text fixture to unit test the task generator.
    /// Creates a new TaskGenerator object and tests the various methods
    /// </summary>
    [Trait("Category", "mono-osx-failing")]
    public sealed class LoadAndParseTests
    {
        /// <summary>
        /// Tests the load method. Expects true to be returned.
        /// </summary>
        [Fact]
        public void TestLoadXml()
        {
            TaskParser tp = new TaskParser();
            string s = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                           <Rule Name=`TaskGeneratorLoadTest`>
                             <BoolProperty Name=`TestProperty1` Switch=`tp` />
                           </Rule>
                         </ProjectSchemaDefinitions>";
            Assert.True(tp.Parse(s.Replace("`", "\""), "TaskGeneratorLoadTest")); // "File failed to load correctly."
        }

        /// <summary>
        /// Tests the TaskName property. 
        /// Should get "CL" back for this specific case.
        /// </summary>
        [Fact]
        public void TestGetTaskName()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.Equal("CL", tp.GeneratedTaskName);
        }

        /// <summary>
        /// Tests the BaseClass property. XamlTaskFactory does not currently support setting the BaseClass.
        /// </summary>
        [Fact(Skip = "Ignored in MSTest")]
        public void TestGetBaseClass()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL` BaseClass=`mybase.class`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.Equal("DataDrivenToolTask", tp.BaseClass);
        }

        /// <summary>
        /// Tests the ResourceNamespace property. XamlTaskFactory does not currently support setting the ResourceNamespace.
        /// </summary>
        [Fact(Skip = "Ignored in MSTest")]
        public void TestGetResourceNamespace()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL` ResourceNamespace=`Microsoft.Build.NativeTasks.Strings`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.Null(tp.ResourceNamespace);
        }

        /// <summary>
        /// Tests the Namespace property. XamlTaskFactory does not currently support setting the Namespace.
        /// </summary>
        [Fact(Skip = "Ignored in MSTest")]
        public void TestGetNamespace()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL` Namespace=`mynamespace`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.Equal("XamlTaskNamespace", tp.Namespace);
        }

        /// <summary>
        /// See what happens when the name is missing from the task element
        /// </summary>
        [Fact]
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

            Assert.True(exceptionCaught); // "Should have caught a XamlParseException"
        }

        /// <summary>
        /// See what happens when the task element is valid, but we're searching for a different rule that's not in the file.
        /// </summary>
        [Fact]
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

            Assert.True(exceptionCaught); // "Should have caught a XamlParseException"
        }

        /// <summary>
        /// Basic test of several reversible boolean switches, to verify that everything gets passed through correctly.
        /// </summary>
        [Fact]
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

            Assert.Equal(2, properties.Count); // "Expected two properties but there were " + properties.Count
            Assert.NotNull(properties.First.Value); // "GlobalOptimizations switch should exist"
            Assert.Equal("GlobalOptimizations", properties.First.Value.Name);
            Assert.Equal("Og", properties.First.Value.SwitchName);
            Assert.Equal("Og-", properties.First.Value.ReverseSwitchName);
            Assert.Equal("true", properties.First.Value.Reversible); // "Switch should be marked as reversible"

            properties.RemoveFirst();

            Assert.NotNull(properties.First.Value); // "IntrinsicFunctions switch should exist"
            Assert.Equal("IntrinsicFunctions", properties.First.Value.Name);
            Assert.Equal("Oi", properties.First.Value.SwitchName);
            Assert.Equal("Oi:NO", properties.First.Value.ReverseSwitchName);
            Assert.Equal("true", properties.First.Value.Reversible); // "Switch should be marked as reversible"
            Assert.Equal(PropertyType.Boolean, properties.First.Value.Type);
        }

        [Fact]
        public void TestParseIncorrect_PropertyNamesMustBeUnique()
        {
            string incorrectXmlContents = @"<ProjectSchemaDefinitions
                                       xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework`
                                       xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml`
                                       xmlns:sys=`clr-namespace:System;assembly=mscorlib`
                                       xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`SameName` Switch=`Og` ReverseSwitch=`Og-` />
                                       <BoolProperty Name=`SameName` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            Should
                .Throw<XamlParseException>(() => XamlTestHelpers.LoadAndParse(incorrectXmlContents, "CL"))
                .Message.ShouldStartWith("MSB3724");
        }

        /// <summary>
        /// Tests a basic non-reversible booleans switch
        /// </summary>
        [Fact]
        public void TestBasicNonReversibleBooleanSwitch()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`KeepComments` Switch=`C` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.Single(properties); // "Expected one property but there were " + properties.Count
            Assert.NotNull(properties.First.Value); // "KeepComments switch should exist"
            Assert.Equal("KeepComments", properties.First.Value.Name);
            Assert.Equal("C", properties.First.Value.SwitchName);
            Assert.Null(properties.First.Value.ReverseSwitchName); // "KeepComments shouldn't have a reverse switch value"
            Assert.Equal(String.Empty, properties.First.Value.Reversible); // "Switch should NOT marked as reversible"
            Assert.Equal(String.Empty, properties.First.Value.DefaultValue); // "Switch should NOT have a default value"
            Assert.Equal(PropertyType.Boolean, properties.First.Value.Type);
        }

        /// <summary>
        /// Tests a basic non-reversible booleans switch that has a default value set. 
        /// </summary>
        [Fact]
        public void TestBasicNonReversibleBooleanSwitch_WithDefault()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`SuppressStartupBanner` Switch=`nologo` Default=`true` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.Single(properties); // "Expected one property but there were " + properties.Count
            Assert.NotNull(properties.First.Value); // "SuppressStartupBanner switch should exist"
            Assert.Equal("SuppressStartupBanner", properties.First.Value.Name);
            Assert.Equal("nologo", properties.First.Value.SwitchName);
            Assert.Null(properties.First.Value.ReverseSwitchName); // "SuppressStartupBanner shouldn't have a reverse switch value"
            Assert.Equal(String.Empty, properties.First.Value.Reversible); // "Switch should NOT be marked as reversible"
            Assert.Equal("true", properties.First.Value.DefaultValue); // "Switch should default to true"
            Assert.Equal(PropertyType.Boolean, properties.First.Value.Type);
        }

        /// <summary>
        /// Test for a basic string property switch
        /// </summary>
        [Fact]
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

            Assert.Single(properties); // "Expected one property but there were " + properties.Count
            Assert.NotNull(properties.First.Value); // "GeneratePreprocessedFile switch should exist"
            Assert.Equal("GeneratePreprocessedFile", properties.First.Value.Name);
            Assert.Equal(PropertyType.String, properties.First.Value.Type); // Enum properties are represented as string types
            Assert.Equal(3, properties.First.Value.Values.Count); // "GeneratePreprocessedFile should have three values"
        }

        /// <summary>
        /// Tests XamlTaskFactory support for DynamicEnumProperties.  These are primarily of use as a visualization in the property pages; as far as the 
        /// XamlTaskFactory and XamlDataDrivenToolTask are concerned, they are treated as StringProperties.  
        /// </summary>
        [Fact]
        public void TestDynamicEnumProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <DynamicEnumProperty Name=`CLBeforeTargets` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.Single(properties); // "Expected one property but there were " + properties.Count
            Assert.NotNull(properties.First.Value); // "CLBeforeTargets switch should exist"
            Assert.Equal("CLBeforeTargets", properties.First.Value.Name);
            Assert.Equal(PropertyType.String, properties.First.Value.Type); // Enum properties are represented as string types
        }

        /// <summary>
        /// Tests a simple string property. 
        /// </summary>
        [Fact]
        public void TestBasicStringProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.Single(properties); // "Expected one property but there were " + properties.Count
            Assert.NotNull(properties.First.Value); // "TargetAssembly switch should exist"
            Assert.Equal("TargetAssembly", properties.First.Value.Name);
            Assert.Equal(PropertyType.String, properties.First.Value.Type);
            Assert.Equal("/target:\"[value]\"", properties.First.Value.SwitchName);
        }

        [Fact]
        public void TestLoadAndParseFromAbsoluteFilePath()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            string tmpXamlFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText(tmpXamlFile, xmlContents.Replace("`", "\""));
                TaskParser tp = new TaskParser();
                tp.Parse(tmpXamlFile, "CL");

                LinkedList<Property> properties = tp.Properties;

                Assert.Single(properties); // "Expected one property but there were " + properties.Count
                Assert.NotNull(properties.First.Value); // "TargetAssembly switch should exist"
                Assert.Equal("TargetAssembly", properties.First.Value.Name);
                Assert.Equal(PropertyType.String, properties.First.Value.Type);
                Assert.Equal("/target:\"[value]\"", properties.First.Value.SwitchName);
            }
            finally
            {
                // This throws because the file is still in use!
                //if (File.Exists(tmpXamlFile))
                //    File.Delete(tmpXamlFile);
            }
        }

        /// <summary>
        /// Tests a simple string array property. 
        /// </summary>
        [Fact]
        public void TestBasicStringArrayProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringListProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` Separator=`;` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.Single(properties); // "Expected one property but there were " + properties.Count
            Assert.NotNull(properties.First.Value); // "TargetAssembly switch should exist"
            Assert.Equal("TargetAssembly", properties.First.Value.Name);
            Assert.Equal(PropertyType.StringArray, properties.First.Value.Type);
            Assert.Equal("/target:\"[value]\"", properties.First.Value.SwitchName);
            Assert.Equal(";", properties.First.Value.Separator);
        }

        /// <summary>
        /// Tests a simple string array property. 
        /// </summary>
        [Fact]
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

            Assert.Single(properties); // "Expected one property but there were " + properties.Count
            Assert.NotNull(properties.First.Value); // "TargetAssembly switch should exist"
            Assert.Equal("TargetAssembly", properties.First.Value.Name);
            Assert.Equal(PropertyType.StringArray, properties.First.Value.Type);
            Assert.Equal("/target:\"[value]\"", properties.First.Value.SwitchName);
            Assert.Equal(";", properties.First.Value.Separator);
        }

        /// <summary>
        /// Tests a simple string array property. 
        /// </summary>
        [Fact]
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

            Assert.Single(properties); // "Expected one property but there were " + properties.Count
            Assert.NotNull(properties.First.Value); // "TargetAssembly switch should exist"
            Assert.Equal("TargetAssembly", properties.First.Value.Name);
            Assert.Equal(PropertyType.ItemArray, properties.First.Value.Type);  // Although it's a String array property, DataSource.SourceType overrides that
            Assert.Equal("/target:\"[value]\"", properties.First.Value.SwitchName);
            Assert.Equal(";", properties.First.Value.Separator);
        }
    }

    #endregion

    #region Tests for compilation

    public class CompilationTests
    {
        /// <summary>
        /// Tests to see if the generated stream compiles
        /// Code must be compilable on its own.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
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
                cp.ReferencedAssemblies.Add("System.Xml.dll");
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "Microsoft.Build.Utilities.Core.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "Microsoft.Build.Tasks.Core.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "Microsoft.Build.Framework.dll"));
                cp.ReferencedAssemblies.Add("System.Data.dll");

                // Generate an executable instead of 
                // a class library.
                cp.GenerateExecutable = false;
                // Set the assembly file name to generate.
                cp.GenerateInMemory = true;
                // Invoke compilation
                CompilerResults cr = provider.CompileAssemblyFromSource(cp, sw.ToString());
                // put in finally block
                Assert.Empty(cr.Errors); // "Compilation Failed"
            }
        }

        /// <summary>
        /// Tests to make sure the file generated compiles
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
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
                cp.ReferencedAssemblies.Add("System.Xml.dll");
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "Microsoft.Build.Utilities.Core.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "Microsoft.Build.Tasks.Core.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(XamlTestHelpers.PathToMSBuildBinaries, "Microsoft.Build.Framework.dll"));
                cp.ReferencedAssemblies.Add("System.Data.dll");

                // Generate an executable instead of 
                // a class library.
                cp.GenerateExecutable = false;
                // Set the assembly file name to generate.
                cp.GenerateInMemory = true;
                // Invoke compilation
                CompilerResults cr = provider.CompileAssemblyFromFile(cp, "XamlTaskFactory_Tests_TestGenerateToFile.cs");
                Assert.Empty(cr.Errors); // "Compilation Failed"
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
    [Trait("Category", "mono-osx-failing")]
    public sealed class GeneratedTaskTests
    {
        private Assembly _fakeTaskDll;

        /// <summary>
        /// Tests that quotes are correctly escaped
        /// </summary>
        [Fact]
        public void TestQuotingQuotes()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode(XamlTestHelpers.QuotingQuotesXml);
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
        }

        /// <summary>
        /// Tests that backslashes are correctly escaped
        /// </summary>
        [Fact]
        public void TestQuotingBackslashes()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode(XamlTestHelpers.QuotingBackslashXml);
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
        }

        /// <summary>
        /// Tests the GenerateReversible method
        /// </summary>
        [Fact]
        public void TestGenerateReversible()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicReversible");
            Assert.NotNull(pi); // "Shouldn't be null"
            Assert.Equal(typeof(bool), pi.PropertyType); // "PropertyType should be a boolean"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.Equal("/Br", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
            }
        }

        /// <summary>
        /// Tests the GenerateNonreversible method
        /// </summary>
        [Fact]
        public void TestGenerateNonreversible()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicNonreversible");
            Assert.NotNull(pi); // "Shouldn't be null"
            Assert.Equal(typeof(bool), pi.PropertyType); // "PropertyType should be a boolean"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.Equal("/Bn", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
            }
        }

        /// <summary>
        /// Tests the GenerateStrings method
        /// </summary>
        [Fact]
        public void TestGenerateStrings()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicString");
            Assert.NotNull(pi); // "Shouldn't be null"
            Assert.Equal(typeof(string), pi.PropertyType); // "PropertyType should be a string"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.Equal("/Bs", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
            }
        }

        /// <summary>
        /// Tests the GenerateIntegers method
        /// </summary>
        [Fact]
        public void TestGenerateIntegers()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicInteger");
            Assert.NotNull(pi); // "Shouldn't be null"
            Assert.Equal(typeof(int), pi.PropertyType); // "PropertyType should be an int"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.Equal("/Bi", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
            }
        }

        /// <summary>
        /// Tests the GenerateStringArrays method
        /// </summary>
        [Fact]
        public void TestGenerateStringArrays()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicStringArray");
            Assert.NotNull(pi); // "Shouldn't be null"
            Assert.Equal(typeof(string[]), pi.PropertyType); // "PropertyType should be a stringarray"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                PropertyInfo documentationAttribute = attribute.GetType().GetProperty("SwitchName");
                if (documentationAttribute != null)
                {
                    Assert.Equal("/Bsa", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
                }
                else
                {
                    // required attribute
                    Assert.IsType<RequiredAttribute>(attribute);
                }
            }
        }

        [Fact]
        public void TestBasicReversibleTrue()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicReversible", true);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
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

            Assert.Equal("/Br", toolSwitchValue);
        }

        [Fact]
        public void TestBasicReversibleFalse()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicReversible", false);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
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
            Assert.Equal("/BrF", toolSwitchValue);
        }

        [Fact]
        public void TestBasicNonreversible()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicNonreversible", true);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
            bool booleanValue = switchList["BasicNonreversible"].BooleanValue;
            Assert.True(booleanValue, "Actual BooleanValue is " + booleanValue.ToString());
            string toolSwitchValue = switchList["BasicNonreversible"].SwitchValue;
            Assert.Equal("/Bn", toolSwitchValue);
        }

        [Fact]
        public void TestBasicString()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicString", "Enum1");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
            string CommandLineToolSwitchOutput = switchList["BasicString"].SwitchValue;
            Assert.Equal("/Bs1", CommandLineToolSwitchOutput);
            obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicString", "Enum2");
            switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
            CommandLineToolSwitchOutput = switchList["BasicString"].SwitchValue;
            Assert.Equal("/Bs2", CommandLineToolSwitchOutput);
        }

        [Fact]
        public void TestBasicStringArray()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicStringArray", new string[1]);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
            string toolSwitchValue = switchList["BasicStringArray"].SwitchValue;
            Assert.Equal("/Bsa", toolSwitchValue);
        }

        [Fact]
        public void TestBasicFileWSwitch()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicFileWSwitch", "File");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
            string toolSwitchValue = switchList["BasicFileWSwitch"].SwitchValue;
            Assert.Equal("/Bfws", toolSwitchValue);
        }

        [Fact]
        public void TestBasicFileWOSwitch()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicFileWOSwitch", "File");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
            string toolSwitchValue = switchList["BasicFileWOSwitch"].SwitchValue;
            Assert.True(String.IsNullOrEmpty(toolSwitchValue), "Expected nothing, got " + toolSwitchValue);
        }

        [Fact]
        public void TestBasicDynamicEnum()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicDynamicEnum", "MySpecialBeforeTarget");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
            string toolSwitchValue = switchList["BasicDynamicEnum"].SwitchValue;
            Assert.True(String.IsNullOrEmpty(toolSwitchValue), "Expected nothing, got " + toolSwitchValue);
        }

        [Fact]
        public void TestBasicDirectory()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicDirectory", "FakeDirectory");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
            string toolSwitchValue = switchList["BasicDirectory"].SwitchValue;
            Assert.True(String.IsNullOrEmpty(toolSwitchValue), "Expected nothing, got " + toolSwitchValue);
        }

        [Fact]
        public void TestBasicInteger()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicInteger", 1);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.NotNull(switchList);
            string CommandLineToolSwitchOutput = switchList["BasicInteger"].SwitchValue + switchList["BasicInteger"].Separator + switchList["BasicInteger"].Number;
            Assert.Equal("/Bi1", CommandLineToolSwitchOutput);
        }

        #endregion
    }
}
