// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// An evaluated design-time metadatum.
    /// Parented either by a ProjectItemDefinition or a ProjectItem.
    /// </summary>
    /// <remarks>
    /// Never used to represent built-in metadata, like %(Filename). There is always a backing XML object.
    /// </remarks>
    [DebuggerDisplay("{Name}={EvaluatedValue} [{_xml.Value}]")]
    public class ProjectMetadata : IKeyed, IValued, IEquatable<ProjectMetadata>, IMetadatum
    {
        /// <summary>
        /// Parent item or item definition that this metadatum lives in.
        /// ProjectMetadata's always live in a project and always have a parent.
        /// The project can be gotten from this parent.
        /// Used to evaluate any updates.
        /// </summary>
        private readonly IProjectMetadataParent _parent;

        /// <summary>
        /// Backing XML metadata.
        /// Can never be null.
        /// </summary>
        private readonly ProjectMetadataElement _xml;

        /// <summary>
        /// Evaluated value
        /// </summary>
        private string _evaluatedValueEscaped;

        /// <summary>
        /// Any immediately previous metadatum (from item definition or item) that was overridden by this one during evaluation.
        /// This would include all metadata with the same name that lie above in the logical
        /// project file, who are on item definitions of the same type, and whose conditions evaluated to true.
        /// If this metadatum is on an item, it would include any previous metadatum with the same name on the same item whose condition
        /// evaluated to true, and following that any item definition metadata.
        /// If there are none above this is null.
        /// If the project has not been reevaluated since the last modification this value may be incorrect.
        /// </summary>
        private ProjectMetadata _predecessor;

        /// <summary>
        /// Creates a metadata backed by XML. 
        /// Constructed during evaluation of a project.
        /// </summary>
        internal ProjectMetadata(IProjectMetadataParent parent, ProjectMetadataElement xml, string evaluatedValueEscaped, ProjectMetadata predecessor)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
            ErrorUtilities.VerifyThrowArgumentNull(xml, "xml");
            ErrorUtilities.VerifyThrowArgumentNull(evaluatedValueEscaped, "evaluatedValueEscaped");

            _parent = parent;
            _xml = xml;
            _evaluatedValueEscaped = evaluatedValueEscaped;
            _predecessor = predecessor;
        }

        /// <summary>
        /// Name of the metadata
        /// </summary>
        public string Name
        {
            [DebuggerStepThrough]
            get
            { return _xml.Name; }
        }

        /// <summary>
        /// Gets the evaluated metadata value.
        /// Cannot be set directly: only the unevaluated value can be set.
        /// Is never null.
        /// </summary>
        public string EvaluatedValue
        {
            [DebuggerStepThrough]
            get
            { return EscapingUtilities.UnescapeAll(_evaluatedValueEscaped); }
        }

        /// <summary>
        /// Gets or sets the unevaluated metadata value.
        /// 
        /// As well as updating the unevaluated value, the setter updates the evaluated value, but does not affect anything else in the project until reevaluation. For example,
        ///     --if a piece of metadata named "m" is modified on item of type "i", it does not affect "j" which is evaluated from "@(j->'%(m)')" until reevaluation.
        ///     --if the unevaluated value of "m" is set to something that is modified by evaluation, such as "$(p)", the evaluated value will be set to "$(p)" until reevaluation.
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state.
        /// 
        /// Setting metadata through a ProjectItem may cause the underlying ProjectItemElement to be split, if it originated with an itemlist, wildcard, or semicolon expression,
        /// because it was clear that the caller intended to only affect that particular item.
        /// Setting metadata through a ProjectMetadata does not cause any splitting, because we assume the caller presumably intends to affect all items using the underlying
        /// ProjectMetadataElement. At least, this seems a reasonable assumption, and it avoids the need for metadata to hold a pointer to their containing items.
        /// </summary>
        /// <remarks>
        /// The containing project will be dirtied by the XML modification.  Unevaluated values are assumed to be passed in escaped as necessary. 
        /// </remarks>
        public string UnevaluatedValue
        {
            [DebuggerStepThrough]
            get
            {
                return _xml.Value;
            }

            [DebuggerStepThrough]
            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "value");
                Project.VerifyThrowInvalidOperationNotImported(_xml.ContainingProject);
                ErrorUtilities.VerifyThrowInvalidOperation(_xml.Parent != null && _xml.Parent.Parent != null && _xml.Parent.Parent.Parent != null, "OM_ObjectIsNoLongerActive");

                if (String.Equals(_xml.Value, value, StringComparison.Ordinal))
                {
                    return;
                }

                _xml.Value = value;

                // Clear out the current value of this metadata, so the new value can't refer to the old one.
                // The expansion call below otherwise passes in the parent item's metadata - including this one's
                // current value.
                _evaluatedValueEscaped = String.Empty;

                _evaluatedValueEscaped = _parent.Project.ExpandMetadataValueBestEffortLeaveEscaped(_parent, value, Location);
            }
        }

        /// <summary>
        /// Backing XML metadata.
        /// Can never be null.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ProjectMetadataElement Xml
        {
            [DebuggerStepThrough]
            get
            { return _xml; }
        }

        /// <summary>
        /// Project that this metadatum lives in.
        /// ProjectMetadata's always live in a project.
        /// </summary>
        public Project Project
        {
            [DebuggerStepThrough]
            get
            { return _parent.Project; }
        }

        /// <summary>
        /// The item type of the parent item definition or item.
        /// </summary>
        public string ItemType
        {
            get { return _parent.ItemType; }
        }

        /// <summary>
        /// Any immediately previous metadatum (from item definition or item) that was overridden by this one during evaluation.
        /// This would include all metadata with the same name that lie above in the logical
        /// project file, who are on item definitions of the same type, and whose conditions evaluated to true.
        /// If this metadatum is on an item, it would include any previous metadatum with the same name on the same item whose condition
        /// evaluated to true, and following that any item definition metadata.
        /// If there are none above this is null.
        /// If the project has not been reevaluated since the last modification this value may be incorrect.
        /// </summary>
        public ProjectMetadata Predecessor
        {
            [DebuggerStepThrough]
            get
            { return _predecessor; }
        }

        /// <summary>
        /// If the metadatum originated in an imported file, returns true.
        /// Otherwise returns false.
        /// </summary>
        public bool IsImported
        {
            get
            {
                bool isImported = !Object.ReferenceEquals(_xml.ContainingProject, _parent.Project.Xml);

                return isImported;
            }
        }

        /// <summary>
        /// Location of the element
        /// </summary>
        public ElementLocation Location
        {
            get { return _xml.Location; }
        }

        /// <summary>
        /// Location of the condition attribute
        /// </summary>
        public ElementLocation ConditionLocation
        {
            get { return _xml.ConditionLocation; }
        }

        /// <summary>
        /// Implementation of IKeyed exposing the metadata name, so metadata
        /// can be put in a dictionary conveniently.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IKeyed.Key
        {
            [DebuggerStepThrough]
            get
            { return Name; }
        }

        /// <summary>
        /// Implementation of IValued
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IValued.EscapedValue
        {
            [DebuggerStepThrough]
            get
            { return EvaluatedValueEscaped; }
        }

        /// <summary>
        /// Gets the evaluated metadata value.
        /// Cannot be set directly: only the unevaluated value can be set.
        /// Is never null.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal string EvaluatedValueEscaped
        {
            [DebuggerStepThrough]
            get
            { return _evaluatedValueEscaped; }
        }

        #region IEquatable<ProjectMetadata> Members

        /// <summary>
        /// Compares this metadata to another for equivalence.
        /// </summary>
        /// <param name="other">The other metadata</param>
        /// <returns>True if they are equivalent, false otherwise.</returns>
        bool IEquatable<ProjectMetadata>.Equals(ProjectMetadata other)
        {
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return (_xml == other._xml &&
                    _evaluatedValueEscaped == other._evaluatedValueEscaped);
        }

        #endregion

        /// <summary>
        /// Deep clone a metadatum, retaining the same parent.
        /// </summary>
        internal ProjectMetadata DeepClone()
        {
            // The new metadatum's predecessor is the same as its original's predecessor, just as the XML is the same
            // as its original's XML. Predecessors map to XML elements.
            return new ProjectMetadata(_parent, this.Xml, this.EvaluatedValueEscaped, this.Predecessor);
        }
    }
}
