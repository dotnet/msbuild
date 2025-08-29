// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;


using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit Tests for TaskHostConfiguration packet.
    /// </summary>
    public class TaskHostConfiguration_Tests
    {
        /// <summary>
        /// Override for ContinueOnError
        /// </summary>
        private bool _continueOnErrorDefault = true;

        /// <summary>
        /// Test that an exception is thrown when the task name is null. 
        /// </summary>
        [Fact]
        public void ConstructorWithNullName()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                TaskHostConfiguration config = new TaskHostConfiguration(
                    nodeId: 1,
                    startupDirectory: Directory.GetCurrentDirectory(),
                    buildProcessEnvironment: null,
                    culture: Thread.CurrentThread.CurrentCulture,
                    uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                    appDomainSetup:
#if FEATURE_APPDOMAIN
                    null,
#endif
                    lineNumberOfTask:
#endif
                    1,
                    columnNumberOfTask: 1,
                    projectFileOfTask: @"c:\my project\myproj.proj",
                    continueOnError: _continueOnErrorDefault,
                    taskName: null,
                    taskLocation: @"c:\my tasks\mytask.dll",
                    isTaskInputLoggingEnabled: false,
                    taskParameters: null,
                    globalParameters: null,
                    warningsAsErrors: null,
                    warningsNotAsErrors: null,
                    warningsAsMessages: null);
            });
        }
        /// <summary>
        /// Test that an exception is thrown when the task name is empty. 
        /// </summary>
        [Fact]
        public void ConstructorWithEmptyName()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                TaskHostConfiguration config = new TaskHostConfiguration(
                    nodeId: 1,
                    startupDirectory: Directory.GetCurrentDirectory(),
                    buildProcessEnvironment: null,
                    culture: Thread.CurrentThread.CurrentCulture,
                    uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                    appDomainSetup:
#if FEATURE_APPDOMAIN
                    null,
#endif
                    lineNumberOfTask:
#endif
                    1,
                    columnNumberOfTask: 1,
                    projectFileOfTask: @"c:\my project\myproj.proj",
                    continueOnError: _continueOnErrorDefault,
                    taskName: String.Empty,
                    taskLocation: @"c:\my tasks\mytask.dll",
                    isTaskInputLoggingEnabled: false,
                    taskParameters: null,
                    globalParameters: null,
                    warningsAsErrors: null,
                    warningsNotAsErrors: null,
                    warningsAsMessages: null);
            });
        }
        /// <summary>
        /// Test that an exception is thrown when the path to the task assembly is null
        /// </summary>
        [Fact]
        public void ConstructorWithNullLocation()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                TaskHostConfiguration config = new TaskHostConfiguration(
                    nodeId: 1,
                    startupDirectory: Directory.GetCurrentDirectory(),
                    buildProcessEnvironment: null,
                    culture: Thread.CurrentThread.CurrentCulture,
                    uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                    appDomainSetup:
#if FEATURE_APPDOMAIN
                    null,
#endif
                    lineNumberOfTask:
#endif
                    1,
                    columnNumberOfTask: 1,
                    projectFileOfTask: @"c:\my project\myproj.proj",
                    continueOnError: _continueOnErrorDefault,
                    taskName: "TaskName",
                    taskLocation: null,
                    isTaskInputLoggingEnabled: false,
                    taskParameters: null,
                    globalParameters: null,
                    warningsAsErrors: null,
                    warningsNotAsErrors: null,
                    warningsAsMessages: null);
            });
        }

#if !FEATURE_ASSEMBLYLOADCONTEXT
        /// <summary>
        /// Test that an exception is thrown when the path to the task assembly is empty
        /// </summary>
        [Fact]
        public void ConstructorWithEmptyLocation()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                TaskHostConfiguration config = new TaskHostConfiguration(
                    nodeId: 1,
                    startupDirectory: Directory.GetCurrentDirectory(),
                    buildProcessEnvironment: null,
                    culture: Thread.CurrentThread.CurrentCulture,
                    uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                    appDomainSetup:
#if FEATURE_APPDOMAIN
                    null,
#endif
                    lineNumberOfTask:
#endif
                    1,
                    columnNumberOfTask: 1,
                    projectFileOfTask: @"c:\my project\myproj.proj",
                    continueOnError: _continueOnErrorDefault,
                    taskName: "TaskName",
                    taskLocation: String.Empty,
                    isTaskInputLoggingEnabled: false,
                    taskParameters: null,
                    globalParameters: null,
                    warningsAsErrors: null,
                    warningsNotAsErrors: null,
                    warningsAsMessages: null);
            });
        }
