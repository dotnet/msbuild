// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Collections;

using Microsoft.Build.BuildEngine.Shared;
using System.Xml;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
    /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
    /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
    /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
    /// 
    /// This class represents a collection of all Import elements in a given project file
    /// </summary>
    /// <remarks>
    /// <format type="text/markdown"><![CDATA[
    /// ## Remarks
    /// > [!WARNING]
    /// > This class (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
    /// > <xref:Microsoft.Build.Construction>
    /// > <xref:Microsoft.Build.Evaluation>
    /// > <xref:Microsoft.Build.Execution>
    /// ]]></format>
    /// </remarks>
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// IEnumerable member method for returning the enumerator
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        /// <owner>LukaszG</owner>
        public IEnumerator GetEnumerator()
        {
            ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Hashtable not initialized!");
            return this.imports.Values.GetEnumerator();
        }

        #endregion

        #region ICollection Members

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// ICollection member method for copying the contents of this collection into an array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        /// <owner>LukaszG</owner>
        public void CopyTo(Array array, int index)
        {
            ErrorUtilities.VerifyThrow(this.imports != null, "ImportCollection's Dictionary not initialized!");
            this.imports.Values.CopyTo(array, index);
        }

        /// <summary>
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// ICollection member property for returning the number of items in this collection
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// ICollection member property for determining whether this collection is thread-safe
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// ICollection member property for returning this collection's synchronization object
        /// </summary>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
        /// This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// <see href="/dotnet/api/microsoft.build.construction">Microsoft.Build.Construction</see>
        /// <see href="/dotnet/api/microsoft.build.evaluation">Microsoft.Build.Evaluation</see>
        /// <see href="/dotnet/api/microsoft.build.execution">Microsoft.Build.Execution</see>
        /// 
        /// Copy the contents of this collection into a strongly typed array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
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
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        public void AddNewImport(string projectFile, string condition)
        {
            ErrorUtilities.VerifyThrowArgumentLength(projectFile, nameof(projectFile));

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
        /// <remarks>
        /// <format type="text/markdown"><![CDATA[
        /// ## Remarks
        /// > [!WARNING]
        /// > This method (and the whole namespace) is deprecated. Please use the classes in these namespaces instead: 
        /// > <xref:Microsoft.Build.Construction>
        /// > <xref:Microsoft.Build.Evaluation>
        /// > <xref:Microsoft.Build.Execution>
        /// ]]></format>
        /// </remarks>
        /// <owner>JeffCal</owner>
        public void RemoveImport
        (
            Import importToRemove
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(importToRemove, nameof(importToRemove));

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
