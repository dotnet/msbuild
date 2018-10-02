// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Mock out the node class through the use of a derived class
    /// </summary>
    class MockTaskExecutionModule : TaskExecutionModule
    {
        // A dictionary containing the inputs to the postTaskOutputsMethod
        // This is used by tests to see what the results of calling PostTaskOutputs were
        Dictionary<string, object> postTaskOutputsInputs;

        public Dictionary<string, object> PostTaskOutputsInputs
        {
            get { return postTaskOutputsInputs; }
            set { postTaskOutputsInputs = value; }
        }

        public MockTaskExecutionModule(EngineCallback nodeProxy)
            : base(nodeProxy, TaskExecutionModule.TaskExecutionModuleMode.SingleProcMode, false)
        {

        }

        public MockTaskExecutionModule(EngineCallback nodeProxy, TaskExecutionModule.TaskExecutionModuleMode moduleMode)
            : base(nodeProxy, moduleMode, false)
        {

        }

        /// <summary>
        /// This method is called from TaskExecutionState and allows tests to see the results from TaskExecutionState
        /// </summary>
        internal override void PostTaskOutputs
        (
            int nodeProxyId,
            bool taskExecutedSuccessfully,
            Exception thrownException,
            long executionTime
        )
        {
            postTaskOutputsInputs = new Dictionary<string, object>();
            postTaskOutputsInputs.Add("nodeProxyId", nodeProxyId);
            postTaskOutputsInputs.Add("taskExecutedSuccessfully", taskExecutedSuccessfully);
            postTaskOutputsInputs.Add("thrownException", thrownException);
        }
 
        /// <summary>
        /// Override to BuildProject file to return true so we can test that
        /// </summary>
        override internal bool BuildProjectFile
        (
            int nodeProxyId,
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            EngineLoggingServices loggingServices,
            string[] toolsVersions,
            bool useResultsCache,
            bool unloadProjectsOnCompletion,
            BuildEventContext taskContext
        )
        {
            return true;
        }

        /// <summary>
        /// Return some data from the method for unit testing
        /// </summary>
        override internal void GetLineColumnOfXmlNode(int nodeProxyId, out int lineNumber, out int columnNumber)
        {
            lineNumber = 0;
            columnNumber = 0;
        }
        
    }
}
