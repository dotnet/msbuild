// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Collections;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// Defines the elements of a task within a project file
    /// </summary>
    internal class TaskDefinition : IDisposable
    {
        #region Private data members

        /// <summary>
        /// Task name
        /// </summary>
        private string _name;

        /// <summary>
        /// Condition on the task
        /// </summary>
        private string _condition;

        /// <summary>
        /// Target should continue if task failed
        /// </summary>
        private bool _continueOnError;

        /// <summary>
        /// Task Xml representation
        /// </summary>
        private XmlElement _taskElement;

        /// <summary>
        /// XMLDocument to use when creating elements
        /// </summary>
        private XmlDocument _parentXmlDocument;

        /// <summary>
        /// Event which notifies if the task has completed execution
        /// </summary>
        private AutoResetEvent _taskExecuted;

        /// <summary>
        /// Event which notifies if the task has started execution
        /// </summary>
        private AutoResetEvent _taskStarted;

        /// <summary>
        /// Final task parameter
        /// </summary>
        private Dictionary<string, string> _finalTaskParameters;

        /// <summary>
        /// Expected result of the task
        /// </summary>
        private WorkUnitResult _expectedResult;

        #endregion

        #region Constructor

        /// <summary>
        /// Basic constructor which takes the task name
        /// </summary>
        public TaskDefinition(string name, XmlDocument projectXmlDocument)
            : this(name, null, false, projectXmlDocument, new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null))
        {
        }

        /// <summary>
        /// Basic constructor which takes the task name and the task expected status
        /// </summary>
        public TaskDefinition(string name, XmlDocument projectXmlDocument, WorkUnitResult expectedResult)
            : this(name, null, false, projectXmlDocument, expectedResult)
        {
        }

        /// <summary>
        /// Constructor allows you to set all the data
        /// </summary>
        public TaskDefinition(string name, string condition, bool continueOnError, XmlDocument projectXmlDocument, WorkUnitResult expectedResult)
        {
            _name = name;
            _condition = condition;
            _continueOnError = continueOnError;
            _taskElement = projectXmlDocument.CreateElement(_name, @"http://schemas.microsoft.com/developer/msbuild/2003");
            _parentXmlDocument = projectXmlDocument;
            _expectedResult = expectedResult;
            _finalTaskParameters = new Dictionary<string, string>();
            _taskExecuted = new AutoResetEvent(false);
            _taskStarted = new AutoResetEvent(false);
            GenerateTaskElement();
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Add a task input parameter
        /// </summary>
        public void AddTaskInput(string inputName, string inputValue)
        {
            _taskElement.SetAttribute(inputName, inputValue);
        }

        /// <summary>
        /// Adds a task output
        /// </summary>
        public void AddTaskOutput(string outputParameterName, string outputAssignmentName, bool assignmentAsProperty)
        {
            XmlElement output = _parentXmlDocument.CreateElement("Output", @"http://schemas.microsoft.com/developer/msbuild/2003");

            output.SetAttribute("TaskParameter", outputParameterName);
            if (assignmentAsProperty)
            {
                output.SetAttribute("PropertyName", outputAssignmentName);
            }
            else
            {
                output.SetAttribute("ItemName", outputAssignmentName);
            }

            _taskElement.AppendChild(output as XmlNode);
        }

        /// <summary>
        /// Validates if the parameter name and value were populated correctly
        /// </summary>
        public void ValidateTaskParameter(string parameterName, string parameterValue)
        {
            if (!_finalTaskParameters.ContainsKey(parameterName))
            {
                Assert.Fail("Final task parameter list does not contain the parameter");
            }

            Assert.AreEqual(_finalTaskParameters[parameterName], parameterValue, "Value is not the same as expected");
        }

        /// <summary>
        /// Waits for the task to complete executing
        /// </summary>
        public void WaitForTaskToComplete()
        {
            _taskExecuted.WaitOne();
        }

        /// <summary>
        /// Waits for the task to start executing
        /// </summary>
        public void WaitForTaskToStart()
        {
            _taskStarted.WaitOne();
        }

        /// <summary>
        /// Signals the task has executed
        /// </summary>
        public void SignalTaskCompleted()
        {
            _taskExecuted.Set();
        }

        /// <summary>
        /// Signals the task has started
        /// </summary>
        public void SignalTaskStarted()
        {
            _taskStarted.Set();
        }

        #endregion

        #region Public properties

        /// <summary>
        /// Task XML element
        /// </summary>
        public XmlElement FinalTaskXmlElement
        {
            get
            {
                return _taskElement;
            }
        }

        /// <summary>
        /// Expected result code from this task
        /// </summary>
        public WorkUnitResult ExpectedResult
        {
            get
            {
                return _expectedResult;
            }
        }

        /// <summary>
        /// Task name
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
        }

        /// <summary>
        /// Set the final task parameters. Returns NULL on get regardless as this value can only be used by public methods and not
        /// straight by user.
        /// </summary>
        public Dictionary<string, string> FinalTaskParameters
        {
            get
            {
                return null;
            }
            set
            {
                _finalTaskParameters = value;
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Generates the task xml element and default attributes
        /// </summary>
        private void GenerateTaskElement()
        {
            if (_condition != null)
            {
                _taskElement.SetAttribute("Condition", _condition);
            }

            if (_continueOnError)
            {
                _taskElement.SetAttribute("ContinueOnError", "true");
            }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Close the event handles
        /// </summary>
        public void Dispose()
        {
            InternalDispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Close the event handles
        /// </summary>
        private void InternalDispose()
        {
            _taskStarted.Close();
            _taskExecuted.Close();
        }

        /// <summary>
        /// Destroy this object
        /// </summary>
        ~TaskDefinition()
        {
            InternalDispose();
        }

        #endregion
    }
}

