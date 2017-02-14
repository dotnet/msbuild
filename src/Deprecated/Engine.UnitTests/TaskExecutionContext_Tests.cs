// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class TaskExecutionContextTest
    {
        /// <summary>
        /// Make sure that the constructor correctly sets the properties that are passed in
        /// </summary>
        [Test]
        public void TaskExecutionContextCreation()
        {

             // Create some items to instantiate a task execution context and check to make sure those values are set properly
            Engine engine = new Engine();
                   engine.BinPath="TestBinPath";
          
            ArrayList targetsToBuild = new ArrayList(); 
            targetsToBuild.Add("targetName");
            ProjectBuildState projectContext = new ProjectBuildState(null, targetsToBuild, new BuildEventContext(0, 1, 1, 1));

            TaskExecutionContext context = new TaskExecutionContext(null, null, null, projectContext, 4, EngineCallback.inProcNode, new BuildEventContext(BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId));
         
            Assert.IsTrue(context.BuildContext.TargetNamesToBuild.Contains("targetName"),"Expected target list to contain targetName");
            Assert.IsNull(context.ParentTarget,"ParentTarget should be null");
            Assert.IsNull(context.ThrownException,"ThrownException should be null");
            Assert.AreEqual(4,context.HandleId,"Node ProxyId should be 4");
        } 


        /// <summary>
        /// Check for each property we can set, that we get the same value out
        /// </summary>
        [Test]
        public void ExecutionProperties()
        {
            Engine engine = new Engine();
                   engine.BinPath="TestBinPath";

            ArrayList targetsToBuild = new ArrayList();
            targetsToBuild.Add("targetName");
            ProjectBuildState projectContext = new ProjectBuildState(new BuildRequest(-1, null, null, (BuildPropertyGroup)null, null, -1, false, false), targetsToBuild, new BuildEventContext(0, 1, 1, 1));

            TaskExecutionContext context = new TaskExecutionContext(null, null, null, projectContext, 4, EngineCallback.inProcNode, new BuildEventContext(BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId));

            Assert.IsTrue(context.BuildContext.TargetNamesToBuild.Contains("targetName"), "Expected target list to contain targetName");

            Assert.AreEqual(EngineCallback.inProcNode,context.NodeIndex);

            Assert.IsNull(context.ParentTarget,"Expected ParentTarget to be null");

            context.SetTaskOutputs(false, null, 0);

            Assert.IsFalse(context.TaskExecutedSuccessfully );
            Assert.IsNull(context.ThrownException, "Expected ThrownException to be null");

            context.SetTaskOutputs(true, new Exception(), 0);

            Assert.IsTrue(context.TaskExecutedSuccessfully);
            Assert.IsNotNull(context.ThrownException,"Expected ThrownException to not be null");


        }

    }
}
