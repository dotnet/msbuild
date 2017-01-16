// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Collections;

using TargetLoggingContext = Microsoft.Build.BackEnd.Logging.TargetLoggingContext;
using System.Threading.Tasks;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// The mock component TaskBuilder.
    /// </summary>
    internal class QAMockTaskBuilder : ITaskBuilder, IBuildComponent, IDisposable
    {
        #region Data members

        /// <summary>
        /// The component host.
        /// </summary>
        private IBuildComponentHost _host;

        /// <summary>
        /// The test data provider
        /// </summary>
        private ITestDataProvider _testDataProvider;

        /// <summary>
        /// Event to notify that the build has been completed
        /// </summary>
        private AutoResetEvent _taskDone;

        /// <summary>
        /// result of the task
        /// </summary>
        private WorkUnitResult _result;

        #endregion

        #region Constructor

        internal QAMockTaskBuilder()
        {
            _host = null;
            _testDataProvider = null;
            _result = null;
            _taskDone = new AutoResetEvent(false);
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Sets the component host.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            _host = host;
            _testDataProvider = (ITestDataProvider)host.GetComponent(BuildComponentType.TestDataProvider);
        }

        /// <summary>
        /// Shuts down the component.
        /// </summary>
        public void ShutdownComponent()
        {
            _host = null;
            _testDataProvider = null;
            _result = null;
            this.Dispose();
        }

        #endregion

        #region ITaskBuilder Members

        /// <summary>
        /// Simulates executing a task. Execution time simulation is rounded to the closes numeric value. If this value is less than 1000 than nothing happens.
        /// </summary>
        public Task<WorkUnitResult> ExecuteTask(TargetLoggingContext targetLoggingContext, BuildRequestEntry requestEntry, ITargetBuilderCallback targetBuilderCallback, ProjectTargetInstanceChild task, TaskExecutionMode mode, Lookup lookupForInference, Lookup lookupForExecution, CancellationToken cancellationToken)
        {
            bool cancelled = false;
            RequestDefinition testDefinition = _testDataProvider[requestEntry.Request.ConfigurationId];
            TargetDefinition targetDefinition = testDefinition.ProjectDefinition.TargetsCollection[targetLoggingContext.Target.Name];
            ProjectTaskInstance taskInstance = (ProjectTaskInstance)task as ProjectTaskInstance;
            TaskDefinition taskDefinition = targetDefinition.TasksCollection[taskInstance.Name];

            taskDefinition.SignalTaskStarted();

            if (testDefinition.ExecutionTime > 1000)
            {
                DateTime startTime = DateTime.Now;
                long executionTimeInSeconds = testDefinition.ExecutionTime / 1000;

                while (executionTimeInSeconds > 0)
                {
                    if (cancellationToken.WaitHandle.WaitOne(1, false) == true)
                    {
                        cancelled = true;
                        break;
                    }

                    Thread.Sleep(1000);
                    executionTimeInSeconds -= 1;
                }
            }

            if (!cancelled)
            {
                _result = taskDefinition.ExpectedResult;
            }
            else
            {
                _result = new WorkUnitResult(WorkUnitResultCode.Canceled, WorkUnitActionCode.Stop, new MockTaskBuilderException());
            }

            taskDefinition.SignalTaskCompleted();
            _taskDone.Set();
            return Task<BuildResult>.FromResult(_result);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Close the event handels
        /// </summary>
        public void Dispose()
        {
            InternalDispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Close the envent handles
        /// </summary>
        private void InternalDispose()
        {
            _taskDone.Close();
        }

        /// <summary>
        /// Distroy this object by closing the event handles.
        /// </summary>
        ~QAMockTaskBuilder()
        {
            InternalDispose();
        }

        #endregion
    }

    /// <summary>
    /// Exception object for the mock task builder
    /// </summary>
    [Serializable]
    public class MockTaskBuilderException : Exception
    {
        public MockTaskBuilderException() : base("MockTaskBuilderException")
        {
        }
    }
}