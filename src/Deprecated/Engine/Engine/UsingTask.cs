// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a single UsingTask element in a project file
    /// </summary>
    /// <owner>LukaszG</owner>
    public class UsingTask
    {
        #region Properties

        private bool importedFromAnotherProject;

        /// <summary>
        /// Returns true if this UsingTask was imported from another project
        /// </summary>
        /// <owner>LukaszG</owner>
        public bool IsImported
        {
            get { return this.importedFromAnotherProject; }
        }

        private XmlAttribute taskNameAttribute = null;

        /// <summary>
        /// The task name
        /// </summary>
        /// <owner>LukaszG</owner>
        public string TaskName
        {
            get { return this.taskNameAttribute?.Value; }
        }

        /// <summary>
        /// Internal accessor for the task name XML attribute
        /// </summary>
        internal XmlAttribute TaskNameAttribute
        {
            get { return this.taskNameAttribute; }
        }

        private XmlAttribute assemblyNameAttribute = null;

        /// <summary>
        /// The name of the assembly containing the task
        /// </summary>
        /// <owner>LukaszG</owner>
        public string AssemblyName
        {
            get { return this.assemblyNameAttribute?.Value; }
        }

        /// <summary>
        /// Internal accessor for the assembly name XML attribute
        /// </summary>
        internal XmlAttribute AssemblyNameAttribute
        {
            get { return this.assemblyNameAttribute; }
        }

        private XmlAttribute assemblyFileAttribute = null;

        /// <summary>
        /// The assembly file containing the task
        /// </summary>
        /// <owner>LukaszG</owner>
        public string AssemblyFile
        {
            get { return this.assemblyFileAttribute?.Value; }
        }

        /// <summary>
        /// Internal accessor for the assembly file XML attribute
        /// </summary>
        internal XmlAttribute AssemblyFileAttribute
        {
            get { return this.assemblyFileAttribute; } 
        }

        private XmlAttribute conditionAttribute = null;

        /// <summary>
        /// The condition string for this UsingTask
        /// </summary>
        /// <owner>LukaszG</owner>
        public string Condition
        {
            get { return this.conditionAttribute?.Value; }
        }

        /// <summary>
        /// Internal accessor for the condition XML attribute
        /// </summary>
        internal XmlAttribute ConditionAttribute
        {
            get { return this.conditionAttribute; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new UsingTask object
        /// </summary>
        /// <param name="usingTaskNode"></param>
        /// <param name="isImported"></param>
        /// <owner>LukaszG</owner>
        internal UsingTask(XmlElement usingTaskNode, bool isImported)
        {
            this.importedFromAnotherProject = isImported;

            // make sure this really is a <UsingTask> tag
            ErrorUtilities.VerifyThrow(usingTaskNode.Name == XMakeElements.usingTask,
                "Expected <{0}> element; received <{1}> element.", XMakeElements.usingTask, usingTaskNode.Name);

            bool illegalChildElementFound = false;
            XmlElement illegalChildElement = null;

            foreach (XmlElement childElement in usingTaskNode.ChildNodes)
            {
                switch (childElement.Name)
                {
                    case XMakeElements.usingTaskBody:
                        // ignore
                        break;
                    case XMakeElements.usingTaskParameter:
                        // ignore
                        break;
                    case XMakeElements.usingTaskParameterGroup:
                        // ignore 
                        break;
                    default:
                        illegalChildElementFound = true;
                        illegalChildElement = childElement;
                        break;
                }

                if (illegalChildElementFound)
                {
                    break;
                }
            }

            // UsingTask has no valid child elements in 3.5 syntax, but in 4.0 syntax it does. 
            // So ignore any valid 4.0 child elements and try to load the project as usual, but
            // still error out if something we don't expect is found. 
            if (illegalChildElementFound)
            {
                ProjectXmlUtilities.ThrowProjectInvalidChildElement(illegalChildElement);
            }

            foreach (XmlAttribute usingTaskAttribute in usingTaskNode.Attributes)
            {
                switch (usingTaskAttribute.Name)
                {
                    // get the task name
                    case XMakeAttributes.taskName:
                        taskNameAttribute = usingTaskAttribute;
                        break;

                    // get the assembly name or the assembly file/path, whichever is specified...
                    case XMakeAttributes.assemblyName:
                        assemblyNameAttribute = usingTaskAttribute;
                        break;

                    case XMakeAttributes.assemblyFile:
                        assemblyFileAttribute = usingTaskAttribute;
                        break;
                        
                    // ignore any RequiredRuntime XML attribute
                    // (we'll make this actually do something when we run on a CLR other than v2.0)
                    case XMakeAttributes.requiredRuntime:
                        // Do nothing
                        break;                       

                    // get the condition, if any
                    case XMakeAttributes.condition:
                        conditionAttribute = usingTaskAttribute;
                        break;

                    // This is only recognized by the new OM:
                    // Just ignore it
                    case XMakeAttributes.requiredPlatform:
                        // Do nothing
                        break;

                    // This is only recognized by the new OM:
                    // Just ignore it
                    case XMakeAttributes.taskFactory:
                        // Do nothing
                        break;

                    // This is only recognized by the new OM:
                    // Just ignore it
                    case XMakeAttributes.runtime:
                        // Do nothing
                        break;

                    // This is only recognized by the new OM:
                    // Just ignore it
                    case XMakeAttributes.architecture:
                        // Do nothing
                        break;

                    default:
                        ProjectXmlUtilities.ThrowProjectInvalidAttribute(usingTaskAttribute); 
                        break;
                }
            }

            ProjectErrorUtilities.VerifyThrowInvalidProject(taskNameAttribute != null,
                usingTaskNode, "MissingRequiredAttribute", XMakeAttributes.taskName, XMakeElements.usingTask);
            ProjectErrorUtilities.VerifyThrowInvalidProject(taskNameAttribute.Value.Length > 0,
                taskNameAttribute, "InvalidAttributeValue", taskNameAttribute.Value, XMakeAttributes.taskName, XMakeElements.usingTask);

            ProjectErrorUtilities.VerifyThrowInvalidProject((assemblyNameAttribute != null) || (assemblyFileAttribute != null),
                usingTaskNode, "UsingTaskAssemblySpecification", XMakeElements.usingTask, XMakeAttributes.assemblyName, XMakeAttributes.assemblyFile);
            ProjectErrorUtilities.VerifyThrowInvalidProject((assemblyNameAttribute == null) || (assemblyFileAttribute == null),
                usingTaskNode, "UsingTaskAssemblySpecification", XMakeElements.usingTask, XMakeAttributes.assemblyName, XMakeAttributes.assemblyFile);

            ProjectErrorUtilities.VerifyThrowInvalidProject((assemblyNameAttribute == null) || (assemblyNameAttribute.Value.Length > 0),
                assemblyNameAttribute, "InvalidAttributeValue", String.Empty, XMakeAttributes.assemblyName, XMakeElements.usingTask);
            ProjectErrorUtilities.VerifyThrowInvalidProject((assemblyFileAttribute == null) || (assemblyFileAttribute.Value.Length > 0),
                assemblyFileAttribute, "InvalidAttributeValue", String.Empty, XMakeAttributes.assemblyFile, XMakeElements.usingTask);
        }

        #endregion
    }
}
