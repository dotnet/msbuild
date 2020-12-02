// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a single task.
    /// </summary>
    /// <owner>rgoel</owner>
    public class BuildTask
    {
        #region Member Data

        // The task XML element, if this is a persisted target.  
        private XmlElement taskElement = null;

        // This is the "Condition" attribute on the task element.
        private XmlAttribute conditionAttribute = null;

        // This is the "ContinueOnError" attribute on the task element.
        private XmlAttribute continueOnErrorAttribute = null;

        // The target to which this task belongs.
        private Target parentTarget= null;

        // The name of the task.
        private string taskName = String.Empty;

        // If this is a persisted task element, this boolean tells us whether
        // it came from the main project file or an imported project file.
        private bool importedFromAnotherProject = false;

        // This is the optional host object for this particular task.  The actual task
        // object will get passed this host object, and can communicate with it as it
        // wishes.  Although it is declared generically as an "Object" here, the actual
        // task will cast it to whatever it expects.
        private ITaskHost hostObject = null;

        #endregion

        #region Constructors
        /// <summary>
        /// This constructor initializes a persisted task from an existing task
        /// element which exists either in the main project file or one of the
        /// imported files.
        /// </summary>
        /// <param name="taskElement"></param>
        /// <param name="parentTarget"></param>
        /// <param name="importedFromAnotherProject"></param>
        /// <owner>rgoel</owner>
        internal BuildTask
        (
            XmlElement      taskElement,
            Target          parentTarget,
            bool            importedFromAnotherProject
        )
        {
            // Make sure a valid node has been given to us.
            error.VerifyThrow(taskElement != null, "Need a valid XML node.");

            // Make sure a valid target has been given to us.
            error.VerifyThrow(parentTarget != null, "Need a valid target parent.");

            this.taskElement = taskElement;
            this.parentTarget = parentTarget;
            this.conditionAttribute = null;
            this.continueOnErrorAttribute = null;
            this.importedFromAnotherProject = importedFromAnotherProject;

            // Loop through all the attributes on the task element.
            foreach (XmlAttribute taskAttribute in taskElement.Attributes)
            {
                switch (taskAttribute.Name)
                {
                    case XMakeAttributes.condition:
                        this.conditionAttribute = taskAttribute;
                        break;

                    case XMakeAttributes.continueOnError:
                        this.continueOnErrorAttribute = taskAttribute;
                        break;

                    // this only makes sense in the context of the new OM, 
                    // so just ignore it.  
                    case XMakeAttributes.msbuildRuntime:
                        // do nothing
                        break;

                    // this only makes sense in the context of the new OM, 
                    // so just ignore it.  
                    case XMakeAttributes.msbuildArchitecture:
                        // do nothing
                        break;
                }
            }

            this.taskName = taskElement.Name;
        }

        /// <summary>
        /// Default constructor.  This is not allowed, because it leaves the
        /// BuildTask in a bad state. But we have to have it, otherwise FXCop
        /// complains.
        /// </summary>
        /// <owner>rgoel</owner>
        private BuildTask
            (
            )
        {
            // Not allowed.
        }

        #endregion

        #region Properties

        /// <summary>
        /// Read-only accessor for XML element representing this task.
        /// </summary>
        /// <value></value>
        /// <owner>RGoel</owner>
        internal XmlElement TaskXmlElement
        {
            get
            {
                return this.taskElement;
            }
        }

        /// <summary>
        /// Accessor for the task's "name" element.
        /// </summary>
        /// <owner>RGoel</owner>
        public string Name
        {
            get
            {
                return this.taskName;
            }
        }

        /// <summary>
        /// Accessor for the task's "condition".
        /// </summary>
        /// <owner>RGoel</owner>
        public string Condition
        {
            get
            {
                return (this.conditionAttribute == null) ? String.Empty : this.conditionAttribute.Value;
            }

            set
            {
                // If this Task object is not actually represented by a 
                // task element in the project file, then do not allow
                // the caller to set the condition.
                error.VerifyThrowInvalidOperation(this.taskElement != null,
                    "CannotSetCondition");

                // If this task was imported from another project, we don't allow modifying it.
                error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject,
                    "CannotModifyImportedProjects");

                this.conditionAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(taskElement, XMakeAttributes.condition, value);

                this.MarkTaskAsDirty();
            }
        }

        /// <summary>
        /// Accessor for the task's "ContinueOnError".
        /// </summary>
        /// <owner>RGoel</owner>
        public bool ContinueOnError
        {
            get
            {
                Expander expander = new Expander(parentTarget.ParentProject.evaluatedProperties, parentTarget.ParentProject.evaluatedItemsByName);

                // NOTE: if the ContinueOnError attribute contains an item metadata reference, this property is meaningless
                // because we are unable to batch -- this property will always be 'false' in that case
                if ((continueOnErrorAttribute != null) &&
                    ConversionUtilities.ConvertStringToBool
                    (
                        expander.ExpandAllIntoString(continueOnErrorAttribute.Value, continueOnErrorAttribute)
                    ))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            set
            {
                // If this Task object is not actually represented by a 
                // task element in the project file, then do not allow
                // the caller to set the attribute.
                error.VerifyThrowInvalidOperation(this.taskElement != null,
                    "CannotSetContinueOnError");

                // If this task was imported from another project, we don't allow modifying it.
                error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject,
                    "CannotModifyImportedProjects");

                if (value)
                {
                    this.taskElement.SetAttribute(XMakeAttributes.continueOnError, "true");
                }
                else
                {
                    // Set the new "ContinueOnError" attribute on the task element.
                    this.taskElement.SetAttribute(XMakeAttributes.continueOnError, "false");
                }

                this.continueOnErrorAttribute = this.taskElement.Attributes[XMakeAttributes.continueOnError];

                this.MarkTaskAsDirty();
            }
        }

        /// <summary>
        /// System.Type object corresponding to the task class that implements
        /// the functionality that runs this task object.
        /// </summary>
        /// <owner>RGoel</owner>
        public Type Type
        {
            get
            {
                // put verify throw for target name
                ErrorUtilities.VerifyThrow(this.ParentTarget != null, "ParentTarget should not be null");
                Engine parentEngine = this.ParentTarget.ParentProject.ParentEngine;
                Project parentProject = this.ParentTarget.ParentProject;
                string projectFileOfTaskNode = XmlUtilities.GetXmlNodeFile(taskElement, parentProject.FullFileName);
                BuildEventContext taskContext = new BuildEventContext
                                               (
                                                   parentProject.ProjectBuildEventContext.NodeId,
                                                   this.ParentTarget.Id,
                                                   parentProject.ProjectBuildEventContext.ProjectContextId,
                                                   parentProject.ProjectBuildEventContext.TaskId
                                               );

                int handleId = parentEngine.EngineCallback.CreateTaskContext(parentProject, ParentTarget, null, taskElement,
                                                                                EngineCallback.inProcNode, taskContext);
                EngineLoggingServices loggingServices = parentEngine.LoggingServices;
                TaskExecutionModule taskExecutionModule = parentEngine.NodeManager.TaskExecutionModule;

                TaskEngine taskEngine = new TaskEngine(taskElement, null,
                    projectFileOfTaskNode, parentProject.FullFileName, loggingServices, handleId, taskExecutionModule, taskContext);

                ErrorUtilities.VerifyThrowInvalidOperation(taskEngine.FindTask(),
                    "MissingTaskError", taskName, parentEngine.ToolsetStateMap[ParentTarget.ParentProject.ToolsVersion].ToolsPath);

                return taskEngine.TaskClass.Type;
            }
        }

        /// <summary>
        /// Accessor for the "host object" for this task.
        /// </summary>
        /// <owner>RGoel</owner>
        public ITaskHost HostObject
        {
            get
            {
                return this.hostObject;
            }

            set
            {
                this.hostObject = value;
            }
        }

        /// <summary>
        /// Accessor for parent Target object.
        /// </summary>
        /// <value></value>
        /// <owner>RGoel</owner>
        internal Target ParentTarget
        {
            get
            {
                return this.parentTarget;
            }

            set
            {
                this.parentTarget = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This retrieves the list of all parameter names from the element
        /// node of this task. Note that it excludes anything that a specific
        /// property is exposed for or that isn't valid here (Name, Condition,
        /// ContinueOnError).
        ///
        /// Note that if there are none, it returns string[0], rather than null,
        /// as it makes writing foreach statements over the return value so
        /// much simpler.
        /// </summary>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        public string[] GetParameterNames()
        {
            if (this.taskElement == null)
            {
                return new string[0];
            }

            ArrayList list = new ArrayList();
            foreach (XmlAttribute attrib in this.taskElement.Attributes)
            {
                string attributeValue = attrib.Name;

                if (!XMakeAttributes.IsSpecialTaskAttribute(attributeValue))
                {
                    list.Add(attributeValue);
                }
            }

            return (string[])list.ToArray(typeof(string));
        }

        /// <summary>
        /// This retrieves an arbitrary attribute from the task element.  These
        /// are attributes that the project author has placed on the task element
        /// that have no meaning to MSBuild other than that they get passed to the
        /// task itself as arguments.
        /// </summary>
        /// <owner>RGoel</owner>
        public string GetParameterValue
        (
            string attributeName
        )
        {
            // You can only request the value of user-defined attributes.  The well-known
            // ones, like "ContinueOnError" for example, are accessed through other means.
            error.VerifyThrowArgument(!XMakeAttributes.IsSpecialTaskAttribute(attributeName),
                "CannotAccessKnownAttributes", attributeName);

            error.VerifyThrowInvalidOperation(this.taskElement != null,
                "CannotUseParameters");

            // If this is a persisted Task, grab the attribute directly from the
            // task element.
            return taskElement.GetAttribute(attributeName) ?? string.Empty;
        }

        /// <summary>
        /// This sets an arbitrary attribute on the task element.  These
        /// are attributes that the project author has placed on the task element
        /// that get passed in to the task.
        ///
        /// This optionally escapes the parameter value so it will be treated as a literal.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="parameterValue"></param>
        /// <param name="treatParameterValueAsLiteral"></param>
        /// <owner>RGoel</owner>
        public void SetParameterValue
            (
            string parameterName,
            string parameterValue,
            bool treatParameterValueAsLiteral
            )
        {
            this.SetParameterValue(parameterName, treatParameterValueAsLiteral ? EscapingUtilities.Escape(parameterValue) : parameterValue);
        }

        /// <summary>
        /// This sets an arbitrary attribute on the task element.  These
        /// are attributes that the project author has placed on the task element
        /// that get passed in to the task.
        /// </summary>
        /// <owner>RGoel</owner>
        public void SetParameterValue
        (
            string parameterName,
            string parameterValue
        )
        {
            // You can only set the value of user-defined attributes.  The well-known
            // ones, like "ContinueOnError" for example, are accessed through other means.
            error.VerifyThrowArgument(!XMakeAttributes.IsSpecialTaskAttribute(parameterName),
                "CannotAccessKnownAttributes", parameterName);

            // If this task was imported from another project, we don't allow modifying it.
            error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject,
                "CannotModifyImportedProjects");

            error.VerifyThrowInvalidOperation(this.taskElement != null,
                "CannotUseParameters");

            // If this is a persisted Task, set the attribute directly on the
            // task element.
            taskElement.SetAttribute(parameterName, parameterValue);

            this.MarkTaskAsDirty();
        }

        /// <summary>
        /// Adds an Output tag to this task element
        /// </summary>
        /// <param name="taskParameter"></param>
        /// <param name="itemName"></param>
        /// <owner>LukaszG</owner>
        public void AddOutputItem(string taskParameter, string itemName)
        {
            AddOutputItem(taskParameter, itemName, null);
        }

        /// <summary>
        /// Adds an Output tag to this task element, with a condition
        /// </summary>
        /// <param name="taskParameter"></param>
        /// <param name="itemName"></param>
        /// <param name="condition">May be null</param>
        internal void AddOutputItem(string taskParameter, string itemName, string condition)
        {
            // If this task was imported from another project, we don't allow modifying it.
            error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject,
                "CannotModifyImportedProjects");

            error.VerifyThrowInvalidOperation(this.taskElement != null,
                "CannotUseParameters");

            XmlElement newOutputElement = this.taskElement.OwnerDocument.CreateElement(XMakeElements.output, XMakeAttributes.defaultXmlNamespace);
            newOutputElement.SetAttribute(XMakeAttributes.taskParameter, taskParameter);
            newOutputElement.SetAttribute(XMakeAttributes.itemName, itemName);

            if (condition != null)
            {
                newOutputElement.SetAttribute(XMakeAttributes.condition, condition);
            }

            this.taskElement.AppendChild(newOutputElement);

            this.MarkTaskAsDirty();
        }

        /// <summary>
        /// Adds an Output tag to this task element
        /// </summary>
        /// <param name="taskParameter"></param>
        /// <param name="propertyName"></param>
        /// <owner>LukaszG</owner>
        public void AddOutputProperty(string taskParameter, string propertyName)
        {
            // If this task was imported from another project, we don't allow modifying it.
            error.VerifyThrowInvalidOperation(!this.importedFromAnotherProject,
                "CannotModifyImportedProjects");

            error.VerifyThrowInvalidOperation(this.taskElement != null,
                "CannotUseParameters");

            XmlElement newOutputElement = this.taskElement.OwnerDocument.CreateElement(XMakeElements.output, XMakeAttributes.defaultXmlNamespace);
            newOutputElement.SetAttribute(XMakeAttributes.taskParameter, taskParameter);
            newOutputElement.SetAttribute(XMakeAttributes.propertyName, propertyName);

            this.taskElement.AppendChild(newOutputElement);

            this.MarkTaskAsDirty();
        }

        /// <summary>
        /// Runs the task associated with this object.
        /// </summary>
        /// <owner>RGoel</owner>
        public bool Execute
            (
            )
        {
            error.VerifyThrowInvalidOperation(this.taskElement != null,
                "CannotExecuteUnassociatedTask");

            error.VerifyThrowInvalidOperation(this.parentTarget != null,
                "CannotExecuteUnassociatedTask");

            return this.parentTarget.ExecuteOneTask(this.taskElement, this.HostObject);
        }

        /// <summary>
        /// Indicates that something has changed within the task element, so the project
        /// needs to be saved and re-evaluated at next build.  Send the "dirtiness"
        /// notification up the chain.
        /// </summary>
        /// <owner>RGoel</owner>
        private void MarkTaskAsDirty
            (
            )
        {
               
            
                // This is a change to the contents of the target.
                this.ParentTarget?.MarkTargetAsDirty();
            
        }

        #endregion
    }
}
