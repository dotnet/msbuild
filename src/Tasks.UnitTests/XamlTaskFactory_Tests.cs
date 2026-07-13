// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xaml;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.Xaml;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.CSharp;
using Shouldly;

#nullable disable

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
        [MSBuildTestMethod]
        public void TestLoadXml()
        {
            TaskParser tp = new TaskParser();
            string s = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                           <Rule Name=`TaskGeneratorLoadTest`>
                             <BoolProperty Name=`TestProperty1` Switch=`tp` />
                           </Rule>
                         </ProjectSchemaDefinitions>";
            Assert.IsTrue(tp.Parse(s.Replace("`", "\""), "TaskGeneratorLoadTest")); // "File failed to load correctly."
        }

        /// <summary>
        /// Tests the TaskName property.
        /// Should get "CL" back for this specific case.
        /// </summary>
        [MSBuildTestMethod]
        public void TestGetTaskName()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.AreEqual("CL", tp.GeneratedTaskName);
        }

        /// <summary>
        /// Tests the BaseClass property. XamlTaskFactory does not currently support setting the BaseClass.
        /// </summary>
        [MSBuildTestMethod]
        [Ignore("Ignored in MSTest")]
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
        [MSBuildTestMethod]
        [Ignore("Ignored in MSTest")]
        public void TestGetResourceNamespace()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL` ResourceNamespace=`Microsoft.Build.NativeTasks.Strings`>
                                       <BoolProperty Name=`GlobalOptimization` Switch=`Og` ReverseSwitch=`Og-` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";

            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");
            Assert.IsNull(tp.ResourceNamespace);
        }

        /// <summary>
        /// Tests the Namespace property. XamlTaskFactory does not currently support setting the Namespace.
        /// </summary>
        [MSBuildTestMethod]
        [Ignore("Ignored in MSTest")]
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
        [MSBuildTestMethod]
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

            Assert.IsTrue(exceptionCaught); // "Should have caught a XamlParseException"
        }

        /// <summary>
        /// See what happens when the task element is valid, but we're searching for a different rule that's not in the file.
        /// </summary>
        [MSBuildTestMethod]
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

            Assert.IsTrue(exceptionCaught); // "Should have caught a XamlParseException"
        }

        /// <summary>
        /// Basic test of several reversible boolean switches, to verify that everything gets passed through correctly.
        /// </summary>
        [MSBuildTestMethod]
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

            Assert.AreEqual(2, properties.Count); // "Expected two properties but there were " + properties.Count
            Assert.IsNotNull(properties.First.Value); // "GlobalOptimizations switch should exist"
            Assert.AreEqual("GlobalOptimizations", properties.First.Value.Name);
            Assert.AreEqual("Og", properties.First.Value.SwitchName);
            Assert.AreEqual("Og-", properties.First.Value.ReverseSwitchName);
            Assert.AreEqual("true", properties.First.Value.Reversible); // "Switch should be marked as reversible"

            properties.RemoveFirst();

            Assert.IsNotNull(properties.First.Value); // "IntrinsicFunctions switch should exist"
            Assert.AreEqual("IntrinsicFunctions", properties.First.Value.Name);
            Assert.AreEqual("Oi", properties.First.Value.SwitchName);
            Assert.AreEqual("Oi:NO", properties.First.Value.ReverseSwitchName);
            Assert.AreEqual("true", properties.First.Value.Reversible); // "Switch should be marked as reversible"
            Assert.AreEqual(PropertyType.Boolean, properties.First.Value.Type);
        }

        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void TestBasicNonReversibleBooleanSwitch()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`KeepComments` Switch=`C` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.ContainsSingle(properties); // "Expected one property but there were " + properties.Count
            Assert.IsNotNull(properties.First.Value); // "KeepComments switch should exist"
            Assert.AreEqual("KeepComments", properties.First.Value.Name);
            Assert.AreEqual("C", properties.First.Value.SwitchName);
            Assert.IsNull(properties.First.Value.ReverseSwitchName); // "KeepComments shouldn't have a reverse switch value"
            Assert.AreEqual(String.Empty, properties.First.Value.Reversible); // "Switch should NOT marked as reversible"
            Assert.AreEqual(String.Empty, properties.First.Value.DefaultValue); // "Switch should NOT have a default value"
            Assert.AreEqual(PropertyType.Boolean, properties.First.Value.Type);
        }

        /// <summary>
        /// Tests a basic non-reversible booleans switch that has a default value set.
        /// </summary>
        [MSBuildTestMethod]
        public void TestBasicNonReversibleBooleanSwitch_WithDefault()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <BoolProperty Name=`SuppressStartupBanner` Switch=`nologo` Default=`true` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.ContainsSingle(properties); // "Expected one property but there were " + properties.Count
            Assert.IsNotNull(properties.First.Value); // "SuppressStartupBanner switch should exist"
            Assert.AreEqual("SuppressStartupBanner", properties.First.Value.Name);
            Assert.AreEqual("nologo", properties.First.Value.SwitchName);
            Assert.IsNull(properties.First.Value.ReverseSwitchName); // "SuppressStartupBanner shouldn't have a reverse switch value"
            Assert.AreEqual(String.Empty, properties.First.Value.Reversible); // "Switch should NOT be marked as reversible"
            Assert.AreEqual("true", properties.First.Value.DefaultValue); // "Switch should default to true"
            Assert.AreEqual(PropertyType.Boolean, properties.First.Value.Type);
        }

        /// <summary>
        /// Test for a basic string property switch
        /// </summary>
        [MSBuildTestMethod]
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

            Assert.ContainsSingle(properties); // "Expected one property but there were " + properties.Count
            Assert.IsNotNull(properties.First.Value); // "GeneratePreprocessedFile switch should exist"
            Assert.AreEqual("GeneratePreprocessedFile", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.String, properties.First.Value.Type); // Enum properties are represented as string types
            Assert.AreEqual(3, properties.First.Value.Values.Count); // "GeneratePreprocessedFile should have three values"
        }

        /// <summary>
        /// Tests XamlTaskFactory support for DynamicEnumProperties.  These are primarily of use as a visualization in the property pages; as far as the
        /// XamlTaskFactory and XamlDataDrivenToolTask are concerned, they are treated as StringProperties.
        /// </summary>
        [MSBuildTestMethod]
        public void TestDynamicEnumProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <DynamicEnumProperty Name=`CLBeforeTargets` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.ContainsSingle(properties); // "Expected one property but there were " + properties.Count
            Assert.IsNotNull(properties.First.Value); // "CLBeforeTargets switch should exist"
            Assert.AreEqual("CLBeforeTargets", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.String, properties.First.Value.Type); // Enum properties are represented as string types
        }

        /// <summary>
        /// Tests a simple string property.
        /// </summary>
        [MSBuildTestMethod]
        public void TestBasicStringProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.ContainsSingle(properties); // "Expected one property but there were " + properties.Count
            Assert.IsNotNull(properties.First.Value); // "TargetAssembly switch should exist"
            Assert.AreEqual("TargetAssembly", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.String, properties.First.Value.Type);
            Assert.AreEqual("/target:\"[value]\"", properties.First.Value.SwitchName);
        }

        [MSBuildTestMethod]
        public void TestLoadAndParseFromAbsoluteFilePath()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            string tmpXamlFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(tmpXamlFile, xmlContents.Replace("`", "\""));
                TaskParser tp = new TaskParser();
                tp.Parse(tmpXamlFile, "CL");

                LinkedList<Property> properties = tp.Properties;

                Assert.ContainsSingle(properties); // "Expected one property but there were " + properties.Count
                Assert.IsNotNull(properties.First.Value); // "TargetAssembly switch should exist"
                Assert.AreEqual("TargetAssembly", properties.First.Value.Name);
                Assert.AreEqual(PropertyType.String, properties.First.Value.Type);
                Assert.AreEqual("/target:\"[value]\"", properties.First.Value.SwitchName);
            }
            finally
            {
                // This throws because the file is still in use!
                // if (File.Exists(tmpXamlFile))
                //    File.Delete(tmpXamlFile);
            }
        }

        /// <summary>
        /// Tests a simple string array property.
        /// </summary>
        [MSBuildTestMethod]
        public void TestBasicStringArrayProperty()
        {
            string xmlContents = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                     <Rule Name=`CL`>
                                       <StringListProperty Name=`TargetAssembly` Switch=`/target:&quot;[value]&quot;` Separator=`;` />
                                     </Rule>
                                   </ProjectSchemaDefinitions>";
            TaskParser tp = XamlTestHelpers.LoadAndParse(xmlContents, "CL");

            LinkedList<Property> properties = tp.Properties;

            Assert.ContainsSingle(properties); // "Expected one property but there were " + properties.Count
            Assert.IsNotNull(properties.First.Value); // "TargetAssembly switch should exist"
            Assert.AreEqual("TargetAssembly", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.StringArray, properties.First.Value.Type);
            Assert.AreEqual("/target:\"[value]\"", properties.First.Value.SwitchName);
            Assert.AreEqual(";", properties.First.Value.Separator);
        }

        /// <summary>
        /// Tests a simple string array property.
        /// </summary>
        [MSBuildTestMethod]
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

            Assert.ContainsSingle(properties); // "Expected one property but there were " + properties.Count
            Assert.IsNotNull(properties.First.Value); // "TargetAssembly switch should exist"
            Assert.AreEqual("TargetAssembly", properties.First.Value.Name);
            Assert.AreEqual(PropertyType.StringArray, properties.First.Value.Type);
            Assert.AreEqual("/target:\"[value]\"", properties.First.Value.SwitchName);
            Assert.AreEqual(";", properties.First.Value.Separator);
        }

        /// <summary>
        /// Tests a simple string array property.
        /// </summary>
        [MSBuildTestMethod]
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

            Assert.ContainsSingle(properties); // "Expected one property but there were " + properties.Count
            Assert.IsNotNull(properties.First.Value); // "TargetAssembly switch should exist"
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
    [MSBuildTestMethod]
    public void OutOfProcXamlTaskFactoryProvidesAssemblyPath()
    {
      try
      {
        const string taskElementContents = @"<ProjectSchemaDefinitions xmlns=""clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
  <Rule Name=""FakeTask"">
  <BoolProperty Name=""Always"" Switch=""/always"" />
  </Rule>
</ProjectSchemaDefinitions>";

        var factory = new Microsoft.Build.Tasks.XamlTaskFactory();
        var loggingHost = new MockEngine { ForceOutOfProcessExecution = true };
        bool initialized = factory.Initialize(
          "FakeTask",
          new Dictionary<string, TaskPropertyInfo>(StringComparer.OrdinalIgnoreCase),
          taskElementContents,
          loggingHost);
        initialized.ShouldBeTrue(loggingHost.Log);

        ITask task = factory.CreateTask(loggingHost);
        task.ShouldNotBeNull();

        string assemblyPath = factory.GetAssemblyPath();
        assemblyPath.ShouldNotBeNullOrEmpty();
        File.Exists(assemblyPath).ShouldBeTrue();

        factory.CleanupTask(task);
      }
      finally
      {
      }
    }

        /// <summary>
        /// Tests to see if the generated stream compiles
        /// Code must be compilable on its own.
        /// </summary>
        [MSBuildTestMethod]
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
            using CodeDomProvider codeGenerator = CodeDomProvider.CreateProvider("CSharp");

            using (StringWriter sw = new StringWriter(CultureInfo.CurrentCulture))
            {
                CodeGeneratorOptions options = new CodeGeneratorOptions();
                options.BlankLinesBetweenMembers = true;
                options.BracingStyle = "C";

                codeGenerator.GenerateCodeFromCompileUnit(compileUnit, sw, options);

                using CSharpCodeProvider provider = new CSharpCodeProvider();
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
                Assert.IsEmpty(cr.Errors); // "Compilation Failed"
            }
        }

        /// <summary>
        /// Tests to make sure the file generated compiles
        /// </summary>
        [MSBuildTestMethod]
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
            using CodeDomProvider codeGenerator = CodeDomProvider.CreateProvider("CSharp");

            try
            {
                using (StreamWriter sw = new StreamWriter("XamlTaskFactory_Tests_TestGenerateToFile.cs"))
                {
                    CodeGeneratorOptions options = new CodeGeneratorOptions();
                    options.BlankLinesBetweenMembers = true;
                    options.BracingStyle = "C";

                    codeGenerator.GenerateCodeFromCompileUnit(compileUnit, sw, options);
                }

                using CSharpCodeProvider provider = new CSharpCodeProvider();
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
                Assert.IsEmpty(cr.Errors); // "Compilation Failed"
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
        [MSBuildTestMethod]
        public void TestQuotingQuotes()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode(XamlTestHelpers.QuotingQuotesXml);
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
        }

        /// <summary>
        /// Tests that backslashes are correctly escaped
        /// </summary>
        [MSBuildTestMethod]
        public void TestQuotingBackslashes()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode(XamlTestHelpers.QuotingBackslashXml);
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
        }

        /// <summary>
        /// Tests the GenerateReversible method
        /// </summary>
        [MSBuildTestMethod]
        public void TestGenerateReversible()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicReversible");
            Assert.IsNotNull(pi); // "Shouldn't be null"
            Assert.AreEqual(typeof(bool), pi.PropertyType); // "PropertyType should be a boolean"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.AreEqual("/Br", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
            }
        }

        /// <summary>
        /// Tests the GenerateNonreversible method
        /// </summary>
        [MSBuildTestMethod]
        public void TestGenerateNonreversible()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicNonreversible");
            Assert.IsNotNull(pi); // "Shouldn't be null"
            Assert.AreEqual(typeof(bool), pi.PropertyType); // "PropertyType should be a boolean"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.AreEqual("/Bn", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
            }
        }

        /// <summary>
        /// Tests the GenerateStrings method
        /// </summary>
        [MSBuildTestMethod]
        public void TestGenerateStrings()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicString");
            Assert.IsNotNull(pi); // "Shouldn't be null"
            Assert.AreEqual(typeof(string), pi.PropertyType); // "PropertyType should be a string"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.AreEqual("/Bs", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
            }
        }

        /// <summary>
        /// Tests the GenerateIntegers method
        /// </summary>
        [MSBuildTestMethod]
        public void TestGenerateIntegers()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicInteger");
            Assert.IsNotNull(pi); // "Shouldn't be null"
            Assert.AreEqual(typeof(int), pi.PropertyType); // "PropertyType should be an int"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                Assert.AreEqual("/Bi", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
            }
        }

        /// <summary>
        /// Tests the GenerateStringArrays method
        /// </summary>
        [MSBuildTestMethod]
        public void TestGenerateStringArrays()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            PropertyInfo pi = obj.GetType().GetProperty("BasicStringArray");
            Assert.IsNotNull(pi); // "Shouldn't be null"
            Assert.AreEqual(typeof(string[]), pi.PropertyType); // "PropertyType should be a stringarray"
            object[] attributes = pi.GetCustomAttributes(true);
            foreach (object attribute in attributes)
            {
                PropertyInfo documentationAttribute = attribute.GetType().GetProperty("SwitchName");
                if (documentationAttribute != null)
                {
                    Assert.AreEqual("/Bsa", attribute.GetType().GetProperty("SwitchName").GetValue(attribute, null).ToString());
                }
                else
                {
                    // required attribute
                    Assert.IsExactInstanceOfType<RequiredAttribute>(attribute);
                }
            }
        }

        [MSBuildTestMethod]
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

            Assert.AreEqual("/Br", toolSwitchValue);
        }

        [MSBuildTestMethod]
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
            Assert.AreEqual("/BrF", toolSwitchValue);
        }

        [MSBuildTestMethod]
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
            Assert.AreEqual("/Bn", toolSwitchValue);
        }

        [MSBuildTestMethod]
        public void TestBasicString()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicString", "Enum1");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string CommandLineToolSwitchOutput = switchList["BasicString"].SwitchValue;
            Assert.AreEqual("/Bs1", CommandLineToolSwitchOutput);
            obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicString", "Enum2");
            switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            CommandLineToolSwitchOutput = switchList["BasicString"].SwitchValue;
            Assert.AreEqual("/Bs2", CommandLineToolSwitchOutput);
        }

        [MSBuildTestMethod]
        public void TestBasicStringArray()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicStringArray", new string[1]);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string toolSwitchValue = switchList["BasicStringArray"].SwitchValue;
            Assert.AreEqual("/Bsa", toolSwitchValue);
        }

        [MSBuildTestMethod]
        public void TestBasicFileWSwitch()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicFileWSwitch", "File");
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string toolSwitchValue = switchList["BasicFileWSwitch"].SwitchValue;
            Assert.AreEqual("/Bfws", toolSwitchValue);
        }

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        public void TestBasicInteger()
        {
            _fakeTaskDll = XamlTestHelpers.SetupGeneratedCode();
            object obj = _fakeTaskDll.CreateInstance("XamlTaskNamespace.FakeTask");
            XamlTestHelpers.SetProperty(obj, "BasicInteger", 1);
            Dictionary<string, CommandLineToolSwitch> switchList = (Dictionary<string, CommandLineToolSwitch>)XamlTestHelpers.GetProperty(obj, "ActiveToolSwitches");
            Assert.IsNotNull(switchList);
            string CommandLineToolSwitchOutput = switchList["BasicInteger"].SwitchValue + switchList["BasicInteger"].Separator + switchList["BasicInteger"].Number;
            Assert.AreEqual("/Bi1", CommandLineToolSwitchOutput);
        }

        /// <summary>
        /// Verifies that ITaskFactoryBuildParameterProvider.IsMultiThreadedBuild triggers out-of-process compilation
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void MultiThreadedBuildTriggersOutOfProcCompilation(bool isMultiThreaded)
        {
            MockEngine buildEngine = new MockEngine { IsMultiThreadedBuild = isMultiThreaded };

            XamlTaskFactory factory = new XamlTaskFactory();

            // XamlTaskFactory uses ProjectSchemaDefinitions format
            string taskBody = @"
<ProjectSchemaDefinitions xmlns=""clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:sys=""clr-namespace:System;assembly=mscorlib"">
  <Rule Name=""TestXamlTask"" ToolName=""cmd.exe"">
    <StringProperty Name=""TestArg"" Switch=""/c echo "" />
  </Rule>
</ProjectSchemaDefinitions>";

            bool success = factory.Initialize("TestXamlTask", new Dictionary<string, TaskPropertyInfo>(), taskBody, buildEngine);
            success.ShouldBeTrue();

            // Get assembly path - should be non-null when compiled for out-of-proc
            string assemblyPath = factory.GetAssemblyPath();

            if (isMultiThreaded)
            {
                assemblyPath.ShouldNotBeNullOrEmpty("Assembly should be compiled to disk in multi-threaded mode");
                File.Exists(assemblyPath).ShouldBeTrue("Assembly file should exist on disk");
            }
            else
            {
                assemblyPath.ShouldBeNullOrEmpty("In-memory compilation should not have a persistent assembly path");
            }
        }

        /// <summary>
        /// Verifies that ForceOutOfProcessExecution property triggers out-of-proc compilation
        /// </summary>
        [MSBuildTestMethod]
        public void ForceOutOfProcessExecutionTriggersOutOfProcCompilation()
        {
            MockEngine buildEngine = new MockEngine 
            { 
                ForceOutOfProcessExecution = true,
                IsMultiThreadedBuild = false 
            };

            XamlTaskFactory factory = new XamlTaskFactory();

            // XamlTaskFactory uses ProjectSchemaDefinitions format
            string taskBody = @"
<ProjectSchemaDefinitions xmlns=""clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:sys=""clr-namespace:System;assembly=mscorlib"">
  <Rule Name=""TestXamlTaskForced"" ToolName=""cmd.exe"">
    <StringProperty Name=""TestArg"" Switch=""/c echo "" />
  </Rule>
</ProjectSchemaDefinitions>";

            bool success = factory.Initialize("TestXamlTaskForced", new Dictionary<string, TaskPropertyInfo>(), taskBody, buildEngine);
            success.ShouldBeTrue();

            string assemblyPath = factory.GetAssemblyPath();
            assemblyPath.ShouldNotBeNullOrEmpty("ForceOutOfProcessExecution should trigger out-of-proc compilation");
            File.Exists(assemblyPath).ShouldBeTrue("Assembly file should exist on disk");
        }

        /// <summary>
        /// End-to-end test that verifies inline tasks execute successfully when /mt is used.
        /// This confirms the inline task factory compiles for out-of-process execution and the task runs correctly.
        /// </summary>
        [MSBuildTestMethod]
        public void MultiThreadedBuildExecutesInlineTasksSuccessfully()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFolder folder = env.CreateFolder(createFolder: true);
                
                // Create a project with an inline task using XamlTaskFactory
                // XamlTaskFactory is a data-driven tool task, so we use a simple tool (cmd.exe) for testing
                TransientTestFile projectFile = env.CreateFile(folder, "test.proj", @"
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" ToolsVersion=""msbuilddefaulttoolsversion"" DefaultTargets=""Build"">
  
  <!-- Define an inline task using XamlTaskFactory -->
  <UsingTask TaskName=""MyXamlTask"" TaskFactory=""XamlTaskFactory"" AssemblyName=""Microsoft.Build.Tasks.Core, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
    <Task>
      <![CDATA[
        <ProjectSchemaDefinitions xmlns=""clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:sys=""clr-namespace:System;assembly=mscorlib"">
          <Rule Name=""MyXamlTask"" ToolName=""cmd.exe"">
            <StringProperty Name=""Args"" Switch=""/c echo "" />
          </Rule>
        </ProjectSchemaDefinitions>
      ]]>
    </Task>
  </UsingTask>

  <Target Name=""Build"">
    <Message Text=""Starting xaml task test..."" Importance=""High"" />
    <MyXamlTask Args=""XamlTask executed from multi-threaded build"" />
    <Message Text=""Xaml task completed"" Importance=""High"" />
  </Target>

</Project>");

                // Build with /mt flag with detailed verbosity to see task launching details
                string output = RunnerUtilities.ExecMSBuild(
                    projectFile.Path + " /t:Build /mt /v:detailed", 
                    out bool success);

                success.ShouldBeTrue(customMessage: "Build with /mt should succeed with inline xaml task");
                output.ShouldContain("Starting xaml task test",
                    customMessage: "Build should start");
                output.ShouldContain("Xaml task completed",
                    customMessage: "Xaml task should complete successfully");
                output.ShouldContain("XamlTask executed from multi-threaded build",
                    customMessage: "Xaml task should execute and output message via cmd.exe");
                
                // Verify the inline task was launched from a temporary assembly (out-of-process execution)
                output.ShouldContain(".inline_task.dll",
                    customMessage: "Xaml task should be compiled to temporary assembly for out-of-process execution");
                output.ShouldContain("external task host",
                    customMessage: "Xaml task should be launched in external task host");
            }
        }

        #endregion
    }
}
