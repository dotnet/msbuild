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
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// Defines the elements of a target within a project file
    /// </summary>
    internal class TargetDefinition : IDisposable
    {
        #region Private Data

        /// <summary>
        /// Name of the target
        /// </summary>
        private string _name;

        /// <summary>
        /// Condition of the target
        /// </summary>
        private string _condition;

        /// <summary>
        /// Target Inputs
        /// </summary>
        private string _inputs;

        /// <summary>
        /// Target Outputs
        /// </summary>
        private string _outputs;

        /// <summary>
        /// This target depends on the following targets
        /// </summary>
        private string _dependsOnTargets;

        /// <summary>
        /// Target XML element
        /// </summary>
        private XmlElement _targetXmlElement;

        /// <summary>
        /// Tasks which have been added to this definition
        /// </summary>
        private Dictionary<string, TaskDefinition> _tasks;

        /// <summary>
        /// Target result
        /// </summary>
        private TargetResult _result;

        #endregion

        #region Public Method

        /// <summary>
        /// Default constructor
        /// </summary>
        public TargetDefinition(string name, XmlDocument projectXmlDoc) :
            this(name, null, null, null, null, projectXmlDoc)
        {
        }

        /// <summary>
        /// Constructor allows you to set the condition of a target
        /// </summary>
        public TargetDefinition(string name, string condition, XmlDocument projectXmlDoc) :
            this(name, null, null, condition, null, projectXmlDoc)
        {
        }

        /// <summary>
        /// Constructor allows you to set the condition and dependent targets of the target
        /// </summary>
        public TargetDefinition(string name, string condition, string dependsOnTargets, XmlDocument projectXmlDoc) :
            this(name, null, null, condition, dependsOnTargets, projectXmlDoc)
        {
        }

        /// <summary>
        /// Constructor allows you to set the inputs, outputs and condition of a target
        /// </summary>
        public TargetDefinition(string name, string inputs, string outputs, string condition, XmlDocument projectXmlDoc) :
            this(name, inputs, outputs, condition, null, projectXmlDoc)
        {
        }

        /// <summary>
        /// Constructor allows you to set all the elements of the object
        /// </summary>
        public TargetDefinition(string name, string inputs, string outputs, string condition, string dependsOnTargets, XmlDocument projectXmlDoc)
        {
            _name = name;
            _inputs = inputs;
            _outputs = outputs;
            _condition = condition;
            _dependsOnTargets = dependsOnTargets;
            _tasks = new Dictionary<string, TaskDefinition>();
            _result = null;
            _targetXmlElement = projectXmlDoc.CreateElement("Target", @"http://schemas.microsoft.com/developer/msbuild/2003");
            GenerateTargetElementXml();
        }

        #endregion

        #region Public properties

        /// <summary>
        /// XMLElement representing the target. This only returns what has been created so far.
        /// </summary>
        public XmlElement FinalTargetXmlElement
        {
            get
            {
                return _targetXmlElement;
            }
        }

        /// <summary>
        /// List of tasks that have been added to this definition
        /// </summary>
        public Dictionary<string, TaskDefinition> TasksCollection
        {
            get
            {
                return _tasks;
            }
        }

        /// <summary>
        /// Target result
        /// </summary>
        public TargetResult Result
        {
            get
            {
                return _result;
            }
            set
            {
                _result = value;
            }
        }

        /// <summary>
        /// Target Name
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Adds a task to the target
        /// </summary>
        /// <param name="task"></param>
        public void AddTask(TaskDefinition task)
        {
            _targetXmlElement.AppendChild(task.FinalTaskXmlElement);
            _tasks.Add(task.Name, task);
        }

        /// <summary>
        /// Adds the OnError Element to the target
        /// </summary>
        public void AddOnError(string onErrorTargets, string onErrorCondition)
        {
            XmlDocument xmlDoc = _targetXmlElement.OwnerDocument;
            XmlElement targetOnErrorElement = xmlDoc.CreateElement("OnError", @"http://schemas.microsoft.com/developer/msbuild/2003");
            targetOnErrorElement.SetAttribute("ExecuteTargets", onErrorTargets);
            if (onErrorCondition != null)
            {
                targetOnErrorElement.SetAttribute("Condition", onErrorCondition);
            }

            _targetXmlElement.AppendChild(targetOnErrorElement);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Generates the base target XMLElement object
        /// </summary>
        /// <param name="projectFileName"></param>
        private void GenerateTargetElementXml()
        {
            _targetXmlElement.SetAttribute("Name", _name);

            if (_dependsOnTargets != null)
            {
                _targetXmlElement.SetAttribute("DependsOnTargets", _dependsOnTargets);
            }

            if (_condition != null)
            {
                _targetXmlElement.SetAttribute("Condition", _condition);
            }

            if (_inputs != null)
            {
                _targetXmlElement.SetAttribute("Inputs", _inputs);
            }

            if (_outputs != null)
            {
                _targetXmlElement.SetAttribute("Outputs", _outputs);
            }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Clear lists created
        /// </summary>
        public void Dispose()
        {
            _tasks.Clear();
        }

        #endregion
    }
}

