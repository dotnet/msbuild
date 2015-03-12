// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit Tests for TaskHostConfiguration packet.</summary>
//-----------------------------------------------------------------------

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit Tests for TaskHostConfiguration packet.
    /// </summary>
    [TestClass]
    public class TaskHostConfiguration_Tests
    {
        /// <summary>
        /// Override for ContinueOnError
        /// </summary>
        private bool _continueOnErrorDefault = true;

        /// <summary>
        /// Test that an exception is thrown when the task name is null. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void ConstructorWithNullName()
        {
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, null, @"c:\my tasks\mytask.dll", null);
        }

        /// <summary>
        /// Test that an exception is thrown when the task name is empty. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void ConstructorWithEmptyName()
        {
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, String.Empty, @"c:\my tasks\mytask.dll", null);
        }

        /// <summary>
        /// Test that an exception is thrown when the path to the task assembly is null
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void ConstructorWithNullLocation()
        {
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", null, null);
        }

        /// <summary>
        /// Test that an exception is thrown when the path to the task assembly is empty
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void ConstructorWithEmptyLocation()
        {
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", String.Empty, null);
        }

        /// <summary>
        /// Test the valid constructors.  
        /// </summary>
        [TestMethod]
        public void TestValidConstructors()
        {
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", @"c:\MyTasks\MyTask.dll", null);
            TaskHostConfiguration config2 = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", @"c:\MyTasks\MyTask.dll", null);

            IDictionary<string, object> parameters = new Dictionary<string, object>();
            TaskHostConfiguration config3 = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", @"c:\MyTasks\MyTask.dll", parameters);

            IDictionary<string, object> parameters2 = new Dictionary<string, object>();
            parameters2.Add("Text", "Hello!");
            parameters2.Add("MyBoolValue", true);
            parameters2.Add("MyITaskItem", new TaskItem("ABC"));
            parameters2.Add("ItemArray", new ITaskItem[] { new TaskItem("DEF"), new TaskItem("GHI"), new TaskItem("JKL") });

            TaskHostConfiguration config4 = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", @"c:\MyTasks\MyTask.dll", parameters2);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary is null. 
        /// </summary>
        [TestMethod]
        public void TestTranslationWithNullDictionary()
        {
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", @"c:\MyTasks\MyTask.dll", null);

            ((INodePacketTranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.AreEqual(config.TaskName, deserializedConfig.TaskName);
            Assert.AreEqual(config.TaskLocation, config.TaskLocation);
            Assert.IsNull(deserializedConfig.TaskParameters);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary is empty. 
        /// </summary>
        [TestMethod]
        public void TestTranslationWithEmptyDictionary()
        {
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", @"c:\MyTasks\MyTask.dll", new Dictionary<string, object>());

            ((INodePacketTranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.AreEqual(config.TaskName, deserializedConfig.TaskName);
            Assert.AreEqual(config.TaskLocation, config.TaskLocation);
            Assert.IsNotNull(deserializedConfig.TaskParameters);
            Assert.AreEqual(config.TaskParameters.Count, deserializedConfig.TaskParameters.Count);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains just value types. 
        /// </summary>
        [TestMethod]
        public void TestTranslationWithValueTypesInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("Text", "Foo");
            parameters.Add("BoolValue", false);
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", @"c:\MyTasks\MyTask.dll", parameters);

            ((INodePacketTranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.AreEqual(config.TaskName, deserializedConfig.TaskName);
            Assert.AreEqual(config.TaskLocation, config.TaskLocation);
            Assert.IsNotNull(deserializedConfig.TaskParameters);
            Assert.AreEqual(config.TaskParameters.Count, deserializedConfig.TaskParameters.Count);
            Assert.AreEqual(config.TaskParameters["Text"].WrappedParameter, deserializedConfig.TaskParameters["Text"].WrappedParameter);
            Assert.AreEqual(config.TaskParameters["BoolValue"].WrappedParameter, deserializedConfig.TaskParameters["BoolValue"].WrappedParameter);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains an ITaskItem. 
        /// </summary>
        [TestMethod]
        public void TestTranslationWithITaskItemInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("TaskItemValue", new TaskItem("Foo"));
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", @"c:\MyTasks\MyTask.dll", parameters);

            ((INodePacketTranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.AreEqual(config.TaskName, deserializedConfig.TaskName);
            Assert.AreEqual(config.TaskLocation, config.TaskLocation);
            Assert.IsNotNull(deserializedConfig.TaskParameters);
            Assert.AreEqual(config.TaskParameters.Count, deserializedConfig.TaskParameters.Count);
            TaskHostPacketHelpers.AreEqual((ITaskItem)config.TaskParameters["TaskItemValue"].WrappedParameter, (ITaskItem)deserializedConfig.TaskParameters["TaskItemValue"].WrappedParameter);
        }

        /// <summary>
        /// Test serialization / deserialization when the parameter dictionary contains an ITaskItem array. 
        /// </summary>
        [TestMethod]
        public void TestTranslationWithITaskItemArrayInDictionary()
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("TaskItemArrayValue", new ITaskItem[] { new TaskItem("Foo"), new TaskItem("Baz") });
            TaskHostConfiguration config = new TaskHostConfiguration(1, Environment.CurrentDirectory, null, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture, null, 1, 1, @"c:\my project\myproj.proj", _continueOnErrorDefault, "TaskName", @"c:\MyTasks\MyTask.dll", parameters);

            ((INodePacketTranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            TaskHostConfiguration deserializedConfig = packet as TaskHostConfiguration;

            Assert.AreEqual(config.TaskName, deserializedConfig.TaskName);
            Assert.AreEqual(config.TaskLocation, config.TaskLocation);
            Assert.IsNotNull(deserializedConfig.TaskParameters);
            Assert.AreEqual(config.TaskParameters.Count, deserializedConfig.TaskParameters.Count);

            ITaskItem[] itemArray = (ITaskItem[])config.TaskParameters["TaskItemArrayValue"].WrappedParameter;
            ITaskItem[] deserializedItemArray = (ITaskItem[])deserializedConfig.TaskParameters["TaskItemArrayValue"].WrappedParameter;

            TaskHostPacketHelpers.AreEqual(itemArray, deserializedItemArray);
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

                Assert.AreEqual(x.ItemSpec, y.ItemSpec);

                IDictionary metadataFromX = x.CloneCustomMetadata();
                IDictionary metadataFromY = y.CloneCustomMetadata();

                if (x == null && y == null)
                {
                    return;
                }

                if (x == null || y == null)
                {
                    Assert.Fail("The two items are not equal -- one of them is null");
                }

                Assert.AreEqual(metadataFromX.Count, metadataFromY.Count);

                foreach (object metadataName in metadataFromX.Keys)
                {
                    if (!metadataFromY.Contains(metadataName))
                    {
                        Assert.Fail("Only one item contains the '{0}' metadata", metadataName.ToString());
                    }
                    else
                    {
                        Assert.AreEqual(metadataFromX[metadataName], metadataFromY[metadataName]);
                    }
                }
            }
        }
    }
}
