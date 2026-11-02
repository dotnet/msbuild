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
                    hostServices: null,
#if FEATURE_APPDOMAIN
                    appDomainSetup: null,
#endif
                    lineNumberOfTask: 1,
                    columnNumberOfTask: 1,
                    projectFileOfTask: @"c:\my project\myproj.proj",
                    targetName: "",
                    projectFile: "proj.proj",
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
                    hostServices: null,
#if FEATURE_APPDOMAIN
                    appDomainSetup: null,
#endif
                    lineNumberOfTask: 1,
                    columnNumberOfTask: 1,
                    projectFileOfTask: @"c:\my project\myproj.proj",
                    targetName: "",
                    projectFile: "proj.proj",
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
                    hostServices: null,
#if FEATURE_APPDOMAIN
                    appDomainSetup: null,
#endif
                    lineNumberOfTask: 1,
                    columnNumberOfTask: 1,
                    projectFileOfTask: @"c:\my project\myproj.proj",
                    targetName: "",
                    projectFile: "proj.proj",
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
                    hostServices: null,
#if FEATURE_APPDOMAIN
                    appDomainSetup: null,
#endif
                    lineNumberOfTask: 1,
                    columnNumberOfTask: 1,
                    projectFileOfTask: @"c:\my project\myproj.proj",
                    targetName: "",
                    projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Test serialization / deserialization of the AppDomainSetup instance.
        /// </summary>
        [Theory]
        [InlineData(new byte[] { 1, 2, 3 })]
        [InlineData(null)]
        public void TestTranslationWithAppDomainSetup(byte[] configBytes)
        {
            AppDomainSetup setup = new AppDomainSetup();

            TaskHostConfiguration config = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: null,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
                hostServices: null,
                appDomainSetup: setup,
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: new Dictionary<string, object>(),
                globalParameters: new Dictionary<string, string>(),
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            setup.SetConfigurationBytes(configBytes);

            // Set version to 0 for CLR4 (Framework-to-Framework) communication which supports AppDomain.
            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            writeTranslator.NegotiatedPacketVersion = 0;
            ((ITranslatable)config).Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            readTranslator.NegotiatedPacketVersion = 0;
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(readTranslator);

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            deserializedConfig.AppDomainSetup.ShouldNotBeNull();

            if (configBytes is null)
            {
                deserializedConfig.AppDomainSetup.GetConfigurationBytes().ShouldBeNull();
            }
            else
            {
                deserializedConfig.AppDomainSetup.GetConfigurationBytes().SequenceEqual(configBytes).ShouldBeTrue();
            }
        }
