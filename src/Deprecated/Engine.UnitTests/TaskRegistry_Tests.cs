// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class TaskRegistry_Tests
    {
        [Test]
        public void RegisterTaskSimple()
        {
            UsingTaskInfo[] taskInfos = { new UsingTaskInfo("CustomTask", "CustomTask, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", null) };

            TaskRegistry registryStub = TaskRegistryHelperMethods.CreateTaskRegistryAndRegisterTasks(taskInfos);

            int registeredTaskCount = TaskRegistryHelperMethods.GetDeepCountOfRegisteredTasks(registryStub.AllTaskDeclarations);
            Assert.AreEqual(1, registeredTaskCount, "Expected only one registered task in TaskRegistry.AllTaskDeclarations!");

            ArrayList taskAssemblyLoadInfoList = (ArrayList)registryStub.AllTaskDeclarations[taskInfos[0].TaskName];
            Assert.IsNotNull(taskAssemblyLoadInfoList, "Task AssemblyLoadInfo not found in TaskRegistry.AllTaskDeclarations!");
            Assert.AreEqual(1, taskAssemblyLoadInfoList.Count, "Expected only one AssemblyLoadInfo registered!");

            AssemblyLoadInfo taskAssemblyLoadInfo = (AssemblyLoadInfo)taskAssemblyLoadInfoList[0];
            Assert.AreEqual(taskAssemblyLoadInfo, taskInfos[0].AssemblyInfo, "Task AssemblyLoadInfo was not properly registered by TaskRegistry.RegisterTask!");
        }

        [Test]
        public void RegisterMultipleTasksWithDifferentNames()
        {
            UsingTaskInfo[] taskInfos = { new UsingTaskInfo("CustomTask", "CustomTask, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", null),
                                          new UsingTaskInfo("YetAnotherCustomTask", null, "bin\\Assemblies\\YetAnotherCustomTask.dll"),
                                          new UsingTaskInfo("AnotherCustomTask", "AnotherCustomTask, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null", null) };

            TaskRegistry registryStub = TaskRegistryHelperMethods.CreateTaskRegistryAndRegisterTasks(taskInfos);

            int registeredTaskCount = TaskRegistryHelperMethods.GetDeepCountOfRegisteredTasks(registryStub.AllTaskDeclarations);
            Assert.AreEqual(3, registeredTaskCount, "Expected three registered tasks in TaskRegistry.AllTaskDeclarations!");

            foreach (UsingTaskInfo taskInfo in taskInfos)
            {
                ArrayList taskAssemblyLoadInfoList = (ArrayList)registryStub.AllTaskDeclarations[taskInfo.TaskName];
                Assert.IsNotNull(taskAssemblyLoadInfoList, "Task AssemblyLoadInfo not found in TaskRegistry.AllTaskDeclarations!");
                Assert.AreEqual(1, taskAssemblyLoadInfoList.Count, "Expected only one AssemblyLoadInfo registered under this TaskName!");

                AssemblyLoadInfo taskAssemblyLoadInfo = (AssemblyLoadInfo)taskAssemblyLoadInfoList[0];
                Assert.AreEqual(taskAssemblyLoadInfo, taskInfo.AssemblyInfo, "Task AssemblyLoadInfo was not properly registered by TaskRegistry.RegisterTask!");
            }
        }

        [Test]
        public void RegisterMultipleTasksSomeWithSameName()
        {
            UsingTaskInfo[] taskInfos = { new UsingTaskInfo("CustomTask", "CustomTask, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", null),
                                          new UsingTaskInfo("YetAnotherCustomTask", "YetAnotherCustomTask, Version=9.0.0.0, Culture=neutral, PublicKeyToken=null", null),
                                          new UsingTaskInfo("CustomTask", "CustomTask, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null", null) };

            TaskRegistry registryStub = TaskRegistryHelperMethods.CreateTaskRegistryAndRegisterTasks(taskInfos);

            //First assert that there are two unique buckets
            Assert.AreEqual(2, registryStub.AllTaskDeclarations.Count, "Expected only two buckets since two of three tasks have the same name!");

            //Now let's look at the bucket with only one task
            ArrayList singletonBucket = (ArrayList)registryStub.AllTaskDeclarations[taskInfos[1].TaskName];
            Assert.IsNotNull(singletonBucket, "Task AssemblyLoadInfo not found in TaskRegistry.AllTaskDeclarations!");
            Assert.AreEqual(1, singletonBucket.Count, "Expected only one AssemblyLoadInfo registered under this TaskName!");
            AssemblyLoadInfo singletonAssemblyLoadInfo = (AssemblyLoadInfo)singletonBucket[0];
            Assert.AreEqual(singletonAssemblyLoadInfo, taskInfos[1].AssemblyInfo, "Task AssemblyLoadInfo was not properly registered by TaskRegistry.RegisterTask!");

            //Finally let's look at the bucket with two tasks
            ArrayList duplicateBucket = (ArrayList)registryStub.AllTaskDeclarations[taskInfos[0].TaskName];
            Assert.IsNotNull(duplicateBucket, "Task AssemblyLoadInfo not found in TaskRegistry.AllTaskDeclarations!");
            Assert.AreEqual(2, duplicateBucket.Count, "Expected two AssemblyLoadInfos registered under this TaskName!");
            bool bothTaskVersionsFound = duplicateBucket.Contains(taskInfos[0].AssemblyInfo) && duplicateBucket.Contains(taskInfos[2].AssemblyInfo);
            Assert.IsTrue(bothTaskVersionsFound, "Expected both versions of the task to be registered in this bucket!");
        }

        [Test]
        public void RegisterMultipleTasksWithDifferentNamesFromSameAssembly()
        {
            UsingTaskInfo[] taskInfos = { new UsingTaskInfo("CustomTask", "CustomTasks, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", null),
                                          new UsingTaskInfo("YetAnotherCustomTask", null, "bin\\Assemblies\\YetAnotherCustomTask.dll"),
                                          new UsingTaskInfo("AnotherCustomTask", "CustomTasks, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", null) };

            TaskRegistry registryStub = TaskRegistryHelperMethods.CreateTaskRegistryAndRegisterTasks(taskInfos);

            int registeredTaskCount = TaskRegistryHelperMethods.GetDeepCountOfRegisteredTasks(registryStub.AllTaskDeclarations);
            Assert.AreEqual(3, registeredTaskCount, "Expected three registered tasks in TaskRegistry.AllTaskDeclarations!");

            foreach (UsingTaskInfo taskInfo in taskInfos)
            {
                ArrayList taskAssemblyLoadInfoList = (ArrayList)registryStub.AllTaskDeclarations[taskInfo.TaskName];
                Assert.IsNotNull(taskAssemblyLoadInfoList, "Task AssemblyLoadInfo not found in TaskRegistry.AllTaskDeclarations!");
                Assert.AreEqual(1, taskAssemblyLoadInfoList.Count, "Expected only one AssemblyLoadInfo registered under this TaskName!");

                AssemblyLoadInfo taskAssemblyLoadInfo = (AssemblyLoadInfo)taskAssemblyLoadInfoList[0];
                Assert.AreEqual(taskAssemblyLoadInfo, taskInfo.AssemblyInfo, "Task AssemblyLoadInfo was not properly registered by TaskRegistry.RegisterTask!");
            }
        }

        [Test]
        public void RegisterMultipleTasksWithSameNameAndSameAssembly()
        {
            UsingTaskInfo[] taskInfos = { new UsingTaskInfo("CustomTask", null, "Some\\Relative\\Path\\CustomTasks.dll"),
                                          new UsingTaskInfo("YetAnotherCustomTask", "YetAnotherCustomTask, Version=9.0.0.0, Culture=neutral, PublicKeyToken=null", null),
                                          new UsingTaskInfo("CustomTask", null, "Some\\Relative\\Path\\CustomTasks.dll") };

            TaskRegistry registryStub = TaskRegistryHelperMethods.CreateTaskRegistryAndRegisterTasks(taskInfos);

            //two unique buckets
            Assert.AreEqual(2, registryStub.AllTaskDeclarations.Count, "Expected only two buckets since two of three tasks have the same name!");
            //three total tasks
            Assert.AreEqual(3, TaskRegistryHelperMethods.GetDeepCountOfRegisteredTasks(registryStub.AllTaskDeclarations), "Expected three total tasks registered in TaskRegistry!");
        }

        [Test]
        public void AllUsingTaskAttributesAreExpanded()
        {
            UsingTaskInfo[] taskInfos = { new UsingTaskInfo("$(Property1)@(ThirdItem)$(Property2)", null, "Some\\$(Property3)\\Path\\CustomTasks.dll"),
                                          new UsingTaskInfo("YetAnotherCustomTask", "$(Property4)@(ThirdItem), Version=9.0.0.0, Culture=neutral, PublicKeyToken=null", null),
                                          new UsingTaskInfo("Custom$(Property5)Task", null, "Some\\Relative\\Path\\CustomTasks.dll", "'@(ThirdItem)$(Property1)' == 'ThirdValue1Value1'") };

            TaskRegistry registryStub = TaskRegistryHelperMethods.CreateTaskRegistryAndRegisterTasks(taskInfos);

            int registeredTaskCount = TaskRegistryHelperMethods.GetDeepCountOfRegisteredTasks(registryStub.AllTaskDeclarations);
            Assert.AreEqual(3, registeredTaskCount, "Expected three registered tasks in TaskRegistry.AllTaskDeclarations!");

            foreach (UsingTaskInfo taskInfo in taskInfos)
            {
                UsingTaskInfo expandedTaskInfo = taskInfo.CopyAndExpand(TaskRegistryHelperMethods.RegistryExpander);
                ArrayList taskAssemblyLoadInfoList = (ArrayList)registryStub.AllTaskDeclarations[expandedTaskInfo.TaskName];
                Assert.IsNotNull(taskAssemblyLoadInfoList, "Task AssemblyLoadInfo not found in TaskRegistry.AllTaskDeclarations!");
                Assert.AreEqual(1, taskAssemblyLoadInfoList.Count, "Expected only one AssemblyLoadInfo registered under this TaskName!");

                AssemblyLoadInfo taskAssemblyLoadInfo = (AssemblyLoadInfo)taskAssemblyLoadInfoList[0];
                Assert.AreEqual(taskAssemblyLoadInfo, expandedTaskInfo.AssemblyInfo, "Task AssemblyLoadInfo was not properly registered by TaskRegistry.RegisterTask!");
            }
        }

        [Test]
        public void TaskRegisteredOnlyIfConditionIsTrue()
        {
            UsingTaskInfo[] taskInfos = { new UsingTaskInfo("$(Property1)@(ThirdItem)$(Property2)", null, "Some\\$(Property3)\\Path\\CustomTasks.dll", "'true' != 'false'"),
                                          new UsingTaskInfo("YetAnotherCustomTask", "$(Property4)@(ThirdItem), Version=9.0.0.0, Culture=neutral, PublicKeyToken=null", null, "false"),
                                          new UsingTaskInfo("Custom$(Property5)Task", null, "Some\\Relative\\Path\\CustomTasks.dll", "'@(ThirdItem)$(Property1)' == 'ThirdValue1Value1'"),
                                          new UsingTaskInfo("MyTask", null, "TasksAssembly.dll", "'@(ThirdItem)$(Property1)' == 'ThirdValue1'") };

            TaskRegistry registryStub = TaskRegistryHelperMethods.CreateTaskRegistryAndRegisterTasks(taskInfos);

            int registeredTaskCount = TaskRegistryHelperMethods.GetDeepCountOfRegisteredTasks(registryStub.AllTaskDeclarations);
            Assert.AreEqual(2, registeredTaskCount, "Expected two registered tasks in TaskRegistry.AllTaskDeclarations!");

            for (int i = 0; i <= 2; i += 2)
            {
                UsingTaskInfo expandedTaskInfo = taskInfos[i].CopyAndExpand(TaskRegistryHelperMethods.RegistryExpander);
                ArrayList taskAssemblyLoadInfoList = (ArrayList)registryStub.AllTaskDeclarations[expandedTaskInfo.TaskName];
                Assert.IsNotNull(taskAssemblyLoadInfoList, "Task AssemblyLoadInfo not found in TaskRegistry.AllTaskDeclarations!");
                Assert.AreEqual(1, taskAssemblyLoadInfoList.Count, "Expected only one AssemblyLoadInfo registered under this TaskName!");

                AssemblyLoadInfo taskAssemblyLoadInfo = (AssemblyLoadInfo)taskAssemblyLoadInfoList[0];
                Assert.AreEqual(taskAssemblyLoadInfo, expandedTaskInfo.AssemblyInfo, "Task AssemblyLoadInfo was not properly registered by TaskRegistry.RegisterTask!");
            }
        }

        [Test]
        public void TasksNoLongerRegisteredAfterClearCalled()
        {
            UsingTaskInfo[] taskInfos = { new UsingTaskInfo("CustomTask", "CustomTask, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", null),
                                          new UsingTaskInfo("YetAnotherCustomTask", "YetAnotherCustomTask, Version=9.0.0.0, Culture=neutral, PublicKeyToken=null", null),
                                          new UsingTaskInfo("MyCustomTask", "CustomTask, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null", null) };

            TaskRegistry registryStub = TaskRegistryHelperMethods.CreateTaskRegistryAndRegisterTasks(taskInfos);

            int registeredTaskCount = TaskRegistryHelperMethods.GetDeepCountOfRegisteredTasks(registryStub.AllTaskDeclarations);
            Assert.AreEqual(3, registeredTaskCount, "Expected three registered tasks in TaskRegistry.AllTaskDeclarations!");

            registryStub.Clear();

            registeredTaskCount = TaskRegistryHelperMethods.GetDeepCountOfRegisteredTasks(registryStub.AllTaskDeclarations);
            Assert.AreEqual(0, registeredTaskCount, "Expected no registered tasks in TaskRegistry.AllTaskDeclarations!");
        }
    }

    internal class UsingTaskInfo
    {
        public string TaskName;
        public AssemblyLoadInfo AssemblyInfo;
        public string Condition;

        private UsingTask usingTask;
        public UsingTask UsingTask
        {
            get
            {
                if (null == usingTask)
                {
                    usingTask = ConstructUsingTask();
                }
                return usingTask;
            }
        }

        private XmlElement usingTaskElement;
        public XmlNode UsingTaskElement
        {
            get
            {
                if (null == usingTask)
                {
                    usingTask = ConstructUsingTask();
                }
                return usingTaskElement;
            }
        }

        public UsingTaskInfo(string taskName, string assemblyName, string assemblyFile)
        {
            TaskName = taskName;
            AssemblyInfo = new AssemblyLoadInfo(assemblyName, assemblyFile);
        }

        public UsingTaskInfo(string taskName, string assemblyName, string assemblyFile, string condition)
            : this(taskName, assemblyName, assemblyFile)
        {
            Condition = condition;
        }

        public UsingTaskInfo CopyAndExpand(Expander expander)
        {
            string newTaskName = expander.ExpandAllIntoString(UsingTask.TaskName, UsingTask.TaskNameAttribute);
            string newCondition = null;
            string newAssemblyName = null;
            string newAssemblyFile = null;

            if (UsingTask.Condition != null)
            {
                newCondition = expander.ExpandAllIntoString(UsingTask.Condition, UsingTask.ConditionAttribute);
            }
            if (UsingTask.AssemblyName != null)
            {
                newAssemblyName = expander.ExpandAllIntoString(UsingTask.AssemblyName, UsingTask.AssemblyNameAttribute);
            }
            if (UsingTask.AssemblyFile != null)
            {
                newAssemblyFile = expander.ExpandAllIntoString(UsingTask.AssemblyFile, UsingTask.AssemblyFileAttribute);
            }

            return new UsingTaskInfo(newTaskName, newAssemblyName, newAssemblyFile, newCondition);
        }

        private UsingTask ConstructUsingTask()
        {
            usingTaskElement = new XmlDocument().CreateElement("UsingTask");
            usingTaskElement.SetAttribute("TaskName", TaskName);

            if (AssemblyInfo.AssemblyName != null)
            {
                usingTaskElement.SetAttribute("AssemblyName", AssemblyInfo.AssemblyName);
            }
            else
            {
                usingTaskElement.SetAttribute("AssemblyFile", AssemblyInfo.AssemblyFile);
            }

            if (Condition != null)
            {
                usingTaskElement.SetAttribute("Condition", Condition);
            }

            return new UsingTask(usingTaskElement, false);
        }
    }

    internal class TaskRegistryHelperMethods
    {
        private static Expander registryExpander;

        internal static Expander RegistryExpander
        {
            get 
            {
                if (registryExpander == null)
                {
                    registryExpander = GetExpander();
                }
                return registryExpander; 
            }
        }

        private static EngineLoggingServices engineLoggingServices;

        internal static EngineLoggingServices LoggingServices
        {
            get
            {
                if (null == engineLoggingServices)
                {
                    engineLoggingServices = new EngineLoggingServicesHelper();
                }
                return engineLoggingServices;
            }
        }

        internal static int GetDeepCountOfRegisteredTasks(Hashtable table)
        {
            if (table == null)
            {
                return 0;
            }

            int count = 0;
            foreach (ArrayList bucket in table.Values)
            {
                count += bucket.Count;
            }
            return count;
        }

        internal static TaskRegistry CreateTaskRegistryAndRegisterTasks(UsingTaskInfo[] taskInfos)
        {
            TaskRegistry registry = new TaskRegistry();

            foreach (UsingTaskInfo taskInfo in taskInfos)
            {
                registry.RegisterTask(taskInfo.UsingTask, RegistryExpander, LoggingServices, null);
            }

            return registry;
        }

        internal static Expander GetExpander()
        {
            BuildPropertyGroup propertyGroup = new BuildPropertyGroup();
            propertyGroup.SetProperty("Property1", "Value1");
            propertyGroup.SetProperty("Property2", "Value2");
            propertyGroup.SetProperty("Property3", "Value3");
            propertyGroup.SetProperty("Property4", "Value4");
            propertyGroup.SetProperty("Property5", "Value5");

            BuildItemGroup itemGroup1 = new BuildItemGroup();
            itemGroup1.AddNewItem("FirstItem", "FirstValue1");
            itemGroup1.AddNewItem("FirstItem", "FirstValue2");
            itemGroup1.AddNewItem("FirstItem", "FirstValue3");

            BuildItemGroup itemGroup2 = new BuildItemGroup();
            itemGroup2.AddNewItem("SecondItem", "SecondValue1");
            itemGroup2.AddNewItem("SecondItem", "SecondValue2");
            itemGroup2.AddNewItem("SecondItem", "SecondValue3");

            BuildItemGroup itemGroup3 = new BuildItemGroup();
            itemGroup3.AddNewItem("ThirdItem", "ThirdValue1");

            Hashtable itemsByName = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemsByName["FirstItem"] = itemGroup1;
            itemsByName["SecondItem"] = itemGroup2;
            itemsByName["ThirdItem"] = itemGroup3;

            return new Expander(new ReadOnlyLookup(itemsByName, propertyGroup));
        }
    }
}
