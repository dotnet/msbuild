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
    /// This class represents a single Import element in a project file
    /// </summary>
    /// <owner>LukaszG</owner>
    public class Import : IItemPropertyGrouping
    {
        #region Properties

        private Project parentProject = null;

        /// <summary>
        /// Returns the parent MSBuild Project object.
        /// </summary>
        internal Project ParentProject
        {
            get { return this.parentProject; }
            set { this.parentProject = value; }
        }

        private XmlElement importElement = null;

        /// <summary>
        /// Returns the source XmlElement this Import is based on.
        /// </summary>
        internal XmlElement ImportElement
        {
            get { return this.importElement; }
        }

        private bool importedFromAnotherProject;

        /// <summary>
        /// Returns true if this Import came from an imported project
        /// </summary>
        /// <owner>LukaszG</owner>
        public bool IsImported
        {
            get { return this.importedFromAnotherProject; }
        }

        private XmlAttribute projectPathAttribute = null;

        /// <summary>
        /// Returns the original import path from the Import element
        /// </summary>
        /// <owner>LukaszG</owner>
        public string ProjectPath
        {
            get 
            { 
                return this.projectPathAttribute?.Value; 
            }
            set
            {
                ImportElement.SetAttribute(XMakeAttributes.project, value);
                ParentProject.MarkProjectAsDirtyForReprocessXml();
            }
        }

        /// <summary>
        /// Internal accessor for the project path XML attribute
        /// </summary>
        /// <owner>LukaszG</owner>
        internal XmlAttribute ProjectPathAttribute
        {
            get { return this.projectPathAttribute; }
        }

        private string evaluatedProjectPath = null;

        /// <summary>
        /// Returns the full evaluated import path
        /// </summary>
        /// <owner>LukaszG</owner>
        public string EvaluatedProjectPath
        {
            get { return this.evaluatedProjectPath; }
        }

        private XmlAttribute conditionAttribute = null;

        /// <summary>
        /// The condition string for this UsingTask
        /// </summary>
        /// <owner>LukaszG</owner>
        public string Condition
        {
            get 
            { 
                return this.conditionAttribute?.Value; 
            }
            set
            {
                ImportElement.SetAttribute(XMakeAttributes.condition, value);
                if (conditionAttribute == null)
                {
                    conditionAttribute = ImportElement.Attributes[XMakeAttributes.condition];
                }
                ParentProject.MarkProjectAsDirtyForReprocessXml();
            }
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
        /// Internal constructor
        /// </summary>
        /// <param name="importElement"></param>
        /// <param name="isImported"></param>
        /// <owner>LukaszG</owner>
        internal Import(XmlElement importElement, Project parentProject, bool isImported)
        {
            this.importedFromAnotherProject = isImported;

            // Make sure the <Import> node has been given to us.
            ErrorUtilities.VerifyThrow(importElement != null,
                "Need an XML node representing the <Import> element.");

            this.importElement = importElement;
            
            // Make sure we have a valid parent Project
            ErrorUtilities.VerifyThrow(parentProject != null,
                "Need a parent Project object to instantiate an Import.");
            
            this.parentProject = parentProject;

            // Make sure this really is the <Import> node.
            ProjectXmlUtilities.VerifyThrowElementName(importElement, XMakeElements.import);

            // Loop through the list of attributes on the <Import> element.
            foreach (XmlAttribute importAttribute in importElement.Attributes)
            {
                switch (importAttribute.Name)
                {
                    // The "project" attribute points us at the project file to import.
                    case XMakeAttributes.project:
                        // Just store the attribute value at this point. We want to make sure that we evaluate any
                        // Condition attribute before looking at the Project attribute - if the Condition is going to be false,
                        // it's legitimate for the value of the Project attribute to be completely invalid.
                        // For example, <Import Project="$(A)" Condition="$(A)!=''"/> should not cause an error
                        // that the Project attribute is empty.
                        this.projectPathAttribute = importAttribute;
                        break;

                    // If the "condition" attribute is present, then it must evaluate to "true".
                    case XMakeAttributes.condition:
                        this.conditionAttribute = importAttribute;
                        break;

                    // We've come across an attribute in the <Import> element that we
                    // don't recognize.  Fail due to invalid project file.
                    default:
                        ProjectXmlUtilities.ThrowProjectInvalidAttribute(importAttribute);
                        break;
                }
            }

            ProjectErrorUtilities.VerifyThrowInvalidProject((this.projectPathAttribute != null) && (this.projectPathAttribute.Value.Length != 0),
                importElement, "MissingRequiredAttribute",
                XMakeAttributes.project, XMakeElements.import);

            // Make sure this node has no children.  Our schema doesn't support having
            // children beneath the <Import> element.
            if (importElement.HasChildNodes)
            {
                // Don't put the "if" condition inside the first parameter to
                // VerifyThrow..., because we'll get null reference exceptions,
                // since the parameter importElement.FirstChild.Name is being
                // passed in regardless of whether the condition holds true or not.
                ProjectXmlUtilities.ThrowProjectInvalidChildElement(importElement.FirstChild);
            }
        }

        #endregion

        /// <summary>
        /// Sets the full evaluated project path for this import.
        /// </summary>
        /// <param name="newEvaluatedProjectPath"></param>
        /// <owner>LukaszG</owner>
        internal void SetEvaluatedProjectPath(string newEvaluatedProjectPath)
        {
            this.evaluatedProjectPath = newEvaluatedProjectPath;
        }
    }
}