#endif

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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
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
        /// With the environment-delta wire format (packet version >= 5) and the default
        /// <see cref="InvariantPayloadTransferMode.Full"/> mode, the build process environment
        /// is serialized in full and round-trips correctly.
        /// </summary>
        [Fact]
        public void TestTranslationEnvironmentFullRoundTripsAtVersion5()
        {
            Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PATH"] = @"c:\windows;c:\windows\system32",
                ["NUMBER_OF_PROCESSORS"] = "8",
            };

            TaskHostConfiguration config = CreateConfigurationWithEnvironment(environment);
            config.EnvironmentMode.ShouldBe(InvariantPayloadTransferMode.Full);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            writeTranslator.NegotiatedPacketVersion = 5;
            ((ITranslatable)config).Translate(writeTranslator);
            long version5Length = TranslationHelpers.GetWriteStreamLength();

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            readTranslator.NegotiatedPacketVersion = 5;
            TaskHostConfiguration deserializedConfig = (TaskHostConfiguration)TaskHostConfiguration.FactoryForDeserialization(readTranslator);

            deserializedConfig.EnvironmentMode.ShouldBe(InvariantPayloadTransferMode.Full);
            deserializedConfig.BuildProcessEnvironment.ShouldNotBeNull();
            deserializedConfig.BuildProcessEnvironment.Count.ShouldBe(environment.Count);
            deserializedConfig.BuildProcessEnvironment["PATH"].ShouldBe(environment["PATH"]);
            deserializedConfig.BuildProcessEnvironment["NUMBER_OF_PROCESSORS"].ShouldBe("8");

            // Prove the v5 Full path actually emitted the one-byte EnvironmentMode marker rather than silently
            // falling back to the legacy full-dictionary format: the same config serialized at a pre-delta
            // version (no marker) is strictly smaller on the wire.
            TaskHostConfiguration legacyConfig = CreateConfigurationWithEnvironment(environment);
            ITranslator legacyWriter = TranslationHelpers.GetWriteTranslator();
            legacyWriter.NegotiatedPacketVersion = 4;
            ((ITranslatable)legacyConfig).Translate(legacyWriter);
            TranslationHelpers.GetWriteStreamLength().ShouldBeLessThan(version5Length);
        }

        /// <summary>
        /// With the environment-delta wire format (packet version >= 5) and
        /// <see cref="InvariantPayloadTransferMode.Identical"/> mode, the build process environment
        /// dictionary is omitted from the wire (saving bytes) and the receiver reconstructs it from the
        /// connection's baseline via <see cref="TaskHostConfiguration.SetResolvedBuildProcessEnvironment"/>.
        /// </summary>
        [Fact]
        public void TestTranslationEnvironmentIdenticalOmitsDictionaryAtVersion5()
        {
            Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PATH"] = @"c:\windows;c:\windows\system32",
                ["TEMP"] = @"c:\temp",
            };

            // Serialize the same environment both ways to prove the "Identical" form is smaller on the wire.
            TaskHostConfiguration fullConfig = CreateConfigurationWithEnvironment(environment);
            ITranslator fullWriter = TranslationHelpers.GetWriteTranslator();
            fullWriter.NegotiatedPacketVersion = 5;
            ((ITranslatable)fullConfig).Translate(fullWriter);
            long fullLength = TranslationHelpers.GetWriteStreamLength();

            TaskHostConfiguration identicalConfig = CreateConfigurationWithEnvironment(environment);
            identicalConfig.EnvironmentMode = InvariantPayloadTransferMode.Identical;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            writeTranslator.NegotiatedPacketVersion = 5;
            ((ITranslatable)identicalConfig).Translate(writeTranslator);
            TranslationHelpers.GetWriteStreamLength().ShouldBeLessThan(fullLength);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            readTranslator.NegotiatedPacketVersion = 5;
            TaskHostConfiguration deserializedConfig = (TaskHostConfiguration)TaskHostConfiguration.FactoryForDeserialization(readTranslator);

            // The dictionary was not on the wire, so it is null until reconstructed from the baseline.
            deserializedConfig.EnvironmentMode.ShouldBe(InvariantPayloadTransferMode.Identical);
            deserializedConfig.BuildProcessEnvironment.ShouldBeNull();

            deserializedConfig.SetResolvedBuildProcessEnvironment(environment);
            deserializedConfig.BuildProcessEnvironment.ShouldBe(environment);
        }

        /// <summary>
        /// With the global-properties delta wire format (packet version >= 5) and the default
        /// <see cref="InvariantPayloadTransferMode.Full"/> mode, the whole global-properties dictionary
        /// (including CurrentSolutionConfigurationContents) is serialized and round-trips correctly.
        /// </summary>
        [Fact]
        public void TestTranslationGlobalPropertiesFullRoundTripsAtVersion5()
        {
            const string solutionConfigValue = "<SolutionConfiguration><ProjectConfiguration>Debug|AnyCPU</ProjectConfiguration></SolutionConfiguration>";
            Dictionary<string, string> globalProperties = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Configuration"] = "Debug",
                ["CurrentSolutionConfigurationContents"] = solutionConfigValue,
            };

            TaskHostConfiguration config = CreateConfigurationWithGlobalProperties(globalProperties);
            config.GlobalParametersMode.ShouldBe(InvariantPayloadTransferMode.Full);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            writeTranslator.NegotiatedPacketVersion = 5;
            ((ITranslatable)config).Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            readTranslator.NegotiatedPacketVersion = 5;
            TaskHostConfiguration deserializedConfig = (TaskHostConfiguration)TaskHostConfiguration.FactoryForDeserialization(readTranslator);

            deserializedConfig.GlobalParametersMode.ShouldBe(InvariantPayloadTransferMode.Full);
            deserializedConfig.GlobalProperties.ShouldNotBeNull();
            deserializedConfig.GlobalProperties.Count.ShouldBe(globalProperties.Count);
            deserializedConfig.GlobalProperties["Configuration"].ShouldBe("Debug");
            deserializedConfig.GlobalProperties["CurrentSolutionConfigurationContents"].ShouldBe(solutionConfigValue);
        }

        /// <summary>
        /// With the global-properties delta wire format (packet version >= 5) and
        /// <see cref="InvariantPayloadTransferMode.Identical"/> mode, the whole global-properties dictionary is
        /// omitted from the wire (saving bytes) and the receiver reconstructs it from the connection's baseline
        /// via <see cref="TaskHostConfiguration.SetResolvedGlobalParameters"/>.
        /// </summary>
        [Fact]
        public void TestTranslationGlobalPropertiesIdenticalOmitsDictionaryAtVersion5()
        {
            // A large, representative blob so the "Identical" form is clearly smaller on the wire.
            string solutionConfigValue = "<SolutionConfiguration>" + string.Concat(Enumerable.Repeat(
                "<ProjectConfiguration Project=\"{GUID}\" AbsolutePath=\"c:\\repo\\project.csproj\">Debug|AnyCPU</ProjectConfiguration>", 50)) + "</SolutionConfiguration>";
            Dictionary<string, string> globalProperties = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Configuration"] = "Debug",
                ["CurrentSolutionConfigurationContents"] = solutionConfigValue,
            };

            // Serialize the same config both ways to prove the "Identical" form is smaller on the wire.
            TaskHostConfiguration fullConfig = CreateConfigurationWithGlobalProperties(globalProperties);
            ITranslator fullWriter = TranslationHelpers.GetWriteTranslator();
            fullWriter.NegotiatedPacketVersion = 5;
            ((ITranslatable)fullConfig).Translate(fullWriter);
            long fullLength = TranslationHelpers.GetWriteStreamLength();

            TaskHostConfiguration identicalConfig = CreateConfigurationWithGlobalProperties(globalProperties);
            identicalConfig.GlobalParametersMode = InvariantPayloadTransferMode.Identical;

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            writeTranslator.NegotiatedPacketVersion = 5;
            ((ITranslatable)identicalConfig).Translate(writeTranslator);
            TranslationHelpers.GetWriteStreamLength().ShouldBeLessThan(fullLength);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            readTranslator.NegotiatedPacketVersion = 5;
            TaskHostConfiguration deserializedConfig = (TaskHostConfiguration)TaskHostConfiguration.FactoryForDeserialization(readTranslator);

            // The dictionary was not on the wire, so it is null until reconstructed from the baseline.
            deserializedConfig.GlobalParametersMode.ShouldBe(InvariantPayloadTransferMode.Identical);
            deserializedConfig.GlobalProperties.ShouldBeNull();

            deserializedConfig.SetResolvedGlobalParameters(globalProperties);
            deserializedConfig.GlobalProperties["CurrentSolutionConfigurationContents"].ShouldBe(solutionConfigValue);
        }

        /// <summary>
        /// For a negotiated packet version below 5 (a legacy peer, e.g. a CLR2/net35 or otherwise older task host)
        /// the delta wire format is not used: both the build process environment and the global properties are
        /// serialized in the legacy full-dictionary format. This guards the Math.Min version-negotiation fallback
        /// so older hosts keep working.
        /// </summary>
        [Fact]
        public void TestTranslationLegacyVersionKeepsFullDictionaryFormat()
        {
            const string solutionConfigValue = "<SolutionConfiguration><ProjectConfiguration>Debug|AnyCPU</ProjectConfiguration></SolutionConfiguration>";
            Dictionary<string, string> globalProperties = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Configuration"] = "Debug",
                ["CurrentSolutionConfigurationContents"] = solutionConfigValue,
            };

            TaskHostConfiguration config = CreateConfigurationWithGlobalProperties(globalProperties);

            ITranslator writeTranslator = TranslationHelpers.GetWriteTranslator();
            writeTranslator.NegotiatedPacketVersion = 4;
            ((ITranslatable)config).Translate(writeTranslator);

            ITranslator readTranslator = TranslationHelpers.GetReadTranslator();
            readTranslator.NegotiatedPacketVersion = 4;
            TaskHostConfiguration deserializedConfig = (TaskHostConfiguration)TaskHostConfiguration.FactoryForDeserialization(readTranslator);

            // Legacy format carries the whole global-properties dictionary (including the solution-config blob).
            deserializedConfig.GlobalProperties.ShouldNotBeNull();
            deserializedConfig.GlobalProperties["CurrentSolutionConfigurationContents"].ShouldBe(solutionConfigValue);
            deserializedConfig.GlobalProperties["Configuration"].ShouldBe("Debug");

            // The environment round-trips in full (CreateConfigurationWithGlobalProperties seeds it with PATH).
            deserializedConfig.BuildProcessEnvironment.ShouldNotBeNull();
            deserializedConfig.BuildProcessEnvironment["PATH"].ShouldBe(@"c:\windows");
        }

        private TaskHostConfiguration CreateConfigurationWithGlobalProperties(Dictionary<string, string> globalProperties)
        {
            return new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = @"c:\windows" },
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: new Dictionary<string, object>(),
                globalParameters: globalProperties,
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);
        }

        private TaskHostConfiguration CreateConfigurationWithEnvironment(Dictionary<string, string> environment)
        {
            return new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Directory.GetCurrentDirectory(),
                buildProcessEnvironment: environment,
                culture: Thread.CurrentThread.CurrentCulture,
                uiCulture: Thread.CurrentThread.CurrentUICulture,
                hostServices: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: @"c:\my project\myproj.proj",
                targetName: "TargetName",
                projectFile: "proj.proj",
                continueOnError: _continueOnErrorDefault,
                taskName: "TaskName",
                taskLocation: @"c:\MyTasks\MyTask.dll",
                isTaskInputLoggingEnabled: false,
                taskParameters: new Dictionary<string, object>(),
                globalParameters: new Dictionary<string, string>(),
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);
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
                    Assert.Fail("The two item lists are not equal -- one of them is null");
                }

                if (x.Length != y.Length)
                {
                    Assert.Fail("The two item lists have different lengths, so they cannot be equal");
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
                    Assert.Fail("The two items are not equal -- one of them is null");
                }

                Assert.Equal(x.ItemSpec, y.ItemSpec);

                IDictionary metadataFromX = x.CloneCustomMetadata();
                IDictionary metadataFromY = y.CloneCustomMetadata();

                Assert.Equal(metadataFromX.Count, metadataFromY.Count);

                foreach (object metadataName in metadataFromX.Keys)
                {
                    if (!metadataFromY.Contains(metadataName))
                    {
                        Assert.Fail(string.Format("Only one item contains the '{0}' metadata", metadataName));
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
