// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.BuildEngine.Shared;
using System.Xml;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a collection of all Import elements in a given project file
    /// </summary>
    /// <owner>LukaszG</owner>
    public class ImportCollection : IEnumerable, ICollection
    {
        #region Fields

        private Hashtable imports;
        private Project parentProject;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor exposed to the outside world
        /// </summary>
        /// <owner>LukaszG</owner>
        internal ImportCollection(Project parentProject)
        {
            // Make sure we have a valid parent Project
            ErrorUtilities.VerifyThrow(parentProject != null,
                "Need a parent Project object to instantiate an ImportCollection.");

            this.parentProject = parentProject;

            this.imports = new Hashtable(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// IEnumerable member method for returning the enumerator
        /// </summary>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        public IEnumerator GetEnumerator()
        {
            ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Hashtable not initialized!");
            return this.imports.Values.GetEnumerator();
        }

        #endregion

        #region ICollection Members

        /// <summary>
        /// ICollection member method for copying the contents of this collection into an array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        /// <owner>LukaszG</owner>
        public void CopyTo(Array array, int index)
        {
            ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Dictionary not initialized!");
            this.imports.Values.CopyTo(array, index);
        }

        /// <summary>
        /// ICollection member property for returning the number of items in this collection
        /// </summary>
        /// <owner>LukaszG</owner>
        public int Count
        {
            get
            {
                ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Dictionary not initialized!");
                return this.imports.Count;
            }
        }

        /// <summary>
        /// ICollection member property for determining whether this collection is thread-safe
        /// </summary>
        /// <owner>LukaszG</owner>
        public bool IsSynchronized
        {
            get
            {
                ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Dictionary not initialized!");
                return this.imports.IsSynchronized;
            }
        }

        /// <summary>
        /// ICollection member property for returning this collection's synchronization object
        /// </summary>
        /// <owner>LukaszG</owner>
        public object SyncRoot
        {
            get
            {
                ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Dictionary not initialized!");
                return this.imports.SyncRoot;
            }
        }

        #endregion

        #region Members
        
        /// <summary>
        /// Read-only accessor for the Project instance that this ImportCollection belongs to.
        /// </summary>
        internal Project ParentProject
        {
            get { return parentProject; }
        }

        /// <summary>
        /// Removes all Imports from this collection. Does not alter the parent project's XML.
        /// </summary>
        /// <owner>LukaszG</owner>
        internal void Clear()
        {
            ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Hashtable not initialized!");
            this.imports.Clear();
        }

        /// <summary>
        /// Gets the Import object with the given index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        internal Import this[string index]
        {
            get
            {
                ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Hashtable not initialized!");
                return (Import)this.imports[index];
            }
            set
            {
                this.imports[index] = value;
            }
        }

        /// <summary>
        /// Copy the contents of this collection into a strongly typed array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        /// <owner>LukaszG</owner>
        public void CopyTo(Import[] array, int index)
        {
            ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Hashtable not initialized!");
            this.imports.Values.CopyTo(array, index);
        }

        /// <summary>
        /// Adds a new import to the project ,and adds a corresponding &lt;Import&gt; element to the end of the project.
        /// </summary>
        /// <param name="projectFile">Project file to add the import to</param>
        /// <param name="condition">Condition. If null, no condition is added.</param>
        public void AddNewImport(string projectFile, string condition)
        {
            ErrorUtilities.VerifyThrowArgumentLength(projectFile, "projectFile");

            XmlElement projectElement = this.parentProject.ProjectElement;
            XmlElement newImportElement = projectElement.OwnerDocument.CreateElement(XMakeElements.import, XMakeAttributes.defaultXmlNamespace);
            
            if (condition != null)
            {
                newImportElement.SetAttribute(XMakeAttributes.condition, condition);
            }
            newImportElement.SetAttribute(XMakeAttributes.project, projectFile);

            projectElement.AppendChild(newImportElement);

            this.parentProject.MarkProjectAsDirtyForReprocessXml();
        }

        /// <summary>
        /// Removes an import from the project, and removes the corresponding &lt;Import&gt; element
        /// from the project's XML.
        /// </summary>
        /// <param name="importToRemove"></param>
        /// <owner>JeffCal</owner>
        public void RemoveImport
        (
            Import importToRemove
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(importToRemove, "importToRemove");

            // Confirm that it's not an imported import.
            ErrorUtilities.VerifyThrowInvalidOperation(!importToRemove.IsImported,
                "CannotModifyImportedProjects");

            // Confirm that the import belongs to this project.
            ErrorUtilities.VerifyThrowInvalidOperation(importToRemove.ParentProject == this.parentProject,
                "IncorrectObjectAssociation", "Import", "Project");

            // Remove the Xml for the <Import> from the <Project>.
            this.parentProject.ProjectElement.RemoveChild(importToRemove.ImportElement);

            // Remove the import from our hashtable.
            this.imports.Remove(importToRemove.EvaluatedProjectPath);
            
            // Dissociate the import from the parent project.
            importToRemove.ParentProject = null;

            // The project file has been modified and needs to be saved and re-evaluated.
            this.parentProject.MarkProjectAsDirtyForReprocessXml();
        }

        #endregion
    }
}
