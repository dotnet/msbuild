// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// An evaluated design-time item
    /// </summary>
    /// <remarks>
    /// Edits to this object will indirectly dirty the containing project because they will modify the backing XML.
    /// </remarks>
    /// <comment>
    /// We cannot use a copy-on-write table for the metadata, as ProjectMetadata objects are mutable. However,
    /// we do use it for build-time items.
    /// </comment>
    [DebuggerDisplay("{ItemType}={EvaluatedInclude} [{UnevaluatedInclude}] #DirectMetadata={DirectMetadataCount}")]
    public class ProjectItem : IItem<ProjectMetadata>, IProjectMetadataParent, IItemData
    {
        /// <summary>
        /// Project that this item lives in.
        /// ProjectItems always live in a project.
        /// Used to get item definitions and project directory.
        /// </summary>
        private readonly Project _project;

        /// <summary>
        /// Fragment of the original include that led to this item,
        /// with properties expanded but not wildcards.  Escaped as necessary
        /// </summary>
        /// <remarks>
        /// This is ONLY used to figure out %(RecursiveDir) when it is requested.
        /// It's likely too expensive to figure that out if it isn't needed, so we store
        /// the necessary material here.
        /// </remarks>
        private readonly string _evaluatedIncludeBeforeWildcardExpansionEscaped;

        /// <summary>
        /// Item definitions are stored in one single table shared by all items of a particular item type.
        ///
        /// When an item is created from another item, such as by using an expression like Include="@(x)",
        /// any item definition metadata those source items have must override any item definition metadata
        /// associated with the new item type.
        ///
        /// Copying all those item definition metadata into real metadata on this item would be very inefficient, because
        /// it would turn a single shared table into a separate table for every item.
        ///
        /// Instead, we get a reference to the item definition of the source items, and consult
        /// that table before we consult our own item type's item definition. Since item definitions can't change at this point,
        /// it's safe to reference their original table.
        ///
        /// If our item gets copied again, we need a reference to the inherited item definition and we need the real item
        /// definition of the source items. Thus a list is created. On copying, a list is created, beginning with a clone
        /// of any list the source item had, and ending with the item definition list of the source item type.
        ///
        /// When we look up a metadata value we look at
        /// (1) directly associated metadata and built-in metadata
        /// (2) the inherited item definition list, starting from the top
        /// (3) the item definition associated with our item type
        /// </summary>
        private readonly List<ProjectItemDefinition> _inheritedItemDefinitions;

        /// <summary>
        /// Backing XML item.
        /// Can never be null
        /// </summary>
        private ProjectItemElement _xml;

        /// <summary>
        /// Evaluated include.
        /// The original XML may have evaluated to several of these items,
        /// each with a different include.
        /// May be empty, for example from expanding an empty list or from a transform with undefined metadata.
        /// Escaped as necessary
        /// </summary>
        private string _evaluatedIncludeEscaped;

        /// <summary>
        /// Collection of metadata that link the XML metadata and evaluated metadata.
        /// Since evaluation has occurred, this is an unordered collection.
        /// May be null.
        /// </summary>
        /// <remarks>
        /// Lazily created, as there are lots of items
        /// that have no metadata at all. The value is one of:
        /// - null, when there is no direct metadata;
        /// - a single <see cref="ProjectMetadata"/>;
        /// - a <see cref="ProjectMetadata"/> array of up to <see cref="CompactMetadataMaxCount"/> entries;
        /// - a <see cref="SparseDirectMetadata"/>, to preserve free slots after removals;
        /// - a <see cref="PropertyDictionary{T}"/> after promotion.
        ///
        /// A single writer publishes compact snapshots without modifying existing slots.
        /// Promoted dictionaries synchronize readers and the writer internally.
        /// </remarks>
        private volatile object _directMetadata;

        private const int CompactMetadataMaxCount = 8;

        /// <summary>
        /// Cached values of derivable item-spec modifiers. All time-based metadata are computed on demand.
        /// </summary>
        private ItemSpecModifiers.Cache _cachedModifiers;

        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectItem(ProjectItemElement xml, Project project)
        {
            this._project = project;
            this._xml = xml;
        }

        /// <summary>
        /// Called by the Evaluator during project evaluation.
        /// Direct metadata may be null, indicating no metadata. It is assumed to have already been cloned.
        /// Inherited item definition metadata may be null. It is assumed that its list has already been cloned.
        /// ProjectMetadata objects may be shared with other items.
        /// </summary>
        internal ProjectItem(
                             Project project,
                             ProjectItemElement xml,
                             string evaluatedIncludeEscaped,
                             string evaluatedIncludeBeforeWildcardExpansionEscaped,
                             object directMetadataCloned,
                             List<ProjectItemDefinition> inheritedItemDefinitionsCloned)
        {
            Assumed.NotNull(project);
            ArgumentNullException.ThrowIfNull(xml);

            // Orcas accidentally allowed empty includes if they resulted from expansion: we preserve that bug
            ArgumentNullException.ThrowIfNull(evaluatedIncludeEscaped);
            ArgumentNullException.ThrowIfNull(evaluatedIncludeBeforeWildcardExpansionEscaped);

            _xml = xml;
            _project = project;
            _evaluatedIncludeEscaped = evaluatedIncludeEscaped;
            _evaluatedIncludeBeforeWildcardExpansionEscaped = evaluatedIncludeBeforeWildcardExpansionEscaped;
            _directMetadata = directMetadataCloned;
            _inheritedItemDefinitions = inheritedItemDefinitionsCloned;
        }

        internal virtual ProjectItemLink Link => null;

        /// <inheritdoc cref="IItemData.EnumerateMetadata"/>
        IEnumerable<KeyValuePair<string, string>> IItemData.EnumerateMetadata() => Metadata.Select(m => new KeyValuePair<string, string>(m.Name, m.EvaluatedValue));

        /// <summary>
        /// Backing XML item.
        /// Can never be null.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ProjectItemElement Xml
        {
            [DebuggerStepThrough]
            get
            { return _xml; }
        }

        /// <summary>
        /// Gets or sets the type of this item.
        /// </summary>
        public string ItemType
        {
            [DebuggerStepThrough]
            get
            { return _xml.ItemType; }
            set
            {
                if (Link != null)
                {
                    Link.ChangeItemType(value);
                }
                else
                {
                    ChangeItemType(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the unevaluated value of the Include.
        /// </summary>
        public string UnevaluatedInclude
        {
            [DebuggerStepThrough]
            get
            {
                return _xml.Include;
            }

            set
            {
                Rename(value);
            }
        }

        /// <inheritdoc cref="IItemData.EvaluatedInclude"/>
        /// <remarks>
        /// Gets the evaluated value of the include, unescaped.
        /// </remarks>
        public string EvaluatedInclude
        {
            [DebuggerStepThrough]
            get
            { return Link != null ? Link.EvaluatedInclude : EscapingUtilities.UnescapeAll(_evaluatedIncludeEscaped); }
        }

        /// <summary>
        /// Gets the evaluated value of the include, escaped as necessary.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IItem.EvaluatedIncludeEscaped
        {
            [DebuggerStepThrough]
            get
            { return _evaluatedIncludeEscaped; }
        }

        /// <summary>
        /// The directory of the project being built
        /// Never null: If there is no project filename yet, it will use the current directory
        /// </summary>
        string IItem.ProjectDirectory
        {
            get { return _project.DirectoryPath; }
        }

        /// <summary>
        /// Project that this item lives in.
        /// ProjectItems always live in a project.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Project Project
        {
            [DebuggerStepThrough]
            get
            { return _project; }
        }

        /// <summary>
        /// If the item originated in an imported file, returns true.
        /// Otherwise returns false.
        /// </summary>
        public bool IsImported
        {
            get
            {
                bool isImported = !Object.ReferenceEquals(_xml.ContainingProject, _project.Xml);

                return isImported;
            }
        }

        /// <summary>
        /// Metadata directly on the item, if any.
        /// Does not include metadata from item definitions.
        /// Does not include built-in metadata.
        /// Never returns null.
        /// A view obtained from a nonempty item reflects later changes. Each enumeration uses a snapshot.
        /// </summary>
        public IEnumerable<ProjectMetadata> DirectMetadata
        {
            get
            {
                if (Link != null)
                {
                    return Link.DirectMetadata;
                }

                return _directMetadata is null
                    ? ReadOnlyEmptyCollection<ProjectMetadata>.Instance
                    : new DirectMetadataView(this);
            }
        }

        /// <summary>
        /// Count of direct metadata on this item, if any.
        /// Does NOT count any metadata inherited from item definitions.
        /// Does not count built-in metadata, such as "FullPath".
        /// </summary>
        public int DirectMetadataCount
        {
            [DebuggerStepThrough]
            get
            { return Link != null ? Link.DirectMetadata.Count : GetCount(_directMetadata); }
        }

        /// <summary>
        /// Metadata on the item, if any.  Includes metadata specified by the definition, if any.
        /// If there is no metadata, returns an empty collection.
        /// Does not include built-in metadata, such as "FullPath".
        /// Get the values of built-in metadata using <see cref="GetMetadataValue(string)"/>.
        /// This is a read-only collection. To modify the metadata, use <see cref="SetMetadataValue(string, string)"/>.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "This is a reasonable choice. API review approved")]
        public ICollection<ProjectMetadata> Metadata
        {
            [DebuggerStepThrough]
            get
            { return Link != null ? Link.MetadataCollection : MetadataCollection; }
        }

        IEnumerable<ProjectMetadata> IItem<ProjectMetadata>.Metadata => Metadata;

        /// <summary>
        /// Count of metadata on this item, if any.
        /// Includes any metadata inherited from item definitions.
        /// Includes both custom and built-in metadata.
        /// </summary>
        public int MetadataCount => Metadata.Count + ItemSpecModifiers.All.Length;

        /// <summary>
        /// Implementation of IKeyed exposing the item type, so items
        /// can be put in a dictionary conveniently.
        /// </summary>
        string IKeyed.Key
        {
            [DebuggerStepThrough]
            get
            { return ItemType; }
        }

        /// <summary>
        /// Internal version of <see cref="Metadata">Metadata</see> that returns
        /// a full ICollection.
        /// Unordered collection of evaluated metadata on the item.
        /// If there is no metadata, returns an empty collection.
        /// Does not include built-in metadata.
        /// Includes any from item definitions not masked by directly set metadata.
        /// This is a read-only collection. To modify the metadata, use <see cref="SetMetadataValue(string, string)"/>.
        /// </summary>
        internal ICollection<ProjectMetadata> MetadataCollection
        {
            get
            {
                RetrievableEntryHashSet<ProjectMetadata> allMetadata = new RetrievableEntryHashSet<ProjectMetadata>(MSBuildNameIgnoreCaseComparer.Default);

                // Lowest priority: regular item definitions
                ProjectItemDefinition itemDefinition;
                if (_project.ItemDefinitions.TryGetValue(ItemType, out itemDefinition))
                {
                    foreach (ProjectMetadata metadataFromDefinition in itemDefinition.Metadata)
                    {
                        allMetadata[metadataFromDefinition.Name] = metadataFromDefinition;
                    }
                }

                // Next, any inherited item definitions. Front of the list is highest priority,
                // so walk backwards.
                if (_inheritedItemDefinitions != null)
                {
                    for (int i = _inheritedItemDefinitions.Count - 1; i >= 0; i--)
                    {
                        foreach (ProjectMetadata metadatum in _inheritedItemDefinitions[i].Metadata)
                        {
                            allMetadata[metadatum.Name] = metadatum;
                        }
                    }
                }

                // Finally any direct metadata win.
                foreach (ProjectMetadata metadatum in EnumerateDirectMetadata(_directMetadata))
                {
                    allMetadata[metadatum.Name] = metadatum;
                }

                return allMetadata.Values;
            }
        }

        /// <summary>
        /// Accesses the unescaped evaluated include prior to wildcard expansion
        /// </summary>
        internal string EvaluatedIncludeBeforeWildcardExpansion
        {
            [DebuggerStepThrough]
            get
            { return EscapingUtilities.UnescapeAll(_evaluatedIncludeBeforeWildcardExpansionEscaped); }
        }

        /// <summary>
        /// Accesses the evaluated include prior to wildcard expansion
        /// </summary>
        internal string EvaluatedIncludeBeforeWildcardExpansionEscaped
        {
            [DebuggerStepThrough]
            get
            { return _evaluatedIncludeBeforeWildcardExpansionEscaped; }
        }

        /// <summary>
        /// Accesses the inherited item definitions, if any.
        /// Used ONLY by the ProjectInstance, when cloning a ProjectItem.
        /// </summary>
        internal List<ProjectItemDefinition> InheritedItemDefinitions
        {
            [DebuggerStepThrough]
            get
            { return _inheritedItemDefinitions; }
        }

        /// <summary>
        /// Gets an evaluated metadata on this item.
        /// Potentially includes a metadata from an item definition.
        /// Does not return built-in metadata, such as "FullPath".
        /// Returns null if not found.
        /// </summary>
        public ProjectMetadata GetMetadata(string name)
        {
            if (Link != null)
            {
                return Link.GetMetadata(name);
            }

            ArgumentException.ThrowIfNullOrEmpty(name);

            return FindDirectMetadata(name) ?? GetItemDefinitionMetadata(name);
        }

        /// <summary>
        /// Get the evaluated value of a metadata on this item, possibly from an item definition.
        /// Returns empty string if it does not exist.
        /// To determine whether a piece of metadata does not exist vs. simply has no value, use <see cref="HasMetadata(string)">HasMetadata</see>.
        /// May be used to access the value of built-in metadata, such as "FullPath".
        /// Attempting to get built-in metadata on a value that is not a valid path throws InvalidOperationException.
        /// </summary>
        public string GetMetadataValue(string name)
        {
            return Link != null ? Link.GetMetadataValue(name) : EscapingUtilities.UnescapeAll(((IItem)this).GetMetadataValueEscaped(name));
        }

        /// <summary>
        /// Returns true if a particular piece of metadata is defined on this item,
        /// otherwise false.
        /// Includes built-in metadata and metadata inherited from item definitions.
        /// </summary>
        public bool HasMetadata(string name)
        {
            if (Link != null)
            {
                return Link.HasMetadata(name);
            }

            if (FindDirectMetadata(name) != null)
            {
                return true;
            }

            if (ItemSpecModifiers.IsItemSpecModifier(name))
            {
                return true;
            }

            ProjectMetadata metadatum = GetItemDefinitionMetadata(name);
            if (metadatum != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// See <see cref="GetMetadataValue(string)">GetMetadataValue</see> for a more detailed explanation.
        /// Returns the escaped value of the metadatum requested.
        /// </summary>
        string IItem.GetMetadataValueEscaped(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            string value = FindDirectMetadata(name)?.EvaluatedValueEscaped;

            if (value == null)
            {
                value = GetBuiltInMetadataEscaped(name);
            }

            if (value == null)
            {
                ProjectMetadata metadatum = GetItemDefinitionMetadata(name);

                if (metadatum != null && Expander<ProjectProperty, ProjectItem>.ExpressionMayContainExpandableExpressions(metadatum.EvaluatedValueEscaped))
                {
                    Expander<ProjectProperty, ProjectItem> expander = new Expander<ProjectProperty, ProjectItem>(null, null, new BuiltInMetadataTable(this), FileSystems.Default);

                    value = expander.ExpandIntoStringLeaveEscaped(metadatum.EvaluatedValueEscaped, ExpanderOptions.ExpandBuiltInMetadata, metadatum.Location);
                }
                else if (metadatum != null)
                {
                    return metadatum.EvaluatedValueEscaped;
                }
            }

            return value ?? String.Empty;
        }

        /// <summary>
        /// Gets any existing ProjectMetadata on the item, or
        /// else any on an applicable item definition.
        /// This is ONLY called during evaluation.
        /// Does not return built-in metadata, such as "FullPath".
        /// Returns null if not found.
        /// </summary>
        ProjectMetadata IItem<ProjectMetadata>.GetMetadata(string name)
        {
            return GetMetadata(name);
        }

        /// <summary>
        /// Adds a ProjectMetadata to the item.
        /// This is ONLY called during evaluation and does not affect the XML.
        /// </summary>
        ProjectMetadata IItem<ProjectMetadata>.SetMetadata(ProjectMetadataElement metadataElement, string evaluatedInclude)
        {
            ProjectMetadata predecessor = GetMetadata(metadataElement.Name);

            ProjectMetadata metadatum = new ProjectMetadata(this, metadataElement, evaluatedInclude, predecessor);

            SetDirectMetadatum(metadatum);

            return metadatum;
        }

        /// <summary>
        /// Adds metadata with the specified name and value to the item.
        /// Updates an existing metadata if one already exists with the same name on the item directly, as opposed to inherited from an item definition.
        /// Updates the evaluated project, but does not affect anything else in the project until reevaluation. For example,
        /// if a piece of metadata named "m" is added on item of type "i", it does not affect "j" which is evaluated from "@(j->'%(m)')" until reevaluation.
        /// Also if the unevaluated value of "m" is set to something that is modified by evaluation, such as "$(p)", the evaluated value will be set to literally "$(p)" until reevaluation.
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state without a reevaluation.
        /// Returns the new or existing metadatum.
        /// </summary>
        /// <remarks>Unevaluated value is assumed to be escaped as necessary</remarks>
        public ProjectMetadata SetMetadataValue(string name, string unevaluatedValue)
        {
            return Link != null ? Link.SetMetadataValue(name, unevaluatedValue, false) :
                                  SetMetadataOperation(name, unevaluatedValue, propagateMetadataToSiblingItems: false);
        }

        /// <summary>
        /// Overload of <see cref="SetMetadataValue(string,string)"/>. Adds the option of not splitting the item element and thus affecting all sibling items.
        /// Sibling items are defined as all ProjectItem instances that were created from the same item element.
        ///
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state without a reevaluation
        /// </summary>
        /// /// <param name="name">Metadata name</param>
        /// <param name="unevaluatedValue">Metadata value</param>
        /// <param name="propagateMetadataToSiblingItems">
        /// If true, adds direct metadata to the <see cref="ProjectItemElement"/> from which this <see cref="ProjectItem"/> originated. The intent is to affect all other sibling items.
        /// </param>
        /// <returns>Returns the new or existing metadatum.</returns>
        public ProjectMetadata SetMetadataValue(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems)
        {
            return Link != null ? Link.SetMetadataValue(name, unevaluatedValue, propagateMetadataToSiblingItems) :
                                  SetMetadataOperation(name, unevaluatedValue, propagateMetadataToSiblingItems: propagateMetadataToSiblingItems);
        }

        private ProjectMetadata SetMetadataOperation(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems)
        {
            Project.VerifyThrowInvalidOperationNotImported(_xml.ContainingProject);

            XmlUtilities.VerifyThrowArgumentValidElementName(name);
            ErrorUtilities.VerifyThrowArgument(!ItemSpecModifiers.IsItemSpecModifier(name), "ItemSpecModifierCannotBeCustomMetadata", name);
            ErrorUtilities.VerifyThrowInvalidOperation(!XMakeElements.ReservedItemNames.Contains(name), "CannotModifyReservedItemMetadata", name);
            ErrorUtilities.VerifyThrowInvalidOperation(_xml.Parent?.Parent != null, "OM_ObjectIsNoLongerActive");

            if (!propagateMetadataToSiblingItems)
            {
                _project.SplitItemElementIfNecessary(_xml);
            }

            ProjectMetadata metadatum = FindDirectMetadata(name);

            if (metadatum != null)
            {
                metadatum.UnevaluatedValue = unevaluatedValue;
            }
            else
            {
                ProjectMetadataElement metadatumXml = _xml.AddMetadata(name, unevaluatedValue);

                string evaluatedValueEscaped = _project.ExpandMetadataValueBestEffortLeaveEscaped(this, unevaluatedValue, metadatumXml.Location);

                metadatum = new ProjectMetadata(this, metadatumXml, evaluatedValueEscaped, null /* predecessor unknown */);
            }

            if (!propagateMetadataToSiblingItems)
            {
                SetDirectMetadatum(metadatum);
            }
            else
            {
                var siblingItems = _project.Items.Where(i => i._xml == _xml);

                foreach (var siblingItem in siblingItems)
                {
                    siblingItem.SetDirectMetadatum(metadatum.DeepClone());
                }
            }

            return metadatum;
        }

        /// <summary>
        /// Removes any metadata with the specified name.
        /// Returns true if the evaluated metadata existed, otherwise false.
        /// If the metadata name is one of the built-in metadata, like "FullPath", throws InvalidArgumentException.
        /// If the metadata originates in an item definition, and was not overridden, throws InvalidOperationException.
        /// </summary>
        public bool RemoveMetadata(string name)
        {
            if (Link != null)
            {
                return Link.RemoveMetadata(name);
            }

            ArgumentException.ThrowIfNullOrEmpty(name);
            ErrorUtilities.VerifyThrowArgument(!ItemSpecModifiers.IsItemSpecModifier(name), "ItemSpecModifierCannotBeCustomMetadata", name);
            Project.VerifyThrowInvalidOperationNotImported(_xml.ContainingProject);
            ErrorUtilities.VerifyThrowInvalidOperation(_xml.Parent?.Parent != null, "OM_ObjectIsNoLongerActive");

            ProjectMetadata metadatum = FindDirectMetadata(name);

            if (metadatum == null)
            {
                ProjectMetadata itemDefinitionMetadata = GetItemDefinitionMetadata(name);
                ErrorUtilities.VerifyThrowInvalidOperation(itemDefinitionMetadata == null, "OM_CannotRemoveMetadataOriginatingFromItemDefinition", name);
                return false;
            }

            _project.SplitItemElementIfNecessary(_xml);

            // New metadata objects may have been created
            metadatum = FindDirectMetadata(name);

            _xml.RemoveChild(metadatum.Xml);
            RemoveDirectMetadatum(name);

            return true;
        }

        /// <summary>
        /// Renames the item.
        /// Equivalent to setting the <see cref="UnevaluatedInclude"/> value.
        /// Generally, no expansion occurs. This is because it would potentially result in several items,
        /// which is not meaningful semantics when renaming a single item.
        /// However if the item does not need to be split (which would invalidate its ProjectItemElement),
        /// and the new value expands to exactly one item, then its evaluated include is updated
        /// with the expanded value, rather than the unexpanded value.
        /// </summary>
        /// <remarks>
        /// Even if the new value expands to zero items, we do not expand it.
        /// The common case we are interested in for expansion here is setting something
        /// like "$(sourcesroot)\foo.cs" and expanding that to a single item.
        /// If say "@(foo)" is set as the new name, and it expands to blank, that might
        /// be surprising to the host and maybe even unhandled, if on full reevaluation
        /// it wouldn’t expand to blank. That’s why we're being cautious and supporting
        /// the most common scenario only.
        /// Many hosts will do a ReevaluateIfNecessary before reading anyway.
        /// </remarks>
        public void Rename(string name)
        {
            if (Link != null)
            {
                Link.Rename(name);
                return;
            }

            Project.VerifyThrowInvalidOperationNotImported(_xml.ContainingProject);
            ErrorUtilities.VerifyThrowInvalidOperation(_xml.Parent?.Parent != null, "OM_ObjectIsNoLongerActive");

            if (String.Equals(UnevaluatedInclude, name, StringComparison.Ordinal))
            {
                return;
            }

            _cachedModifiers.Clear(); // Clear cached values

            if (_xml.Count == 0 /* no metadata */ && _project.IsSuitableExistingItemXml(_xml, name, null /* no metadata */) && !FileMatcher.HasWildcardsSemicolonItemOrPropertyReferences(name))
            {
                _evaluatedIncludeEscaped = name;

                // Fast item lookup tables are invalid now.
                // Make sure that when the caller invokes ReevaluateIfNecessary() that they'll be refreshed.
                Project.MarkDirty();
                return;
            }

            bool splitOccurred = _project.SplitItemElementIfNecessary(_xml);

            _xml.Include = name;

            if (splitOccurred)
            {
                _evaluatedIncludeEscaped = name;
            }
            else
            {
                _evaluatedIncludeEscaped = _project.ExpandItemIncludeBestEffortLeaveEscaped(_xml);
            }
        }

        #region IMetadataTable Members

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name specified.
        /// If no value is available, returns empty string.
        /// Value, if escaped, remains escaped.
        /// </summary>
        string IMetadataTable.GetEscapedValue(string name)
        {
            string value = ((IMetadataTable)this).GetEscapedValue(null, name);

            return value;
        }

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
        /// If no value is available, returns empty string.
        /// If item type is null, it is ignored, otherwise it must match.
        /// Value, if escaped, remains escaped.
        /// </summary>
        string IMetadataTable.GetEscapedValue(string itemType, string name)
        {
            string value = ((IMetadataTable)this).GetEscapedValueIfPresent(itemType, name);

            return value ?? String.Empty;
        }

        /// <summary>
        /// Returns the value if it exists.
        /// If no value is available, returns null.
        /// If item type is null, it is ignored, otherwise it must match.
        /// Value, if escaped, remains escaped.
        /// </summary>
        string IMetadataTable.GetEscapedValueIfPresent(string itemType, string name)
        {
            if (itemType == null || MSBuildNameIgnoreCaseComparer.Default.Equals(ItemType, itemType))
            {
                string value = ((IItem)this).GetMetadataValueEscaped(name);

                if (value.Length > 0 || HasMetadata(name))
                {
                    return value;
                }
            }

            return null;
        }

        #endregion

        /// <summary>
        /// Changes the item type of this item.
        /// Until reevaluation puts it in the correct place, it will be placed at
        /// the end of the list of items of its new type.
        /// </summary>
        /// <remarks>
        /// This is a little involved, as it requires replacing
        /// the XmlElement, and updating the project's datastructures.
        /// </remarks>
        internal void ChangeItemType(string newItemType)
        {
            ArgumentException.ThrowIfNullOrEmpty(newItemType, "ItemType");
            Project.VerifyThrowInvalidOperationNotImported(_xml.ContainingProject);
            ErrorUtilities.VerifyThrowInvalidOperation(_xml.Parent?.Parent != null, "OM_ObjectIsNoLongerActive");

            if (String.Equals(ItemType, newItemType, StringComparison.Ordinal))
            {
                return;
            }

            _project.SplitItemElementIfNecessary(_xml);

            _project.RemoveItemBeforeItemTypeChange(this);

            // xml.ChangeItemType will throw if new item type is invalid. Make sure we re-add the item anyway
            try
            {
                _xml.ChangeItemType(newItemType);
            }
            finally
            {
                _project.ReAddExistingItemAfterItemTypeChange(this);
            }
        }

        /// <summary>
        /// Creates new xml objects for itself, disconnecting from the old xml objects.
        /// Called ONLY by <see cref="Microsoft.Build.Evaluation.Project.SplitItemElementIfNecessary(ProjectItemElement)"/>
        /// </summary>
        /// <remarks>
        /// Called when breaking up a single ProjectItemElement that evaluates into several ProjectItems.
        /// </remarks>
        internal void SplitOwnItemElement()
        {
            ProjectItemElement oldXml = _xml;

            _xml = _xml.ContainingProject.CreateItemElement(ItemType, ((IItem)this).EvaluatedIncludeEscaped);

            oldXml.Parent.InsertBeforeChild(_xml, oldXml);

            // ProjectMetadata objects may be being shared with other ProjectItem objects,
            // or originate from item definitions, so it is necessary to replace ours with
            // new ones. The old snapshot is immutable, so it can be enumerated directly
            // while the new metadata are added. They are added in enumeration order without
            // holes, as the dictionary used previously did when it was recreated here.
            object previous = _directMetadata;
            _directMetadata = null;

            foreach (ProjectMetadata metadatum in EnumerateDirectMetadata(previous))
            {
                SetMetadataValue(metadatum.Name, metadatum.EvaluatedValueEscaped);
            }
        }

        /// <summary>
        /// Gets the number of metadata in a direct metadata snapshot.
        /// </summary>
        private static int GetCount(object directMetadata)
            => directMetadata switch
            {
                null => 0,
                ProjectMetadata => 1,
                SparseDirectMetadata sparse => sparse.Count,
                PropertyDictionary<ProjectMetadata> dictionary => dictionary.Count,
                _ => ((ProjectMetadata[])directMetadata).Length,
            };

        private static IEnumerable<ProjectMetadata> EnumerateDirectMetadata(object directMetadata)
            => directMetadata switch
            {
                null => Array.Empty<ProjectMetadata>(),
                ProjectMetadata single => EnumerateSingle(single),
                SparseDirectMetadata sparse => EnumerateOccupiedSlots(sparse.Slots),
                PropertyDictionary<ProjectMetadata> dictionary => dictionary.GetSnapshot(),
                _ => (ProjectMetadata[])directMetadata,
            };

        private static IEnumerable<ProjectMetadata> EnumerateSingle(ProjectMetadata metadata)
        {
            yield return metadata;
        }

        private static IEnumerable<ProjectMetadata> EnumerateOccupiedSlots(ProjectMetadata[] slots)
        {
            foreach (ProjectMetadata metadatum in slots)
            {
                if (metadatum != null)
                {
                    yield return metadatum;
                }
            }
        }

        // Like the ordered hash set, look up the evaluated name using its indexed hash and the live XML name.
        // A rename to a different hash is visible in enumeration but not in lookups until reevaluation.
        private static bool MatchesName(ProjectMetadata metadata, string name, int nameHash)
            => ((metadata.InitialNameHashCode ^ nameHash) & int.MaxValue) == 0
                && MSBuildNameIgnoreCaseComparer.Default.Equals(metadata.Name, name);

        /// <summary>
        /// Returns the index of the metadatum with the specified name in the slots, or -1.
        /// Names are compared case-insensitively.
        /// </summary>
        private static int IndexOfDirectMetadatum(ProjectMetadata[] slots, string name)
        {
            int nameHash = MSBuildNameIgnoreCaseComparer.Default.GetHashCode(name);
            for (int i = 0; i < slots.Length; i++)
            {
                ProjectMetadata metadatum = slots[i];
                if (metadatum != null && MatchesName(metadatum, name, nameHash))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets the direct metadatum with the specified name, or null if there is none.
        /// Does not consider item definitions or built-in metadata.
        /// </summary>
        private ProjectMetadata FindDirectMetadata(string name)
        {
            object directMetadata = _directMetadata;
            return directMetadata switch
            {
                null => null,
                ProjectMetadata single => MatchesName(single, name, MSBuildNameIgnoreCaseComparer.Default.GetHashCode(name)) ? single : null,
                PropertyDictionary<ProjectMetadata> dictionary => dictionary.GetWithMatchingHash(name),
                SparseDirectMetadata sparse => FindInSlots(sparse.Slots, name),
                _ => FindInSlots((ProjectMetadata[])directMetadata, name),
            };
        }

        private static ProjectMetadata FindInSlots(ProjectMetadata[] slots, string name)
        {
            int index = IndexOfDirectMetadatum(slots, name);
            return index < 0 ? null : slots[index];
        }

        /// <summary>
        /// Adds the metadatum to the direct metadata.
        /// Replaces any direct metadatum with the same name.
        /// </summary>
        private void SetDirectMetadatum(ProjectMetadata metadatum)
        {
            ArgumentNullException.ThrowIfNull(metadatum);

            if (_directMetadata is PropertyDictionary<ProjectMetadata> dictionary)
            {
                dictionary.Set(metadatum);
            }
            else
            {
                _directMetadata = WithDirectMetadatum(_directMetadata, metadatum);
            }
        }

        /// <summary>
        /// Returns a snapshot that has the metadatum in addition to the metadata in <paramref name="current"/>.
        /// The slot of each metadatum is the same as in the hash set that was used previously, so that
        /// the enumeration order does not change: a metadatum with the same name is replaced in its slot,
        /// a new metadatum goes into the most recently freed slot, and otherwise it is appended.
        /// </summary>
        private static object WithDirectMetadatum(object current, ProjectMetadata metadatum)
        {
            if (current == null)
            {
                return metadatum;
            }

            if (current is ProjectMetadata single)
            {
                return MatchesName(single, metadatum.Name, metadatum.InitialNameHashCode)
                    ? metadatum
                    : (ProjectMetadata[])[single, metadatum];
            }

            ProjectMetadata[] slots = current is SparseDirectMetadata sparseSlots ? sparseSlots.Slots : (ProjectMetadata[])current;
            int index = IndexOfDirectMetadatum(slots, metadatum.Name);
            ProjectMetadata[] updatedSlots;

            if (index >= 0)
            {
                if (ReferenceEquals(slots[index], metadatum))
                {
                    return current;
                }

                updatedSlots = (ProjectMetadata[])slots.Clone();
                updatedSlots[index] = metadatum;

                return current is SparseDirectMetadata replaced
                    ? new SparseDirectMetadata(updatedSlots, replaced.FreeSlots)
                    : updatedSlots;
            }

            if (current is SparseDirectMetadata sparse)
            {
                int[] freeSlots = sparse.FreeSlots;
                updatedSlots = (ProjectMetadata[])slots.Clone();
                updatedSlots[freeSlots[freeSlots.Length - 1]] = metadatum;

                if (freeSlots.Length == 1)
                {
                    return updatedSlots;
                }

                int[] updatedFreeSlots = new int[freeSlots.Length - 1];
                Array.Copy(freeSlots, updatedFreeSlots, updatedFreeSlots.Length);

                return new SparseDirectMetadata(updatedSlots, updatedFreeSlots);
            }

            if (slots.Length == CompactMetadataMaxCount)
            {
                // ProjectMetadata is mutable. Use the same ordered dictionary as before compact storage.
                var entries = new RetrievableValuedEntryHashSet<ProjectMetadata>(CompactMetadataMaxCount + 1, MSBuildNameIgnoreCaseComparer.Default);
                foreach (ProjectMetadata entry in slots)
                {
                    entries.AddWithHashCode(entry, entry.InitialNameHashCode);
                }

                entries.Add(metadatum);
                return new PropertyDictionary<ProjectMetadata>(entries);
            }

            updatedSlots = new ProjectMetadata[slots.Length + 1];
            Array.Copy(slots, updatedSlots, slots.Length);
            updatedSlots[slots.Length] = metadatum;

            return updatedSlots;
        }

        /// <summary>
        /// Removes the direct metadatum with the specified name, if any.
        /// Returns true if it was present.
        /// </summary>
        private bool RemoveDirectMetadatum(string name)
        {
            object current = _directMetadata;
            if (current is PropertyDictionary<ProjectMetadata> dictionary)
            {
                bool removed = dictionary.Remove(name);
                if (removed && dictionary.Count == 0)
                {
                    _directMetadata = null;
                }

                return removed;
            }

            if (current is ProjectMetadata single)
            {
                if (!MatchesName(single, name, MSBuildNameIgnoreCaseComparer.Default.GetHashCode(name)))
                {
                    return false;
                }

                _directMetadata = null;
                return true;
            }

            SparseDirectMetadata sparse = current as SparseDirectMetadata;
            ProjectMetadata[] slots = sparse?.Slots ?? (ProjectMetadata[])current;
            int index = slots == null ? -1 : IndexOfDirectMetadatum(slots, name);
            if (index < 0)
            {
                return false;
            }

            if (GetCount(current) == 1)
            {
                _directMetadata = null;
                return true;
            }

            // Leave a hole so the most recently freed slot is used by the next insertion.
            ProjectMetadata[] updatedSlots = (ProjectMetadata[])slots.Clone();
            updatedSlots[index] = null;
            int[] freeSlots = sparse?.FreeSlots;
            int freeSlotCount = freeSlots?.Length ?? 0;
            int[] updatedFreeSlots = new int[freeSlotCount + 1];
            freeSlots?.CopyTo(updatedFreeSlots, 0);
            updatedFreeSlots[freeSlotCount] = index;
            _directMetadata = new SparseDirectMetadata(updatedSlots, updatedFreeSlots);
            return true;
        }
        /// <summary>
        /// Helper to get the value of a built-in metadatum with
        /// the specified name, if any.
        /// </summary>
        private string GetBuiltInMetadataEscaped(string name)
            => ItemSpecModifiers.TryGetModifierKind(name, out ItemSpecModifierKind modifierKind)
                ? BuiltInMetadata.GetMetadataValueEscaped(_project.DirectoryPath, _evaluatedIncludeBeforeWildcardExpansionEscaped, _evaluatedIncludeEscaped, Xml.ContainingProject.FullPath, modifierKind, ref _cachedModifiers)
                : null;

        /// <summary>
        /// Retrieves the named metadata from the item definition, if any.
        /// If it is not present, returns null
        /// </summary>
        /// <param name="name">The metadata name.</param>
        /// <returns>The value if it exists, null otherwise.</returns>
        private ProjectMetadata GetItemDefinitionMetadata(string name)
        {
            ProjectMetadata metadataFromDefinition = null;

            // Check any inherited item definition metadata first. It's more like
            // direct metadata, but we didn't want to copy the tables.
            if (_inheritedItemDefinitions != null)
            {
                foreach (ProjectItemDefinition inheritedItemDefinition in _inheritedItemDefinitions)
                {
                    metadataFromDefinition = inheritedItemDefinition.GetMetadata(name);

                    if (metadataFromDefinition != null)
                    {
                        return metadataFromDefinition;
                    }
                }
            }

            // Now try regular item definition metadata for this item type.
            ProjectItemDefinition itemDefinition;
            if (_project.ItemDefinitions.TryGetValue(ItemType, out itemDefinition))
            {
                metadataFromDefinition = itemDefinition.GetMetadata(name);
            }

            return metadataFromDefinition;
        }

        private sealed class DirectMetadataView(ProjectItem item) : ICollection<ProjectMetadata>, IReadOnlyCollection<ProjectMetadata>
        {
            public int Count => GetCount(item._directMetadata);
            public bool IsReadOnly => true;

            public IEnumerator<ProjectMetadata> GetEnumerator() => EnumerateDirectMetadata(item._directMetadata).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public bool Contains(ProjectMetadata metadata)
            {
                foreach (ProjectMetadata entry in this)
                {
                    if (entry.Equals(metadata))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void CopyTo(ProjectMetadata[] array, int arrayIndex)
            {
                ArgumentNullException.ThrowIfNull(array);
                object directMetadata = item._directMetadata;
                IEnumerable<ProjectMetadata> entries = EnumerateDirectMetadata(directMetadata);
                int count = entries is ProjectMetadata[] snapshot ? snapshot.Length : GetCount(directMetadata);
                if (arrayIndex < 0 || arrayIndex > array.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                }

                if (count > array.Length - arrayIndex)
                {
                        throw new ArgumentException("The destination array is too small.", nameof(array));
                }

                foreach (ProjectMetadata entry in entries)
                {
                    array[arrayIndex++] = entry;
                }
            }

            public void Add(ProjectMetadata metadata) => ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            public void Clear() => ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
            public bool Remove(ProjectMetadata metadata) => throw new InvalidOperationException(ResourceUtilities.GetResourceString("OM_NotSupportedReadOnlyCollection"));
        }

        /// <summary>
        /// Direct metadata of an item from which a metadatum was removed while others remain.
        /// Immutable.
        /// </summary>
        /// <remarks>
        /// This keeps the slot layout of the hash set that was used previously, so that the
        /// enumeration order does not change: a removed metadatum leaves a hole, and a new metadatum
        /// fills the most recently freed hole before it is appended. This is rare, so it is not
        /// necessary to make it compact.
        /// </remarks>
        private sealed class SparseDirectMetadata
        {
            /// <summary>
            /// Creates the snapshot. Takes ownership of the arrays.
            /// </summary>
            internal SparseDirectMetadata(ProjectMetadata[] slots, int[] freeSlots)
            {
                Slots = slots;
                FreeSlots = freeSlots;
            }

            /// <summary>
            /// The metadata in enumeration order. Holes are null.
            /// </summary>
            internal ProjectMetadata[] Slots { get; }

            /// <summary>
            /// The indexes of the holes in <see cref="Slots"/>. Never empty.
            /// The last index is the most recently freed hole, which is filled first.
            /// </summary>
            internal int[] FreeSlots { get; }

            /// <summary>
            /// The number of metadata. Always more than zero.
            /// </summary>
            internal int Count => Slots.Length - FreeSlots.Length;
        }

        /// <summary>
        /// A class factory for ProjectItems.
        /// </summary>
        internal class ProjectItemFactory : IItemFactory<ProjectItem, ProjectItem>
        {
            /// <summary>
            /// The Project with which each item should be associated.
            /// </summary>
            private readonly Project _project;

            /// <summary>
            /// The project item's XML
            /// </summary>
            private ProjectItemElement _xml;

            /// <summary>
            /// Creates an item factory which does not specify an item xml.  The item xml must
            /// be specified later.
            /// </summary>
            /// <param name="project">The project for items generated.</param>
            internal ProjectItemFactory(Project project)
            {
                _project = project;
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="project">The project for items generated.</param>
            /// <param name="xml">The xml for items generated.</param>
            internal ProjectItemFactory(Project project, ProjectItemElement xml)
            {
                _project = project;
                _xml = xml;
            }

            /// <summary>
            /// Item type that items created by this factory will have.
            /// </summary>
            public string ItemType
            {
                get => _xml.ItemType;
                set => InternalError.Throw("Cannot change the item type on ProjectItem.ProjectItemFactory");
            }

            /// <summary>
            /// Set the item xml from which items will be created.
            /// Used by the evaluator only.
            /// </summary>
            public ProjectItemElement ItemElement
            {
                set { _xml = value; }
            }

            /// <summary>
            /// Creates an item with the specified type and evaluated include.
            /// Used for making items from "just strings" and from expressions like "@(Compile, ';')"
            /// </summary>
            /// <param name="include">The include.</param>
            /// <param name="definingProject">The path to the project that defined the item.</param>
            /// <returns>A new project item.</returns>
            /// <comments>
            /// NOTE: defining project is ignored because we already know the ItemElement associated with
            /// this item, and use that for where it is defined.
            /// </comments>
            public ProjectItem CreateItem(string include, string definingProject)
            {
                return CreateItem(include, include, definingProject);
            }

            /// <summary>
            /// Creates an item based on the provided item, but with
            /// the project and xml of this factory. Metadata is cloned,
            /// but continues to point to the original ProjectMetadataElement objects.
            /// This is to support the scenario Include="@(i)" where we are copying
            /// metadata, and are happy to see changes in the original metadata, but
            /// setting metadata should create new XML.
            /// </summary>
            /// <comments>
            /// NOTE: defining project is ignored because we already know the ItemElement associated with
            /// this item, and use that for where it is defined.
            /// </comments>
            public ProjectItem CreateItem(ProjectItem source, string definingProject)
            {
                return CreateItem(source._evaluatedIncludeEscaped, source._evaluatedIncludeBeforeWildcardExpansionEscaped, source);
            }

            /// <summary>
            /// Creates an item based on the provided item, but with
            /// the project and xml of this factory and the specified include. Metadata is cloned,
            /// but continues to point to the original ProjectMetadataElement objects.
            /// This is to support this scenario: Include="@(i->'xxx')"
            /// </summary>
            /// <remarks>
            /// If the item type of the source is the same as the item type of the destination,
            /// then it's not necessary to copy metadata originating in an item definition.
            /// If it's not, we have to clone that too.
            /// </remarks>
            /// <comments>
            /// NOTE: defining project is ignored because we already know the ItemElement associated with
            /// this item, and use that for where it is defined.
            /// </comments>
            public ProjectItem CreateItem(string evaluatedIncludeEscaped, ProjectItem source, string definingProject)
            {
                return CreateItem(evaluatedIncludeEscaped, evaluatedIncludeEscaped, source);
            }

            /// <summary>
            /// Creates an item with the specified include and include before wildcard expansion.
            /// This is to support creating items from an include that may have a wildcard expression in it.
            /// </summary>
            /// <comments>
            /// NOTE: defining project is ignored because we already know the ItemElement associated with
            /// this item, and use that for where it is defined.
            /// </comments>
            public ProjectItem CreateItem(string evaluatedIncludeEscaped, string evaluatedIncludeBeforeWildcardExpansion, string definingProject)
            {
                Assumed.NotNull(_xml);

                return new ProjectItem(_project, _xml, evaluatedIncludeEscaped, evaluatedIncludeBeforeWildcardExpansion, null /* no metadata */, null /* no inherited definition metadata */);
            }

            /// <summary>
            /// Applies the supplied metadata to the destination item.
            /// </summary>
            public void SetMetadata(IEnumerable<KeyValuePair<ProjectMetadataElement, string>> metadata, IEnumerable<ProjectItem> destinationItems)
            {
                foreach (IItem<ProjectMetadata> item in destinationItems)
                {
                    foreach (KeyValuePair<ProjectMetadataElement, string> metadatum in metadata)
                    {
                        item.SetMetadata(metadatum.Key, metadatum.Value);
                    }
                }
            }

            /// <summary>
            /// Creates an item based on the provided item, with the specified include and item type.
            /// </summary>
            private ProjectItem CreateItem(string evaluatedIncludeEscaped, string evaluatedIncludeBeforeWildcardExpansionEscaped, ProjectItem source)
            {
                Assumed.NotNull(_xml);

                // The new item inherits any metadata originating in item definitions, which
                // takes precedence over its own item definition metadata.
                //
                // Order of precedence:
                // (1) any directly defined metadata on the source item
                // (2) any inherited item definition metadata the source item had accumulated, in order of accumulation
                // (3) any item definition metadata associated with the source item's item type
                // (4) any item definition metadata associated with the destination item's item type; none yet.

                // Clone for (1)
                object sourceDirectMetadata = source._directMetadata;
                object directMetadataClone = null;
                if (sourceDirectMetadata is ProjectMetadata single)
                {
                    directMetadataClone = single.DeepClone();
                }
                else if (sourceDirectMetadata != null)
                {
                    var entries = EnumerateDirectMetadata(sourceDirectMetadata);
                    if (entries is ProjectMetadata[] snapshot && snapshot.Length > CompactMetadataMaxCount)
                    {
                        var dictionary = new PropertyDictionary<ProjectMetadata>(snapshot.Length);
                        foreach (ProjectMetadata entry in snapshot)
                        {
                            dictionary.Set(entry.DeepClone());
                        }

                        directMetadataClone = dictionary;
                    }
                    else
                    {
                        foreach (ProjectMetadata entry in entries)
                        {
                            directMetadataClone = WithDirectMetadatum(directMetadataClone, entry.DeepClone());
                        }
                    }
                }

                // Combine (2) and (3) into a list, (2) on top.
                int inheritedItemDefinitionsCount = (source._inheritedItemDefinitions == null) ? 0 : source._inheritedItemDefinitions.Count;

                List<ProjectItemDefinition> inheritedItemDefinitionsClone = null;

                if (source._inheritedItemDefinitions != null)
                {
                    inheritedItemDefinitionsClone ??= new List<ProjectItemDefinition>(inheritedItemDefinitionsCount + 1);
                    inheritedItemDefinitionsClone.AddRange(source._inheritedItemDefinitions);
                }

                ProjectItemDefinition sourceItemDefinition;
                if (_project.ItemDefinitions.TryGetValue(source.ItemType, out sourceItemDefinition))
                {
                    inheritedItemDefinitionsClone ??= new List<ProjectItemDefinition>(inheritedItemDefinitionsCount + 1);
                    inheritedItemDefinitionsClone.Add(sourceItemDefinition);
                }

                return new ProjectItem(_project, _xml, evaluatedIncludeEscaped, evaluatedIncludeBeforeWildcardExpansionEscaped, directMetadataClone, inheritedItemDefinitionsClone);
            }
        }

        /// <summary>
        /// Implementation of IMetadataTable that can be passed to expander
        /// to expose only built-in metadata on this item.
        /// </summary>
        private class BuiltInMetadataTable : IMetadataTable
        {
            /// <summary>
            /// Backing item
            /// </summary>
            private ProjectItem _item;

            /// <summary>
            /// Constructor.
            /// </summary>
            internal BuiltInMetadataTable(ProjectItem item)
            {
                _item = item;
            }

            /// <summary>
            /// Retrieves any value we have in our metadata table for the metadata name specified.
            /// If no value is available, returns empty string.
            /// </summary>
            public string GetEscapedValue(string name)
            {
                string value = _item.GetBuiltInMetadataEscaped(name);
                return value;
            }

            /// <summary>
            /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
            /// If item type is null, it is ignored.
            /// If no value is available, returns empty string.
            /// </summary>
            public string GetEscapedValue(string itemType, string name)
            {
                string value = GetEscapedValueIfPresent(itemType, name);

                return value ?? String.Empty;
            }

            /// <summary>
            /// Returns the value if it exists, null otherwise.
            /// If item type is null, it is ignored.
            /// </summary>
            public string GetEscapedValueIfPresent(string itemType, string name)
            {
                string value = null;

                if ((itemType == null) || String.Equals(_item.ItemType, itemType, StringComparison.OrdinalIgnoreCase))
                {
                    value = GetEscapedValue(name);
                }

                return value;
            }
        }
    }
}
