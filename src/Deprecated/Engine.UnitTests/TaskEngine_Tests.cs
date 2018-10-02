// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;
using System.Xml;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class TaskEngineTest
    {
        /*********************************************************************************
         * 
         *                                     BOOL
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a "bool" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Bool_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; 
            EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyBoolParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myBoolParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyBoolParam, false);
        }

        /// <summary>
        /// Initialize a "bool" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Bool_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyBoolParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myBoolParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyBoolParam, false);
        }

        /// <summary>
        /// Initialize a "bool" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Bool_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyBoolParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myBoolParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyBoolParam, false);
        }

        /// <summary>
        /// Initialize a "bool" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Bool_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyBoolParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myBoolParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyBoolParam, false);
        }

        /*********************************************************************************
         * 
         *                                     BOOL[]
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a "bool[]" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_BoolArray_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyBoolArrayParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myBoolArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyBoolArrayParam, null);
        }

        /// <summary>
        /// Initialize a "bool[]" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_BoolArray_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyBoolArrayParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myBoolArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyBoolArrayParam, null);
        }

        /// <summary>
        /// Initialize a "bool[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_BoolArray_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyBoolArrayParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myBoolArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyBoolArrayParam, null);
        }

        /// <summary>
        /// Initialize a "bool[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_BoolArray_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyBoolArrayParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myBoolArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyBoolArrayParam, null);
        }

        /*********************************************************************************
         * 
         *                                     INT
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a "int" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Int_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyIntParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myIntParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyIntParam, 0);
        }

        /// <summary>
        /// Initialize a "int" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Int_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyIntParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myIntParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyIntParam, 0);
        }

        /// <summary>
        /// Initialize a "int" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Int_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyIntParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myIntParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyIntParam, 0);
        }

        /// <summary>
        /// Initialize a "int" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Int_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyIntParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myIntParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyIntParam, 0);
        }

        /*********************************************************************************
         * 
         *                                     INT[]
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a "int[]" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_IntArray_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyIntArrayParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myIntArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyIntArrayParam, null);
        }

        /// <summary>
        /// Initialize a "int[]" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_IntArray_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyIntArrayParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myIntArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyIntArrayParam, null);
        }

        /// <summary>
        /// Initialize a "int[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_IntArray_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyIntArrayParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myIntArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyIntArrayParam, null);
        }

        /// <summary>
        /// Initialize a "int[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_IntArray_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyIntArrayParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myIntArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyIntArrayParam, null);
        }

        /*********************************************************************************
         * 
         *                                     STRING
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a "string" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_String_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyStringParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myStringParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyStringParam, null);
        }

        /// <summary>
        /// Initialize a "string" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_String_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyStringParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myStringParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyStringParam, null);
        }

        /// <summary>
        /// Initialize a "string" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_String_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyStringParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myStringParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyStringParam, null);
        }

        /// <summary>
        /// Initialize a "string" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_String_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyStringParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myStringParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyStringParam, null);
        }

        /*********************************************************************************
         * 
         *                                     STRING[]
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a "string[]" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_StringArray_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyStringArrayParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myStringArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyStringArrayParam, null);
        }

        /// <summary>
        /// Initialize a "string[]" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_StringArray_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyStringArrayParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myStringArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyStringArrayParam, null);
        }

        /// <summary>
        /// Initialize a "string[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_StringArray_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyStringArrayParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myStringArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyStringArrayParam, null);
        }

        /// <summary>
        /// Initialize a "string[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_StringArray_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyStringArrayParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myStringArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyStringArrayParam, null);
        }

        /// <summary>
        /// Initializing a string[] task parameter with a single item.
        /// </summary>
        /// <owner>JomoF</owner>
        [Test]
        public void InitTask_StringArray_SingleItem()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyStringArrayParam", "VisualBasic.dll");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("set", mockTask.myStringArrayParamWasSet, true);
            Assertion.AssertEquals("set length", mockTask.MyStringArrayParam.Length, 1);
            Assertion.AssertEquals("set value", mockTask.MyStringArrayParam[0], "VisualBasic.dll");
        }

        /*********************************************************************************
         * 
         *                                     ITASKITEM
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a "ITaskItem" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_ITaskItem_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyITaskItemParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myITaskItemParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemParam, null);
        }

        /// <summary>
        /// Initialize a "string" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_ITaskItem_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myITaskItemParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemParam, null);
        }

        /// <summary>
        /// Initialize a "string" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_ITaskItem_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myITaskItemParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemParam, null);
        }

        /// <summary>
        /// Initialize a "itaskitem" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_ITaskItem_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myITaskItemParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemParam, null);
        }

        /// <summary>
        /// Initialize a "itaskitem" parameter where a transform was specified, but evaluated to empty.
        /// This should result in no setting at all.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void InitTask_ITaskItem_ExpansionEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket;
            EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemParam", "@(ItemListContainingOneItem -> '%(NonexistentMeta)')");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myITaskItemParamWasSet, true);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemParam.ItemSpec, String.Empty);
        }

        /// <summary>
        /// Initialize a "itaskitem" parameter where a transform was specified, but evaluated to empty.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void InitTask_ITaskItemArray_ExpansionEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket;
            EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemArrayParam", "@(ItemListContainingTwoItems -> '%(NonexistentMeta)')");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter to have gotten called.
            Assertion.AssertEquals("not set", mockTask.myITaskItemArrayParamWasSet, true);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemArrayParam[0].ItemSpec, String.Empty);
        }

        /// <summary>
        /// Initializing a ITaskItem scalar parameter with an item that has some metadata
        /// should preserve the metadata.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_ITaskItem_PreservesMetadata()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemParam", "@(ItemListContainingOneItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("Expected task parameter to be set", mockTask.myITaskItemParamWasSet, true);
            Assertion.AssertNotNull("Expected task parameter to be non-null", mockTask.MyITaskItemParam);
            Assertion.AssertEquals("Expected item spec to be a.cs", "a.cs", mockTask.MyITaskItemParam.ItemSpec);
            Assertion.AssertEquals("Expected culture to be fr-fr", "fr-fr", mockTask.MyITaskItemParam.GetMetadata("Culture"));
        }

        /// <summary>
        /// Initializing a ITaskItem scalar parameter with an item that has some metadata
        /// should preserve the metadata.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_ITaskItem_CannotAcceptTwoItems()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemParam", "@(ItemListContainingTwoItems)");

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /*********************************************************************************
         * 
         *                                     ITASKITEM[]
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a "string[]" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_ITaskItemArray_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyITaskItemArrayParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myITaskItemArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemArrayParam, null);
        }

        /// <summary>
        /// Initialize a "string[]" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_ITaskItemArray_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemArrayParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myITaskItemArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemArrayParam, null);
        }

        /// <summary>
        /// Initialize a "string[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_ITaskItemArray_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemArrayParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myITaskItemArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemArrayParam, null);
        }

        /// <summary>
        /// Initialize a "string[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_ITaskItemArray_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyITaskItemArrayParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            // We expect the setter not to have gotten called, which means its value remains the default.
            Assertion.AssertEquals("not set", mockTask.myITaskItemArrayParamWasSet, false);
            Assertion.AssertEquals("default value", mockTask.MyITaskItemArrayParam, null);
        }

        /*********************************************************************************
         * 
         *                                     [REQUIRED] BOOL
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a REQUIRED "bool" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_Bool_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyRequiredBoolParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "bool" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_Bool_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredBoolParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "bool" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_Bool_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredBoolParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "bool" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_Bool_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredBoolParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /*********************************************************************************
         * 
         *                                     [REQUIRED] BOOL[]
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a REQUIRED "bool[]" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_BoolArray_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyRequiredBoolArrayParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "bool[]" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_BoolArray_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredBoolArrayParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredBoolArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredBoolArrayParam.Length, 0);
        }

        /// <summary>
        /// Initialize a REQUIRED "bool[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_BoolArray_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredBoolArrayParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredBoolArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredBoolArrayParam.Length, 0);
        }

        /// <summary>
        /// Initialize a REQUIRED "bool[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_BoolArray_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredBoolArrayParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredBoolArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredBoolArrayParam.Length, 0);
        }

        /*********************************************************************************
         * 
         *                                     [REQUIRED] INT
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a REQUIRED "int" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_Int_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyRequiredIntParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "int" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_Int_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredIntParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "int" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_Int_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredIntParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "int" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_Int_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredIntParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /*********************************************************************************
         * 
         *                                     [REQUIRED] INT[]
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a REQUIRED "int[]" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_IntArray_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyRequiredIntArrayParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "int[]" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_IntArray_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredIntArrayParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredIntArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredIntArrayParam.Length, 0);
        }

        /// <summary>
        /// Initialize a REQUIRED "int[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_IntArray_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredIntArrayParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredIntArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredIntArrayParam.Length, 0);
        }

        /// <summary>
        /// Initialize a REQUIRED "int[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_IntArray_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredIntArrayParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredIntArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredIntArrayParam.Length, 0);
        }

        /*********************************************************************************
         * 
         *                                     [REQUIRED] STRING
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a REQUIRED "string" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [ExpectedException(typeof(InvalidProjectFileException))]
        [Test]
        public void InitTask_Required_String_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyRequiredStringParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "string" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_String_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredStringParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "string" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_String_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredStringParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "string" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_String_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredStringParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /*********************************************************************************
         * 
         *                                     [REQUIRED] STRING[]
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a REQUIRED "string[]" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_StringArray_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyRequiredStringArrayParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "string[]" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_StringArray_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredStringArrayParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredStringArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredStringArrayParam.Length, 0);
        }

        /// <summary>
        /// Initialize a REQUIRED "string[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_StringArray_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredStringArrayParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredStringArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredStringArrayParam.Length, 0);
        }

        /// <summary>
        /// Initialize a REQUIRED "string[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_StringArray_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredStringArrayParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredStringArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredStringArrayParam.Length, 0);
        }

        /*********************************************************************************
         * 
         *                                     [REQUIRED] ITASKITEM
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a REQUIRED "ITaskItem" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_ITaskItem_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyRequiredITaskItemParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "string" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_ITaskItem_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredITaskItemParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "string" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_ITaskItem_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredITaskItemParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "string" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_ITaskItem_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredITaskItemParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /*********************************************************************************
         * 
         *                                     [REQUIRED] ITASKITEM[]
         * 
         *********************************************************************************/
        /// <summary>
        /// Initialize a REQUIRED "string[]" parameter where no XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InitTask_Required_ITaskItemArray_NoAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.RemoveAttribute("MyRequiredITaskItemArrayParam");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should throw.
            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);
        }

        /// <summary>
        /// Initialize a REQUIRED "string[]" parameter where a completely empty XML attribute was specified on the task.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_ITaskItemArray_EmptyAttribute()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredITaskItemArrayParam", "");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredITaskItemArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredITaskItemArrayParam.Length, 0);
        }

        /// <summary>
        /// Initialize a REQUIRED "string[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_ITaskItemArray_PropertyEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredITaskItemArrayParam", "$(NonExistentProperty)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredITaskItemArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredITaskItemArrayParam.Length, 0);
        }

        /// <summary>
        /// Initialize a REQUIRED "string[]" parameter where an XML attribute was specified, but evaluated to empty.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void InitTask_Required_ITaskItemArray_ItemEvaluatesToEmpty()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();
            taskNode.SetAttribute("MyRequiredITaskItemArrayParam", "@(NonExistentItem)");
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            bool success = taskEngine.InitializeTask(mockTask, itemBucket, engineProxy);

            // We expect success.
            Assertion.Assert("success", success);

            Assertion.AssertEquals("was set", mockTask.myRequiredITaskItemArrayParamWasSet, true);
            Assertion.AssertEquals("new value", mockTask.MyRequiredITaskItemArrayParam.Length, 0);
        }

        /*********************************************************************************
         * 
         *                                  OUTPUT PARAMS
         * 
         *********************************************************************************/
        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "ArrayList".  This should fail. Bug #416910
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void TaskOutputsArrayListParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "MyArrayListOutputParam");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            // This should fail because MSBuild doesn't support output params of type ArrayList.
            taskEngine.GatherGeneratedTaskOutputs(LookupHelpers.CreateLookup(new Hashtable()), myTaskOutputSpecification, "MyArrayListOutputParam", "MyItemList", null, mockTask);
        }

        /// <summary>
        /// Attempts to gather outputs from a null task parameter of type "ITaskItem[]".  This should succeed. Bug #430452
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsNullITaskItemArrayParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "NullITaskItemArrayOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(LookupHelpers.CreateLookup(new Hashtable()), myTaskOutputSpecification, "NullITaskItemArrayOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        /// <summary>
        /// Attempts to gather outputs into an item list from an string task parameter that
        /// returns an empty string. This should be a no-op. Bug #444501.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsEmptyStringParameterIntoItemList()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "EmptyStringOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(LookupHelpers.CreateLookup(new Hashtable()), myTaskOutputSpecification, "NullITaskItemArrayOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException
        }

        /// <summary>
        /// Attempts to gather outputs into an item list from an string task parameter that
        /// returns an empty string. This should be a no-op. Bug #444501.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsEmptyStringInStringArrayParameterIntoItemList()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "EmptyStringInStringArrayOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(LookupHelpers.CreateLookup(new Hashtable()), myTaskOutputSpecification, "EmptyStringInStringArrayOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "string".  This should succeed.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsStringParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "StringOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(GetEnteredScopeLookup(), myTaskOutputSpecification, "StringOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "string[]".  This should succeed.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsStringArrayParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "StringArrayOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(GetEnteredScopeLookup(), myTaskOutputSpecification, "StringArrayOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "int".  This should succeed.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsIntParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "IntOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(GetEnteredScopeLookup(), myTaskOutputSpecification, "IntOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "int[]".  This should succeed.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsIntArrayParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "IntArrayOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(GetEnteredScopeLookup(), myTaskOutputSpecification, "IntArrayOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }


        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "object[]".  This should fail.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void TaskOutputsObjectArrayParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "ObjectArrayOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(LookupHelpers.CreateLookup(new Hashtable()), myTaskOutputSpecification, "ObjectArrayOutputParameter", "MyItemList", null, mockTask);
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "ITaskItem".  This should succeed.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsITaskItemParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "ITaskItemOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(LookupHelpers.CreateLookup(new Hashtable()), myTaskOutputSpecification, "ITaskItemOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "TaskItem".  This should succeed.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsTaskItemParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "TaskItemOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(GetEnteredScopeLookup(), myTaskOutputSpecification, "TaskItemOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "TaskItem[]".  This should succeed.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsTaskItemArrayParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "TaskItemArrayOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(GetEnteredScopeLookup(), myTaskOutputSpecification, "TaskItemArrayOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type that is a custom implementation of "ITaskItem".  This should succeed.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsMyTaskItemParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "MyTaskItemOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(GetEnteredScopeLookup(), myTaskOutputSpecification, "MyTaskItemOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type that is an array of a custom implementation of "ITaskItem".  This should succeed.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void TaskOutputsMyTaskItemArrayParameter()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            taskNode = CreateXmlTaskNode();

            // Create a task output specification to satisfy GatherGeneratedTaskOutputs.
            XmlElement outputElement = taskNode.OwnerDocument.CreateElement("Output");
            outputElement.SetAttribute("TaskParameter", "MyTaskItemArrayOutputParameter");
            outputElement.SetAttribute("ItemName", "MyItemList");
            taskNode.AppendChild(outputElement);
            TaskOutput myTaskOutputSpecification = new TaskOutput(outputElement);

            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy);

            taskEngine.GatherGeneratedTaskOutputs(GetEnteredScopeLookup(), myTaskOutputSpecification, "MyTaskItemArrayOutputParameter", "MyItemList", null, mockTask);
            // Did not throw InvalidProjectFileException  
        }

        [Test]
        public void TasksAreDiscoveredWhenTaskConditionTrue()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            string condition = "'1'=='1'";
            taskNode = CreateXmlTaskNode(condition);
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy, condition);
            // Make sure class needs to be found
            taskEngine.TaskClass = null;

            Hashtable[] fakeArray = new Hashtable[1];
            fakeArray[0] = new Hashtable();

            // ExecuteTask should fail as it isn't found
            Assertion.Assert(!taskEngine.ExecuteTask(
                                   TaskExecutionMode.ExecuteTaskAndGatherOutputs,
                                   LookupHelpers.CreateLookup(new Hashtable())));
        }
        
        [Test]
        public void TasksNotDiscoveredWhenTaskConditionFalse()
        {
            XmlElement taskNode;
            TaskEngine taskEngine;
            MockTask mockTask;
            ItemBucket itemBucket; EngineProxy engineProxy;
            string condition = "'1'=='2'";
            taskNode = CreateXmlTaskNode(condition);
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy, condition);
            // Make sure class needs to be found
            taskEngine.TaskClass = null;

            Hashtable[] fakeArray = new Hashtable[1];
            fakeArray[0] = new Hashtable();

            // ExecuteTask should succeed as although the task couldn't be found,
            // it never needed to look for it
            Assertion.Assert(taskEngine.ExecuteTask(
                                   TaskExecutionMode.ExecuteTaskAndGatherOutputs,
                                   LookupHelpers.CreateLookup(new Hashtable())));

        }

        /// <summary>
        /// Test task instantiation failure with a bogus using task tag
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void TaskClassResolutionFailureWithUsingTask()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <UsingTask TaskName=`ShipWhidbey` AssemblyName=`madeup` />

                    <Target Name=`Build`>
                        <Message Text=`Building...`/>
                        <ShipWhidbey When=`Now!` />
                    </Target>
                </Project>";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectFileContents, logger);
            project.Build(null, null);

            logger.AssertLogContains("MSB4062");
            logger.AssertLogContains("Building...");
        }

        /// <summary>
        /// Test task instantiation failure without a using task tag
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void TaskClassResolutionFailureWithoutUsingTask()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`Build`>
                        <Message Text=`Building...`/>
                        <ShipWhidbey When=`Now!` />
                    </Target>
                </Project>";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectFileContents, logger);
            project.Build(null, null);

            logger.AssertLogContains("MSB4036");
            logger.AssertLogContains("Building...");
        }

        /// <summary>
        /// Test correct error is logged if the project file specifies an output
        /// that the task does not provide.  Regress VSWhidbey 584487.
        /// </summary>
        /// <owner>JeffCal</owner>
        [Test]
        public void TaskOutputSpecifiedButNotSupported()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`Build`>
                        <Message Text=`Building...`>
                            <Output TaskParameter=`NotSupported` ItemName=`MyItem` />
                        </Message>
                        <Message Text=`Got Here!` />
                    </Target>
                </Project>";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectFileContents, logger);
            project.Build(null, null);

            logger.AssertLogContains("MSB4131");
            logger.AssertLogDoesntContain("Got Here!");
            Assertion.Assert("MSBuild should have logged an error because the task does not provide the given output parameter!", logger.ErrorCount > 0);
        }

        /// <summary>
        /// Verify when task outputs are overridden the override messages are correctly displayed
        /// </summary>
        [Test]
        public void OverridePropertiesInCreateProperty()
        {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                     <EmbeddedResource Include='a.resx'>
                    <LogicalName>foo</LogicalName>
                      </EmbeddedResource>
                        <EmbeddedResource Include='b.resx'>
                        <LogicalName>bar</LogicalName>
                    </EmbeddedResource>
                        <EmbeddedResource Include='c.resx'>
                        <LogicalName>barz</LogicalName>
                    </EmbeddedResource>
                    </ItemGroup>
                  <Target Name=`t`>
                    <CreateProperty Value=""@(EmbeddedResource->'/assemblyresource:%(Identity),%(LogicalName)', ' ')""
                                     Condition=""'%(LogicalName)' != '' "">
                         <Output TaskParameter=""Value"" PropertyName=""LinkSwitches""/>
                    </CreateProperty>
                    <Message Text=`final:[$(LinkSwitches)]`/>                    
                </Target>
                </Project>
            ", logger);
                p.Build(new string[] { "t" });
                logger.AssertLogContains(new string[] { "final:[/assemblyresource:c.resx,barz]"});
                logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceString("TaskStarted", "CreateProperty") });
                logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceString("PropertyOutputOverridden", "LinkSwitches", "/assemblyresource:a.resx,foo", "/assemblyresource:b.resx,bar") });
                logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceString("PropertyOutputOverridden", "LinkSwitches", "/assemblyresource:b.resx,bar", "/assemblyresource:c.resx,barz") });
         }

        /// <summary>
        /// Verify that when a task outputs are inferred the override messages are displayed
        /// </summary>
        [Test]
        public void OverridePropertiesInInferredCreateProperty()
        {
            string[] files = null;
            try
            {
                files = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));

                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`" + files[0] + "`><output>" + files[1] + @"</output></i>
                  </ItemGroup> 
                  <ItemGroup>
                     <EmbeddedResource Include='a.resx'>
                    <LogicalName>foo</LogicalName>
                      </EmbeddedResource>
                        <EmbeddedResource Include='b.resx'>
                        <LogicalName>bar</LogicalName>
                    </EmbeddedResource>
                        <EmbeddedResource Include='c.resx'>
                        <LogicalName>barz</LogicalName>
                    </EmbeddedResource>
                    </ItemGroup>
                  <Target Name=`t2` DependsOnTargets=`t`>
                    <Message Text=`final:[$(LinkSwitches)]`/>   
                  </Target>
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.Output)`>
                    <Message Text=`start:[Hello]`/>
                    <CreateProperty Value=""@(EmbeddedResource->'/assemblyresource:%(Identity),%(LogicalName)', ' ')""
                                     Condition=""'%(LogicalName)' != '' "">
                         <Output TaskParameter=""Value"" PropertyName=""LinkSwitches""/>
                    </CreateProperty>
                    <Message Text=`end:[hello]`/>                    
                </Target>
                </Project>
            ", logger);
                p.Build(new string[] { "t2" });

                // We should only see messages from the second target, as the first is only inferred
                logger.AssertLogDoesntContain("start:");
                logger.AssertLogDoesntContain("end:");

                logger.AssertLogContains(new string[] { "final:[/assemblyresource:c.resx,barz]" });
                logger.AssertLogDoesntContain( ResourceUtilities.FormatResourceString("TaskStarted", "CreateProperty"));
                logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceString("PropertyOutputOverridden", "LinkSwitches", "/assemblyresource:a.resx,foo", "/assemblyresource:b.resx,bar") });
                logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceString("PropertyOutputOverridden", "LinkSwitches", "/assemblyresource:b.resx,bar", "/assemblyresource:c.resx,barz") });
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(files);
            }
        }

        /*********************************************************************************
         * 
         *                                     Helpers
         * 
         *********************************************************************************/
        private void InstantiateMockTaskHelper
            (
            XmlElement taskNode,
            out TaskEngine taskEngine,
            out MockTask mockTask,
            out ItemBucket itemBucket,
            out EngineProxy engineProxy
            )
        {
            InstantiateMockTaskHelper(taskNode, out taskEngine, out mockTask, out itemBucket, out engineProxy, null);
        }

        /// <summary>
        /// Creates an instance of a MockTask, and returns the objects necessary to exercise
        /// taskEngine.InitializeTask
        /// </summary>
        /// <param name="taskNode"></param>
        /// <param name="taskEngine"></param>
        /// <param name="mockTask"></param>
        /// <param name="itemBucket"></param>
        /// <owner>RGoel</owner>
        private void InstantiateMockTaskHelper
            (
            XmlElement taskNode,
            out TaskEngine taskEngine,
            out MockTask mockTask,
            out ItemBucket itemBucket,
            out EngineProxy engineProxy,
            string condition
            )
        {
            LoadedType taskClass = new LoadedType(typeof(MockTask), new AssemblyLoadInfo(typeof(MockTask).Assembly.FullName, null));
            Engine engine = new Engine(@"c:\");
            Project project = new Project(engine);
            EngineCallback engineCallback = new EngineCallback(engine);
            TaskExecutionModule taskExecutionModule = new TaskExecutionModule(engineCallback, 
                                        TaskExecutionModule.TaskExecutionModuleMode.SingleProcMode, false);
            ProjectBuildState buildContext = new ProjectBuildState(null, null, new BuildEventContext(0, 1, 1, 1));
            int nodeProxyID = engineCallback.CreateTaskContext(project, null, buildContext, taskNode, EngineCallback.inProcNode, new BuildEventContext(BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId));
            taskEngine = new TaskEngine
                                (
                                    taskNode,
                                    null, /* host object */
                                    "In Memory",
                                    project.FullFileName,
                                    engine.LoggingServices,
                                    nodeProxyID,
                                    taskExecutionModule, 
                                    new BuildEventContext(0, 1, 1, 1)
                                );
            taskEngine.TaskClass = taskClass;
            engineProxy = new EngineProxy(taskExecutionModule, nodeProxyID, project.FullFileName, project.FullFileName, engine.LoggingServices, null);
            mockTask = new MockTask(new EngineProxy(taskExecutionModule, nodeProxyID, project.FullFileName, project.FullFileName, engine.LoggingServices, null));

            // The code below creates an item table that is equivalent to the following MSBuild syntax:
            //
            //      <ItemGroup>
            //          <ItemListContainingOneItem Include="a.cs">
            //              <Culture>fr-fr</Culture>
            //          </ItemListContainingOneItem>
            //
            //          <ItemListContainingTwoItems Include="b.cs">
            //              <HintPath>c:\foo</HintPath>
            //          </ItemListContainingTwoItems>
            //          <ItemListContainingTwoItems Include="c.cs">
            //              <HintPath>c:\bar</HintPath>
            //          </ItemListContainingTwoItems>
            //      </ItemGroup>
            //
            Hashtable itemsByName = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildItemGroup itemListContainingOneItem = new BuildItemGroup();
            BuildItem a = itemListContainingOneItem.AddNewItem("ItemListContainingOneItem", "a.cs");
            a.SetMetadata("Culture", "fr-fr");
            itemsByName["ItemListContainingOneItem"] = itemListContainingOneItem;

            BuildItemGroup itemListContainingTwoItems = new BuildItemGroup();
            BuildItem b = itemListContainingTwoItems.AddNewItem("ItemListContainingTwoItems", "b.cs");
            b.SetMetadata("HintPath", "c:\\foo");
            BuildItem c = itemListContainingTwoItems.AddNewItem("ItemListContainingTwoItems", "c.cs");
            c.SetMetadata("HintPath", "c:\\bar");
            itemsByName["ItemListContainingTwoItems"] = itemListContainingTwoItems;

            itemBucket = new ItemBucket(new string[0], new Dictionary<string, string>(), LookupHelpers.CreateLookup(itemsByName), 0);
        }

        private static XmlElement CreateXmlTaskNode()
        {
            return CreateXmlTaskNode(null);
        }

        private static XmlElement CreateXmlTaskNode(string condition)
        {
            XmlElement taskNode;
            taskNode = new XmlDocument().CreateElement("MockTask");
            taskNode.SetAttribute("MyRequiredBoolParam", "true");
            taskNode.SetAttribute("MyRequiredBoolArrayParam", "true");
            taskNode.SetAttribute("MyRequiredIntParam", "1");
            taskNode.SetAttribute("MyRequiredIntArrayParam", "1");
            taskNode.SetAttribute("MyRequiredStringParam", "a");
            taskNode.SetAttribute("MyRequiredStringArrayParam", "a");
            taskNode.SetAttribute("MyRequiredITaskItemParam", "a");
            taskNode.SetAttribute("MyRequiredITaskItemArrayParam", "a");
            if (null != condition)
            {
                taskNode.SetAttribute("Condition", condition);
            }
            return taskNode;
        }

        /// <summary>
        /// Return a lookup that has entered scope, so that adds/removes are allowed on it.
        /// </summary>
        /// <returns></returns>
        private Lookup GetEnteredScopeLookup()
        {
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            lookup.EnterScope();
            return lookup;
        }
    }
}
