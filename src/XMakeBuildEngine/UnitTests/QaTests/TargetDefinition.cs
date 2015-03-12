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
        private string name;

        /// <summary>
        /// Condition of the target
        /// </summary>
        private string condition;

        /// <summary>
        /// Target Inputs
        /// </summary>
        private string inputs;
        
        /// <summary>
        /// Target Outputs
        /// </summary>
        private string outputs;

        /// <summary>
        /// This target depends on the following targets
        /// </summary>
        private string dependsOnTargets;

        /// <summary>
        /// Target XML element
        /// </summary>
        private XmlElement targetXmlElement;

        /// <summary>
        /// Tasks which have been added to this definition
        /// </summary>
        private Dictionary<string, TaskDefinition> tasks;

        /// <summary>
        /// Target result
        /// </summary>
        private TargetResult result;

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
            this.name = name;
            this.inputs = inputs;
            this.outputs = outputs;
            this.condition = condition;
            this.dependsOnTargets = dependsOnTargets;
            this.tasks = new Dictionary<string, TaskDefinition>();
            this.result = null;
            this.targetXmlElement = projectXmlDoc.CreateElement("Target", @"http://schemas.microsoft.com/developer/msbuild/2003");
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
                return this.targetXmlElement;
            }
        }

        /// <summary>
        /// List of tasks that have been added to this definition
        /// </summary>
        public Dictionary<string, TaskDefinition> TasksCollection
        {
            get
            {
                return this.tasks;
            }
        }

        /// <summary>
        /// Target result
        /// </summary>
        public TargetResult Result
        {
            get
            {
                return this.result;
            }
            set
            {
                this.result = value;
            }
        }

        /// <summary>
        /// Target Name
        /// </summary>
        public string Name
        {
            get
            {
                return this.name;
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
            this.targetXmlElement.AppendChild(task.FinalTaskXmlElement);
            this.tasks.Add(task.Name, task);
        }

        /// <summary>
        /// Adds the OnError Element to the target
        /// </summary>
        public void AddOnError(string onErrorTargets, string onErrorCondition)
        {
            XmlDocument xmlDoc = targetXmlElement.OwnerDocument;
            XmlElement targetOnErrorElement = xmlDoc.CreateElement("OnError", @"http://schemas.microsoft.com/developer/msbuild/2003");
            targetOnErrorElement.SetAttribute("ExecuteTargets", onErrorTargets);
            if (onErrorCondition != null)
            {
                targetOnErrorElement.SetAttribute("Condition", onErrorCondition);
            }

            this.targetXmlElement.AppendChild(targetOnErrorElement);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Generates the base target XMLElement object
        /// </summary>
        /// <param name="projectFileName"></param>
        private void GenerateTargetElementXml()
        {
            this.targetXmlElement.SetAttribute("Name", this.name);

            if (this.dependsOnTargets != null)
            {
                this.targetXmlElement.SetAttribute("DependsOnTargets", this.dependsOnTargets);
            }

            if (this.condition != null)
            {
                this.targetXmlElement.SetAttribute("Condition", this.condition);
            }

            if (this.inputs != null)
            {
                this.targetXmlElement.SetAttribute("Inputs", this.inputs);
            }

            if (this.outputs != null)
            {
                this.targetXmlElement.SetAttribute("Outputs", this.outputs);
            }   
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Clear lists created
        /// </summary>
        public void Dispose()
        {
            this.tasks.Clear();
        }

        #endregion
    }
}

