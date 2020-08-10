// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Data;
using System.Xml;
using System.Collections;
using System.Globalization;

#if (!STANDALONEBUILD)
using Microsoft.Internal.Performance;
#endif

using Microsoft.Build.BuildEngine.Shared;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a collection of persisted &lt;Target&gt;'s.  Each
    /// MSBuild project has exactly one TargetCollection, which includes
    /// all the imported Targets as well as the ones in the main project file.
    /// </summary>
    /// <owner>rgoel</owner>
    public class TargetCollection : IEnumerable, ICollection
    {
        #region Member Data

        // This is the hashtable of Targets (indexed by name) contained in this collection.
        Hashtable       targetTable = null;
        Project        parentProject = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of this class for the given project.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="parentProject"></param>
        internal TargetCollection
        (
            Project parentProject
        )
        {
            error.VerifyThrow(parentProject != null, "Must pass in valid parent project object.");
            this.targetTable = new Hashtable(StringComparer.OrdinalIgnoreCase);
            this.parentProject = parentProject;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Read-only accessor for parent project object.
        /// </summary>
        /// <value></value>
        /// <owner>RGoel</owner>
        internal Project ParentProject
        {
            get
            {
                return this.parentProject;
            }
        }

        /// <summary>
        /// Read-only property which returns the number of Targets contained
        /// in our collection.
        /// </summary>
        /// <owner>RGoel</owner>
        public int Count
        {
            get
            {
                error.VerifyThrow(this.targetTable != null, "Hashtable not initialized!");

                return this.targetTable.Count;
            }
        }

        /// <summary>
        /// This ICollection property tells whether this object is thread-safe.
        /// </summary>
        /// <owner>RGoel</owner>
        public bool IsSynchronized
        {
            get
            {
                return this.targetTable.IsSynchronized;
            }
        }

        /// <summary>
        /// This ICollection property returns the object to be used to synchronize
        /// access to the class.
        /// </summary>
        /// <owner>RGoel</owner>
        public object SyncRoot
        {
            get
            {
                return this.targetTable.SyncRoot;
            }
        }

        /// <summary>
        /// Gets the target with the given name, case-insensitively.
        /// Note that this also defines the .BuildItem() accessor automagically.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="index"></param>
        /// <returns>The target with the given name.</returns>
        public Target this[string index]
        {
            get
            {
                error.VerifyThrow(this.targetTable != null, "Hashtable not initialized!");

                return (Target)targetTable[index];
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This ICollection method copies the contents of this collection to an 
        /// array.
        /// </summary>
        /// <owner>RGoel</owner>
        public void CopyTo
        (
            Array array,
            int index
        )
        {
            error.VerifyThrow(this.targetTable != null, "Hashtable not initialized!");

            this.targetTable.Values.CopyTo(array, index);
        }

        /// <summary>
        /// This IEnumerable method returns an IEnumerator object, which allows
        /// the caller to enumerate through the Target objects contained in
        /// this TargetCollection.
        /// </summary>
        /// <owner>RGoel</owner>
        public IEnumerator GetEnumerator
            (
            )
        {
            error.VerifyThrow(this.targetTable != null, "Hashtable not initialized!");

            return this.targetTable.Values.GetEnumerator();
        }

        /// <summary>
        /// Adds a new Target to our collection.  This method does nothing
        /// to manipulate the project's XML content.
        /// If a target with the same name already exists, it is replaced by 
        /// the new one.
        /// </summary>
        /// <param name="newTarget">target to add</param>
        internal void AddOverrideTarget(Target newTarget)
        {
            error.VerifyThrow(this.targetTable != null, "Hashtable not initialized!");

            // if a target with this name already exists, override it
            // if it doesn't exist, just add it
            targetTable[newTarget.Name] = newTarget;
        }

        /// <summary>
        /// Adds a new &lt;Target&gt; element to the project file, at the very end.
        /// </summary>
        /// <param name="targetName"></param>
        /// <returns>The new Target object.</returns>
        public Target AddNewTarget
        (
            string targetName
        )
        {
            error.VerifyThrow(this.parentProject != null, "Need parent project.");

            // Create the XML for the new <Target> node and append it to the very end of the main project file.
            XmlElement projectElement = this.parentProject.ProjectElement;
            XmlElement newTargetElement = projectElement.OwnerDocument.CreateElement(XMakeElements.target, XMakeAttributes.defaultXmlNamespace);
            newTargetElement.SetAttribute(XMakeAttributes.name, targetName);
            projectElement.AppendChild(newTargetElement);

            // Create a new Target object, and add it to our hash table.
            Target newTarget = new Target(newTargetElement, this.parentProject, false);
            this.targetTable[targetName] = newTarget;

            // The project file has been modified and needs to be saved and re-evaluated.
            // Also though, adding/removing a target requires us to re-walk all the XML 
            // in order to re-compute out the "first logical target" as well as re-compute
            // the target overriding rules.
            this.parentProject.MarkProjectAsDirtyForReprocessXml();

            return newTarget;
        }

        /// <summary>
        /// Removes a target from the project, and removes the corresponding &lt;Target&gt; element
        /// from the project's XML.
        /// </summary>
        /// <param name="targetToRemove"></param>
        /// <owner>RGoel</owner>
        public void RemoveTarget
        (
            Target targetToRemove
        )
        {
            error.VerifyThrowArgumentNull(targetToRemove, nameof(targetToRemove));

            // Confirm that it's not an imported target.
            error.VerifyThrowInvalidOperation(!targetToRemove.IsImported,
                "CannotModifyImportedProjects");

            // Confirm that the target belongs to this project.
            error.VerifyThrowInvalidOperation(targetToRemove.ParentProject == this.parentProject,
                "IncorrectObjectAssociation", "Target", "Project");

            // Remove the Xml for the <Target> from the <Project>.
            this.parentProject.ProjectElement.RemoveChild(targetToRemove.TargetElement);

            // Remove the target from our hashtable, if it exists.  It might not exist, and that's okay.
            // The reason it might not exist is because of target overriding, and the fact that 
            // our hashtable only stores the *last* target of a given name.
            if ((Target)this.targetTable[targetToRemove.Name] == targetToRemove)
            {
                this.targetTable.Remove(targetToRemove.Name);
            }

            // Dissociate the target from the parent project.
            targetToRemove.ParentProject = null;

            // The project file has been modified and needs to be saved and re-evaluated.
            // Also though, adding/removing a target requires us to re-walk all the XML 
            // in order to re-compute the "first logical target" as well as re-compute
            // the target overriding rules.
            this.parentProject.MarkProjectAsDirtyForReprocessXml();
        }

        /// <summary>
        /// Checks if a target with given name already exists
        /// </summary>
        /// <param name="targetName">name of the target we're looking for</param>
        /// <returns>true if the target already exists</returns>
        public bool Exists
        (
            string targetName
        )
        {
            error.VerifyThrow(this.targetTable != null, "Hashtable not initialized!");
            return targetTable.ContainsKey(targetName);
        }

        /// <summary>
        /// Removes all Targets from our collection.  This method does nothing
        /// to manipulate the project's XML content.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void Clear
            (
            )
        {
            error.VerifyThrow(this.targetTable != null, "Hashtable not initialized!");

            this.targetTable.Clear();
        }

        #endregion
    }
}