#endif

        /// <summary>
        /// Test the valid constructors.  
        /// </summary>
        [Fact]
        public void TestValidConstructors()
        {
            TaskHostConfiguration config = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: null,
                globalParameters: null,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            TaskHostConfiguration config2 = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: null,
                globalParameters: null,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            IDictionary<string, object> parameters = new Dictionary<string, object>();
            TaskHostConfiguration config3 = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: parameters,
                globalParameters: null,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            IDictionary<string, object> parameters2 = new Dictionary<string, object>();
            parameters2.Add("Text", "Hello!");
            parameters2.Add("MyBoolValue", true);
            parameters2.Add("MyITaskItem", new TaskItem("ABC"));
            parameters2.Add("ItemArray", new ITaskItem[] { new TaskItem("DEF"), new TaskItem("GHI"), new TaskItem("JKL") });

            TaskHostConfiguration config4 = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: parameters2,
                globalParameters: null,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            HashSet<string> WarningsAsErrors = new HashSet<string>();
            WarningsAsErrors.Add("MSB1234");
            WarningsAsErrors.Add("MSB1235");
            WarningsAsErrors.Add("MSB1236");
            WarningsAsErrors.Add("MSB1237");

            TaskHostConfiguration config5 = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: parameters2,
                globalParameters: null,
                warningsAsErrors: WarningsAsErrors,
                warningsNotAsErrors: null,
                warningsAsMessages: null);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary is null. 
        /// </summary>
        [Fact]
        public void TestTranslationWithNullDictionary()
        {
            var expectedGlobalProperties = new Dictionary<string, string>
            {
                ["Property1"] = "Value1",
                ["Property2"] = "Value2"
            };

            TaskHostConfiguration config = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: null,
                globalParameters: expectedGlobalProperties,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.Equal(config.TaskName, deserializedConfig.TaskName);
#if !FEATURE_ASSEMBLYLOADCONTEXT
            Assert.Equal(config.TaskLocation, deserializedConfig.TaskLocation);
#endif
            Assert.Null(deserializedConfig.TaskParameters);

            Assert.Equal(expectedGlobalProperties, deserializedConfig.GlobalProperties);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary is empty. 
        /// </summary>
        [Fact]
        public void TestTranslationWithEmptyDictionary()
        {
            TaskHostConfiguration config = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: new Dictionary<string, object>(),
                globalParameters: new Dictionary<string, string>(),
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.Equal(config.TaskName, deserializedConfig.TaskName);
#if !FEATURE_ASSEMBLYLOADCONTEXT
            Assert.Equal(config.TaskLocation, deserializedConfig.TaskLocation);
#endif
            Assert.NotNull(deserializedConfig.TaskParameters);
            Assert.Equal(config.TaskParameters.Count, deserializedConfig.TaskParameters.Count);

            Assert.NotNull(deserializedConfig.GlobalProperties);
            Assert.Equal(config.GlobalProperties.Count, deserializedConfig.GlobalProperties.Count);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains just value types. 
        /// </summary>
        [Fact]
        public void TestTranslationWithValueTypesInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("Text", "Foo");
            parameters.Add("BoolValue", false);
            TaskHostConfiguration config = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: parameters,
                globalParameters: null,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.Equal(config.TaskName, deserializedConfig.TaskName);
#if !FEATURE_ASSEMBLYLOADCONTEXT
            Assert.Equal(config.TaskLocation, deserializedConfig.TaskLocation);
#endif
            Assert.NotNull(deserializedConfig.TaskParameters);
            Assert.Equal(config.TaskParameters.Count, deserializedConfig.TaskParameters.Count);
            Assert.Equal(config.TaskParameters["Text"].WrappedParameter, deserializedConfig.TaskParameters["Text"].WrappedParameter);
            Assert.Equal(config.TaskParameters["BoolValue"].WrappedParameter, deserializedConfig.TaskParameters["BoolValue"].WrappedParameter);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains an ITaskItem. 
        /// </summary>
        [Fact]
        public void TestTranslationWithITaskItemInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("TaskItemValue", new TaskItem("Foo"));
            TaskHostConfiguration config = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: parameters,
                globalParameters: null,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.Equal(config.TaskName, deserializedConfig.TaskName);
#if !FEATURE_ASSEMBLYLOADCONTEXT
            Assert.Equal(config.TaskLocation, deserializedConfig.TaskLocation);
#endif
            Assert.NotNull(deserializedConfig.TaskParameters);
            Assert.Equal(config.TaskParameters.Count, deserializedConfig.TaskParameters.Count);
            TaskHostPacketHelpers.AreEqual((ITaskItem)config.TaskParameters["TaskItemValue"].WrappedParameter, (ITaskItem)deserializedConfig.TaskParameters["TaskItemValue"].WrappedParameter);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains an ITaskItem array. 
        /// </summary>
        [Fact]
        public void TestTranslationWithITaskItemArrayInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("TaskItemArrayValue", new ITaskItem[] { new TaskItem("Foo"), new TaskItem("Baz") });
            TaskHostConfiguration config = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: parameters,
                globalParameters: null,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.Equal(config.TaskName, deserializedConfig.TaskName);
#if !FEATURE_ASSEMBLYLOADCONTEXT
            Assert.Equal(config.TaskLocation, deserializedConfig.TaskLocation);
#endif
            Assert.NotNull(deserializedConfig.TaskParameters);
            Assert.Equal(config.TaskParameters.Count, deserializedConfig.TaskParameters.Count);

            ITaskItem[] itemArray = (ITaskItem[])config.TaskParameters["TaskItemArrayValue"].WrappedParameter;
            ITaskItem[] deserializedItemArray = (ITaskItem[])deserializedConfig.TaskParameters["TaskItemArrayValue"].WrappedParameter;

            TaskHostPacketHelpers.AreEqual(itemArray, deserializedItemArray);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains an ITaskItem array. 
        /// </summary>
        [Fact]
        public void TestTranslationWithWarningsAsErrors()
        {
            HashSet<string> WarningsAsErrors = new HashSet<string>();
            WarningsAsErrors.Add("MSB1234");
            WarningsAsErrors.Add("MSB1235");
            WarningsAsErrors.Add("MSB1236");
            WarningsAsErrors.Add("MSB1237");
            TaskHostConfiguration config = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: null,
                globalParameters: null,
                warningsAsErrors: WarningsAsErrors,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.Equal(config.TaskName, deserializedConfig.TaskName);
#if !FEATURE_ASSEMBLYLOADCONTEXT
            Assert.Equal(config.TaskLocation, deserializedConfig.TaskLocation);
#endif
            Assert.NotNull(deserializedConfig.WarningsAsErrors);
            config.WarningsAsErrors.SequenceEqual(deserializedConfig.WarningsAsErrors, StringComparer.Ordinal).ShouldBeTrue();
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains warningsasmessages
        /// </summary>
        [Fact]
        public void TestTranslationWithWarningsAsMessages()
        {
            HashSet<string> WarningsAsMessages = new HashSet<string>();
            WarningsAsMessages.Add("MSB1234");
            WarningsAsMessages.Add("MSB1235");
            WarningsAsMessages.Add("MSB1236");
            WarningsAsMessages.Add("MSB1237");
            TaskHostConfiguration config = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup:
#if FEATURE_APPDOMAIN
                null,
#endif
                lineNumberOfTask:
#endif
                1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: null,
                globalParameters: null,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: WarningsAsMessages);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.NotNull(deserializedConfig.WarningsAsMessages);
            config.WarningsAsMessages.SequenceEqual(deserializedConfig.WarningsAsMessages, StringComparer.Ordinal).ShouldBeTrue();
        }

        /// <summary>
        /// Helper methods for testing the task host-related packets. 
        /// </summary>
        internal static class TaskHostPacketHelpers
        {
            /// <summary>
            /// Asserts the equality (or lack thereof) of two arrays of ITaskItems.
            /// </summary>
            internal static void AreEqual(ITaskItem[] x, ITaskItem[] y)
            {
                if (x == null && y == null)
                {
                    return;
                }

                if (x == null || y == null)
                {
                    Assert.True(false, "The two item lists are not equal -- one of them is null");
                }

                if (x.Length != y.Length)
                {
                    Assert.True(false, "The two item lists have different lengths, so they cannot be equal");
                }

                for (int i = 0; i < x.Length; i++)
                {
                    AreEqual(x[i], y[i]);
                }
            }

            /// <summary>
            /// Asserts the equality (or lack thereof) of two ITaskItems.
            /// </summary>
            internal static void AreEqual(ITaskItem x, ITaskItem y)
            {
                if (x == null && y == null)
                {
                    return;
                }

                if (x == null || y == null)
                {
                    Assert.True(false, "The two items are not equal -- one of them is null");
                }

                Assert.Equal(x.ItemSpec, y.ItemSpec);

                IDictionary metadataFromX = x.CloneCustomMetadata();
                IDictionary metadataFromY = y.CloneCustomMetadata();

                Assert.Equal(metadataFromX.Count, metadataFromY.Count);

                foreach (object metadataName in metadataFromX.Keys)
                {
                    if (!metadataFromY.Contains(metadataName))
                    {
                        Assert.True(false, string.Format("Only one item contains the '{0}' metadata", metadataName));
                    }
                    else
                    {
                        Assert.Equal(metadataFromX[metadataName], metadataFromY[metadataName]);
                    }
                }
            }
        }
    }
}
