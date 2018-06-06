// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Build.Shared;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Execution;
using Microsoft.Build.Collections;

using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

namespace Microsoft.Build.BackEnd
{
    using ItemsMetadataUpdateDictionary = Dictionary<ProjectItemInstance, Lookup.MetadataModifications>;
    using ItemTypeToItemsMetadataUpdateDictionary = Dictionary<string, Dictionary<ProjectItemInstance, Lookup.MetadataModifications>>;

    /// <summary>
    /// Contains a list of item and property collections, optimized to allow
    ///     - very fast "cloning"
    ///     - quick lookups
    ///     - scoping down of item subsets in nested scopes (useful for batches)
    ///     - isolation of adds, removes, modifies, and property sets inside nested scopes
    ///     
    /// When retrieving the item group for an item type, each table is consulted in turn,
    /// starting with the primary table (the "top" or "innermost" table), until a table is found that has an entry for that type.
    /// When an entry is found, it is returned without looking deeper.
    /// This makes it possible to let callers see only a subset of items without affecting or cloning the original item groups,
    /// by populating a scope with item groups that are empty or contain subsets of items in lower scopes.
    /// 
    /// Instances of this class can be cloned with Clone() to share between batches.
    /// 
    /// When EnterScope() is called, a fresh primary table is inserted, and all adds and removes will be invisible to
    /// any clones made before the scope was entered and anyone who has access to item groups in lower tables.
    /// 
    /// When LeaveScope() is called, the primary tables are merged into the secondary tables, and the primary tables are discarded.
    /// This makes the adds and removes in the primary tables visible to clones made during the previous scope.
    /// 
    /// Scopes can be populated (before Adds, Removes, and Lookups) using PopulateWithItem(). This reduces the set of items of a particular
    /// type that are visible in a scope, because lookups of items of this type will stop at this level and see the subset, rather than the
    /// larger set in a scope below.
    /// 
    /// Items can be added or removed by calling AddNewItem() and RemoveItem(). Only the primary level is modified.
    /// When items are added or removed they enter into a primary table exclusively for adds or removes, instead of the main primary table.
    /// This allows the adds and removes to be applied to the scope below on LeaveScope(). Even when LeaveScope() is called, the adds and removes
    /// stay in their separate add and remove tables: if they were applied to a main table, they could truncate the downward traversal performed by lookups
    /// and hide items in a lower main table. Only on the final call of LeaveScope() can all adds and removes be applied to the outermost table, i.e., the project.
    /// 
    /// Much the same applies to properties.
    /// 
    /// For sensible semantics, only the current primary scope can be modified at any point.
    /// </summary>
    internal class Lookup : IPropertyProvider<ProjectPropertyInstance>, IItemProvider<ProjectItemInstance>
    {
        #region Fields

        /// <summary>
        /// Ordered list of scope used for lookup.
        /// Each scope contains multiple tables:
        ///  - the main item table (populated with subsets of lists, in order to create batches)
        ///  - the add table (items that have been added during execution)
        ///  - the remove table (items that have been removed during execution)
        ///  - the modify table (item metadata modifications)
        ///  - the main property table (populated with properties that are visible in this scope)
        ///  - the property set table (changes made to properties)
        /// All have to be consulted to find the items and properties available in the current scope.
        /// We have to keep them separate, because the adds and removes etc need to be applied to the table
        /// below when we leave a scope.
        /// </summary>
        private LinkedList<Lookup.Scope> _lookupScopes = new LinkedList<Lookup.Scope>();

        /// <summary>
        /// When we are asked for all the items of a certain type using the GetItems() method, we may have to handle items
        /// that have been modified earlier with ModifyItems(). These pending modifications can't be applied immediately to
        /// the item because that would affect other batches. Instead we clone the item, apply the modification, and hand that over.
        /// The problem is that later we might get asked to remove or modify that item. We want to make sure that we record that as
        /// a remove or modify of the real item, not the clone we handed over. So we keep a lookup of (clone, original) to consult.
        /// </summary>
        private Dictionary<ProjectItemInstance, ProjectItemInstance> _cloneTable;

        #endregion

        #region Constructors

        /// <summary>
        /// Construct a lookup over specified items and properties.
        /// </summary>
        internal Lookup(ItemDictionary<ProjectItemInstance> projectItems, PropertyDictionary<ProjectPropertyInstance> properties)
        {
            ErrorUtilities.VerifyThrowInternalNull(projectItems, "projectItems");
            ErrorUtilities.VerifyThrowInternalNull(properties, "properties");

            Lookup.Scope scope = new Lookup.Scope(this, "Lookup()", projectItems, properties);
            _lookupScopes.AddFirst(scope);
        }

        /// <summary>
        /// Copy constructor (called via Clone() - clearer semantics)
        /// </summary>
        private Lookup(Lookup that)
        {
            // Add the same tables from the original
            foreach (Lookup.Scope scope in that._lookupScopes)
            {
                _lookupScopes.AddLast(scope);
            }

            // Clones need to share an (item)clone table; the batching engine asks for items from the lookup,
            // then populates buckets with them, which have clone lookups.
            _cloneTable = that._cloneTable;
        }

        #endregion

        #region Properties

        // Convenience private properties
        // "Primary" is the "top" or "innermost" scope
        // "Secondary" is the next from the top.
        private ItemDictionary<ProjectItemInstance> PrimaryTable
        {
            get { return _lookupScopes.First.Value.Items; }
            set { _lookupScopes.First.Value.Items = value; }
        }

        private ItemDictionary<ProjectItemInstance> PrimaryAddTable
        {
            get { return _lookupScopes.First.Value.Adds; }
            set { _lookupScopes.First.Value.Adds = value; }
        }

        private ItemDictionary<ProjectItemInstance> PrimaryRemoveTable
        {
            get { return _lookupScopes.First.Value.Removes; }
            set { _lookupScopes.First.Value.Removes = value; }
        }

        private ItemTypeToItemsMetadataUpdateDictionary PrimaryModifyTable
        {
            get { return _lookupScopes.First.Value.Modifies; }
            set { _lookupScopes.First.Value.Modifies = value; }
        }

        private PropertyDictionary<ProjectPropertyInstance> PrimaryPropertySets
        {
            get { return _lookupScopes.First.Value.PropertySets; }
            set { _lookupScopes.First.Value.PropertySets = value; }
        }

        private ItemDictionary<ProjectItemInstance> SecondaryTable
        {
            get { return _lookupScopes.First.Next.Value.Items; }
            set { _lookupScopes.First.Next.Value.Items = value; }
        }

        private ItemDictionary<ProjectItemInstance> SecondaryAddTable
        {
            get { return _lookupScopes.First.Next.Value.Adds; }
            set { _lookupScopes.First.Next.Value.Adds = value; }
        }

        private ItemDictionary<ProjectItemInstance> SecondaryRemoveTable
        {
            get { return _lookupScopes.First.Next.Value.Removes; }
            set { _lookupScopes.First.Next.Value.Removes = value; }
        }

        private ItemTypeToItemsMetadataUpdateDictionary SecondaryModifyTable
        {
            get { return _lookupScopes.First.Next.Value.Modifies; }
            set { _lookupScopes.First.Next.Value.Modifies = value; }
        }

        private PropertyDictionary<ProjectPropertyInstance> SecondaryProperties
        {
            get { return _lookupScopes.First.Next.Value.Properties; }
            set { _lookupScopes.First.Next.Value.Properties = value; }
        }

        private PropertyDictionary<ProjectPropertyInstance> SecondaryPropertySets
        {
            get { return _lookupScopes.First.Next.Value.PropertySets; }
            set { _lookupScopes.First.Next.Value.PropertySets = value; }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Compares the primary property sets of the passed in lookups to determine if there are properties which are shared between
        /// the lookups. We find these shared property names because this indicates that the current Lookup is overriding the property value of another Lookup
        /// When an override is detected a messages is generated to inform the users that the property is being changed between batches
        /// </summary>
        /// <returns>array or error messages to log </returns>
        internal List<string> GetPropertyOverrideMessages(Dictionary<string, string> lookupHash)
        {
            List<string> errorMessages = null;
            // For each batch lookup list we need to compare the property items to see if they have already been set
            if (PrimaryPropertySets != null)
            {
                foreach (ProjectPropertyInstance property in PrimaryPropertySets)
                {
                    if (String.Equals(property.Name, ReservedPropertyNames.lastTaskResult, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string propertyName = property.Name;
                    // If the hash contains the property name, output a messages that displays the previous property value and the new property value
                    if (lookupHash.ContainsKey(propertyName))
                    {
                        if (errorMessages == null)
                        {
                            errorMessages = new List<string>();
                        }
                        errorMessages.Add(ResourceUtilities.FormatResourceString("PropertyOutputOverridden", propertyName, EscapingUtilities.UnescapeAll(lookupHash[propertyName]), property.EvaluatedValue));
                    }

                    // Set the value of the hash to the new property value
                    // PERF: we store the EvaluatedValueEscaped here to avoid unnecessary unescaping (the value is stored 
                    // escaped in the property)
                    lookupHash[propertyName] = ((IProperty)property).EvaluatedValueEscaped;
                }
            }

            return errorMessages;
        }

        /// <summary>
        /// Clones this object, to create another one with its own list, but the same contents.
        /// Then the clone can enter scope and have its own fresh primary list without affecting the other object.
        /// </summary>
        internal Lookup Clone()
        {
            return new Lookup(this);
        }

        /// <summary>
        /// Enters the scope using the specified description.
        /// Callers keep the scope in order to pass it to <see cref="LeaveScope">LeaveScope</see>.
        /// </summary>
        internal Lookup.Scope EnterScope(string description)
        {
            // We don't create the tables unless we need them
            Scope scope = new Scope(this, description, null, null);
            _lookupScopes.AddFirst(scope);
            return scope;
        }

        /// <summary>
        /// Leaves the specified scope, which must be the active one.
        /// Moves all tables up one: the tertiary table becomes the secondary table, and so on. The primary
        /// and secondary table are merged. This has the effect of "applying" the adds applied to the primary
        /// table into the secondary table.
        /// </summary>
        private void LeaveScope(Lookup.Scope scopeToLeave)
        {
            ErrorUtilities.VerifyThrow(_lookupScopes.Count >= 2, "Too many calls to Leave().");
            ErrorUtilities.VerifyThrow(Object.ReferenceEquals(scopeToLeave, _lookupScopes.First.Value), "Attempting to leave with scope '{0}' but scope '{1}' is on top of the stack.", scopeToLeave.Description, _lookupScopes.First.Value.Description);

            // Our lookup works by stopping the first time it finds an item group of the appropriate type. 
            // So we can't apply an add directly into the table below because that could create a new group 
            // of that type, which would cause the next lookup to stop there and miss any existing items in a table below.
            // Instead we keep adds stored separately until we're leaving the very last scope. Until then
            // we only move adds down into the next add table below, and when we lookup we consider both tables.
            // Same applies to removes.
            if (_lookupScopes.Count == 2)
            {
                MergeScopeIntoLastScope();
            }
            else
            {
                MergeScopeIntoNotLastScope();
            }

            // Let go of our pointer into the clone table; we assume we won't need it after leaving scope and want to save memory.
            // This is an assumption on IntrinsicTask, that it won't ask to remove or modify a clone in a higher scope than it was handed out in.
            // We mustn't call cloneTable.Clear() because other clones of this lookup may still be using it. When the last lookup clone leaves scope, 
            // the table will be collected.
            _cloneTable = null;

            // Move all tables up one, discarding the primary tables
            _lookupScopes.RemoveFirst();
        }

        /// <summary>
        /// Leaving an arbitrary scope, just merging all the adds, removes, modifies, and sets into the scope below.
        /// </summary>
        private void MergeScopeIntoNotLastScope()
        {
            // Move all the adds down
            if (PrimaryAddTable != null)
            {
                if (SecondaryAddTable == null)
                {
                    SecondaryAddTable = PrimaryAddTable;
                }
                else
                {
                    SecondaryAddTable.ImportItems(PrimaryAddTable);
                }
            }

            // Move all the removes down
            if (PrimaryRemoveTable != null)
            {
                if (SecondaryRemoveTable == null)
                {
                    SecondaryRemoveTable = PrimaryRemoveTable;
                }
                else
                {
                    // When merging remove lists from two or more batches both tables (primary and secondary) may contain identical items. The reason is when removing the items we get the original items rather than a clone
                    // so the same item may have already been added to the secondary table. If we then try and add the same item from the primary table we will get a duplicate key exception from the
                    // dictionary. Therefore we must not merge in an item if it already is in the secondary remove table.
                    foreach (ProjectItemInstance item in PrimaryRemoveTable)
                    {
                        if (!SecondaryRemoveTable.Contains(item))
                        {
                            SecondaryRemoveTable.Add(item);
                        }
                    }
                }
            }

            // Move all the modifies down
            if (PrimaryModifyTable != null)
            {
                if (SecondaryModifyTable == null)
                {
                    SecondaryModifyTable = PrimaryModifyTable;
                }
                else
                {
                    foreach (KeyValuePair<string, Dictionary<ProjectItemInstance, MetadataModifications>> entry in PrimaryModifyTable)
                    {
                        Dictionary<ProjectItemInstance, MetadataModifications> modifiesOfType;
                        if (SecondaryModifyTable.TryGetValue(entry.Key, out modifiesOfType))
                        {
                            // There are already modifies of this type: add to the existing table
                            foreach (KeyValuePair<ProjectItemInstance, MetadataModifications> modify in entry.Value)
                            {
                                MergeModificationsIntoModificationTable(modifiesOfType, modify, ModifyMergeType.SecondWins);
                            }
                        }
                        else
                        {
                            SecondaryModifyTable.Add(entry.Key, entry.Value);
                        }
                    }
                }
            }

            // Move all the sets down
            if (PrimaryPropertySets != null)
            {
                if (SecondaryPropertySets == null)
                {
                    SecondaryPropertySets = PrimaryPropertySets;
                }
                else
                {
                    SecondaryPropertySets.ImportProperties(PrimaryPropertySets);
                }
            }
        }

        /// <summary>
        /// Merge the current scope down into the base scope. This means applying the adds, removes, modifies, and sets
        /// directly into the item and property tables in this scope.
        /// </summary>
        private void MergeScopeIntoLastScope()
        {
            // End of the line for this object: we are done with add tables, and we want to expose our
            // adds to the world
            if (PrimaryAddTable != null)
            {
                SecondaryTable = SecondaryTable ?? new ItemDictionary<ProjectItemInstance>();
                SecondaryTable.ImportItems(PrimaryAddTable);
            }

            if (PrimaryRemoveTable != null)
            {
                SecondaryTable = SecondaryTable ?? new ItemDictionary<ProjectItemInstance>();
                SecondaryTable.RemoveItems(PrimaryRemoveTable);
            }

            if (PrimaryModifyTable != null)
            {
                foreach (KeyValuePair<string, Dictionary<ProjectItemInstance, MetadataModifications>> entry in PrimaryModifyTable)
                {
                    SecondaryTable = SecondaryTable ?? new ItemDictionary<ProjectItemInstance>();
                    ApplyModificationsToTable(SecondaryTable, entry.Key, entry.Value);
                }
            }

            if (PrimaryPropertySets != null)
            {
                SecondaryProperties = SecondaryProperties ?? new PropertyDictionary<ProjectPropertyInstance>(PrimaryPropertySets.Count);
                SecondaryProperties.ImportProperties(PrimaryPropertySets);
            }
        }

        /// <summary>
        /// Gets the effective property for the current scope.
        /// taking the name from the provided string within the specified start and end indexes.
        /// If no match is found, returns null.
        /// Caller must not modify the property returned.
        /// </summary>
        public ProjectPropertyInstance GetProperty(string name, int startIndex, int endIndex)
        {
            // Walk down the tables and stop when the first 
            // property with this name is found
            foreach (Scope scope in _lookupScopes)
            {
                if (scope.PropertySets != null)
                {
                    ProjectPropertyInstance property = scope.PropertySets.GetProperty(name, startIndex, endIndex);
                    if (property != null)
                    {
                        return property;
                    }
                }

                if (scope.Properties != null)
                {
                    ProjectPropertyInstance property = scope.Properties.GetProperty(name, startIndex, endIndex);
                    if (property != null)
                    {
                        return property;
                    }
                }

                if (scope.TruncateLookupsAtThisScope)
                {
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the effective property for the current scope.
        /// If no match is found, returns null.
        /// Caller must not modify the property returned.
        /// </summary>
        public ProjectPropertyInstance GetProperty(string name)
        {
            ErrorUtilities.VerifyThrowInternalLength(name, "name");

            return GetProperty(name, 0, name.Length - 1);
        }

        /// <summary>
        /// Gets the items of the specified type that are visible in the current scope.
        /// If no match is found, returns an empty list.
        /// Caller must not modify the group returned.
        /// </summary>
        public ICollection<ProjectItemInstance> GetItems(string itemType)
        {
            // The visible items consist of the adds (accumulated as we go down)
            // plus the first set of regular items we encounter
            // minus any removes

            List<ProjectItemInstance> allAdds = null;
            List<ProjectItemInstance> allRemoves = null;
            Dictionary<ProjectItemInstance, MetadataModifications> allModifies = null;
            ICollection<ProjectItemInstance> groupFound = null;

            foreach (Scope scope in _lookupScopes)
            {
                // Accumulate adds while we look downwards
                if (scope.Adds != null)
                {
                    ICollection<ProjectItemInstance> adds = scope.Adds[itemType];
                    if (adds.Count != 0)
                    {
                        allAdds = allAdds ?? new List<ProjectItemInstance>(adds.Count);
                        allAdds.AddRange(adds);
                    }
                }

                // Accumulate removes while we look downwards
                if (scope.Removes != null)
                {
                    ICollection<ProjectItemInstance> removes = scope.Removes[itemType];
                    if (removes.Count != 0)
                    {
                        allRemoves = allRemoves ?? new List<ProjectItemInstance>(removes.Count);
                        allRemoves.AddRange(removes);
                    }
                }

                // Accumulate modifications as we look downwards
                if (scope.Modifies != null)
                {
                    Dictionary<ProjectItemInstance, MetadataModifications> modifies;
                    if (scope.Modifies.TryGetValue(itemType, out modifies))
                    {
                        if (modifies.Count != 0)
                        {
                            allModifies = allModifies ?? new Dictionary<ProjectItemInstance, MetadataModifications>(modifies.Count);

                            // We already have some modifies for this type
                            foreach (KeyValuePair<ProjectItemInstance, MetadataModifications> modify in modifies)
                            {
                                // If earlier scopes modify the same metadata on the same item,
                                // they have priority
                                MergeModificationsIntoModificationTable(allModifies, modify, ModifyMergeType.FirstWins);
                            }
                        }
                    }
                }

                if (scope.Items != null)
                {
                    groupFound = scope.Items[itemType];
                    if (groupFound.Count != 0 || scope.Items.HasEmptyMarker(itemType))
                    {
                        // Found a group: we go no further
                        break;
                    }
                }

                if (scope.TruncateLookupsAtThisScope)
                {
                    break;
                }
            }

            if ((allAdds == null) &&
                (allRemoves == null) &&
                (allModifies == null))
            {
                // We can just hand out this group verbatim -
                // that avoids any importing
                groupFound = groupFound ?? Array.Empty<ProjectItemInstance>();

                return groupFound;
            }

            // Set the initial sizes to avoid resizing during import
            int itemsTypesCount = 1;    // We're only ever importing a single item type
            int itemsCount = groupFound?.Count ?? 0;    // Start with initial set
            itemsCount += allAdds?.Count ?? 0;          // Add all the additions
            itemsCount -= allRemoves?.Count ?? 0;       // Remove the removals
            if (itemsCount < 0)
                itemsCount = 0;

            // We have adds and/or removes and/or modifies to incorporate.
            // We can't modify the group, because that might
            // be visible to other batches; we have to create
            // a new one.
            ItemDictionary<ProjectItemInstance> result = new ItemDictionary<ProjectItemInstance>(itemsTypesCount, itemsCount);

            if (groupFound != null)
            {
                result.ImportItemsOfType(itemType, groupFound);
            }
            // Removes are processed after adds; this means when we remove there's no need to concern ourselves
            // with the case where the item being removed is in an add table somewhere. The converse case is not possible
            // using a project file: a project file cannot create an item that was already removed, it can only create
            // a unique new item.
            if (allAdds != null)
            {
                result.ImportItemsOfType(itemType, allAdds);
            }

            if (allRemoves != null)
            {
                result.RemoveItems(allRemoves);
            }

            // Modifies can be processed last; if a modified item was removed, the modify will be ignored
            if (allModifies != null)
            {
                ApplyModifies(result, allModifies);
            }

            return result[itemType];
        }

        /// <summary>
        /// Populates with an item group. This is done before the item lookup is used in this scope.
        /// Assumes all the items in the group have the same, provided, type.
        /// Assumes there is no item group of this type in the primary table already.
        /// Should be used only by batching buckets, and if no items are passed,
        /// explicitly stores a marker for this item type indicating this.
        /// </summary>
        internal void PopulateWithItems(string itemType, ICollection<ProjectItemInstance> group)
        {
            PrimaryTable = PrimaryTable ?? new ItemDictionary<ProjectItemInstance>();
            ICollection<ProjectItemInstance> existing = PrimaryTable[itemType];
            ErrorUtilities.VerifyThrow(existing.Count == 0, "Cannot add an itemgroup of this type.");

            if (group.Count > 0)
            {
                PrimaryTable.ImportItemsOfType(itemType, group);
            }
            else
            {
                PrimaryTable.AddEmptyMarker(itemType);
            }
        }

        /// <summary>
        /// Populates with an item. This is done before the item lookup is used in this scope.
        /// There may or may not already be a group for it.
        /// </summary>
        internal void PopulateWithItem(ProjectItemInstance item)
        {
            PrimaryTable = PrimaryTable ?? new ItemDictionary<ProjectItemInstance>();
            PrimaryTable.Add(item);
        }

        /// <summary>
        /// Apply a property to this scope.
        /// </summary>
        internal void SetProperty(ProjectPropertyInstance property)
        {
            // Setting in outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

            // Put in the set table
            PrimaryPropertySets = PrimaryPropertySets ?? new PropertyDictionary<ProjectPropertyInstance>();
            PrimaryPropertySets.Set(property);
        }

        /// <summary>
        /// Implements a true add, an item that has been created in a batch.
        /// </summary>
        internal void AddNewItemsOfItemType(string itemType, ICollection<ProjectItemInstance> group, bool doNotAddDuplicates = false)
        {
            // Adding to outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

#if DEBUG
            foreach (ProjectItemInstance item in group)
            {
                MustNotBeInAnyTables(item);
            }
#endif

            if (group.Count == 0)
            {
                return;
            }

            // Put them in the add table
            PrimaryAddTable = PrimaryAddTable ?? new ItemDictionary<ProjectItemInstance>();
            IEnumerable<ProjectItemInstance> itemsToAdd = group;
            if (doNotAddDuplicates)
            {
                // Remove duplicates from the inputs.
                itemsToAdd = itemsToAdd.Distinct(ProjectItemInstance.EqualityComparer);

                // Ensure we don't also add any that already exist.
                var existingItems = GetItems(itemType);
                if (existingItems.Count > 0)
                {
                    itemsToAdd = itemsToAdd.Where(item => !(existingItems.Contains(item, ProjectItemInstance.EqualityComparer)));
                }
            }

            PrimaryAddTable.ImportItemsOfType(itemType, itemsToAdd);
        }

        /// <summary>
        /// Implements a true add, an item that has been created in a batch.
        /// </summary>
        internal void AddNewItem(ProjectItemInstance item)
        {
            // Adding to outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

#if DEBUG
            // This item must not be in any table already; a project cannot create an item
            // that already exists
            MustNotBeInAnyTables(item);
#endif

            // Put in the add table
            PrimaryAddTable = PrimaryAddTable ?? new ItemDictionary<ProjectItemInstance>();
            PrimaryAddTable.Add(item);
        }

        /// <summary>
        /// Remove a bunch of items from this scope
        /// </summary>
        internal void RemoveItems(IEnumerable<ProjectItemInstance> items)
        {
            foreach (ProjectItemInstance item in items)
            {
                RemoveItem(item);
            }
        }

        /// <summary>
        /// Remove an item from this scope
        /// </summary>
        internal void RemoveItem(ProjectItemInstance item)
        {
            // Removing from outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

            item = RetrieveOriginalFromCloneTable(item);

            // Put in the remove table
            PrimaryRemoveTable = PrimaryRemoveTable ?? new ItemDictionary<ProjectItemInstance>();
            PrimaryRemoveTable.Add(item);

            // No need to remove this item from the primary add table if it's 
            // already there -- we always apply removes after adds, so that add 
            // will be reversed anyway.
        }

        /// <summary>
        /// Modifies items in this scope with the same set of metadata modifications.
        /// Assumes all the items in the group have the same, provided, type.
        /// </summary>
        internal void ModifyItems(string itemType, ICollection<ProjectItemInstance> group, MetadataModifications metadataChanges)
        {
            // Modifying in outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

#if DEBUG
            // This item should not already be in any remove table; there is no way a project can 
            // modify items that were already removed
            // Obviously, do this only in debug, as it's a slow check for bugs.
            LinkedListNode<Scope> node = _lookupScopes.First;
            while (node != null)
            {
                Scope scope = node.Value;
                foreach (ProjectItemInstance item in group)
                {
                    ProjectItemInstance actualItem = RetrieveOriginalFromCloneTable(item);
                    MustNotBeInTable(scope.Removes, actualItem);
                }
                node = node.Next;
            }
#endif

            if (!metadataChanges.HasChanges)
            {
                return;
            }

            // Put in the modify table

            // We don't need to check whether the item is in the add table vs. the main table; either
            // way the modification will be applied.
            PrimaryModifyTable = PrimaryModifyTable ?? new ItemTypeToItemsMetadataUpdateDictionary(MSBuildNameIgnoreCaseComparer.Default);
            Dictionary<ProjectItemInstance, MetadataModifications> modifiesOfType;
            if (!PrimaryModifyTable.TryGetValue(itemType, out modifiesOfType))
            {
                modifiesOfType = new Dictionary<ProjectItemInstance, MetadataModifications>();
                PrimaryModifyTable[itemType] = modifiesOfType;
            }

            foreach (ProjectItemInstance item in group)
            {
                // Each item needs its own collection for metadata changes, even if this particular change is the same
                // for more than one item, subsequent changes might not be.
                var metadataChangeCopy = metadataChanges.Clone();

                // If we're asked to modify a clone we handed out, record it as a modify of the original
                // instead
                ProjectItemInstance actualItem = RetrieveOriginalFromCloneTable(item);
                var modify = new KeyValuePair<ProjectItemInstance, MetadataModifications>(actualItem, metadataChangeCopy);
                MergeModificationsIntoModificationTable(modifiesOfType, modify, ModifyMergeType.SecondWins);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Apply modifies to a temporary result group.
        /// Items to be modified are virtual-cloned so the original isn't changed.
        /// </summary>
        private void ApplyModifies(ItemDictionary<ProjectItemInstance> result, Dictionary<ProjectItemInstance, MetadataModifications> allModifies)
        {
            // Clone, because we're modifying actual items, and this would otherwise be visible to other batches,
            // and would be "published" even if a target fails.
            // FUTURE - don't need to clone here for non intrinsic tasks, but at present, they don't do modifies

            // Store the clone, in case we're asked to modify or remove it later (we will record it against the real item)
            _cloneTable = _cloneTable ?? new Dictionary<ProjectItemInstance, ProjectItemInstance>();

            foreach (var modify in allModifies)
            {
                ProjectItemInstance originalItem = modify.Key;

                if (result.Contains(originalItem))
                {
                    var modificationsToApply = modify.Value;

                    // Modify the cloned item and replace the original with it.
                    ProjectItemInstance cloneItem = modify.Key.DeepClone();

                    ApplyMetadataModificationsToItem(modificationsToApply, cloneItem);

                    result.Replace(originalItem, cloneItem);

                    // This will be null if the item wasn't in the result group, ie, it had been removed after being modified
                    ErrorUtilities.VerifyThrow(!_cloneTable.ContainsKey(cloneItem), "Should be new, not already in table!");
                    _cloneTable[cloneItem] = originalItem;
                }
            }
        }

        /// <summary>
        /// Applies the specified modifications to the supplied item.
        /// </summary>
        private static void ApplyMetadataModificationsToItem(MetadataModifications modificationsToApply, ProjectItemInstance itemToModify)
        {
            // Remove any metadata from the item which is slated for removal.  The indexer in the modifications table will
            // return a modification with Remove == true either if there is an explicit entry for that name in the modifications
            // or if keepOnlySpecified == true and there is no entry for that name.
            if (modificationsToApply.KeepOnlySpecified)
            {
                List<string> metadataToRemove = new List<string>(itemToModify.Metadata.Where(m => modificationsToApply[m.Name].Remove).Select(m => m.Name));
                foreach (var metadataName in metadataToRemove)
                {
                    itemToModify.RemoveMetadata(metadataName);
                }
            }

            // Now make any additions or modifications
            foreach (var modificationPair in modificationsToApply.ExplicitModifications)
            {
                if (modificationPair.Value.Remove)
                {
                    itemToModify.RemoveMetadata(modificationPair.Key);
                }
                else if (modificationPair.Value.NewValue != null)
                {
                    itemToModify.SetMetadata(modificationPair.Key, modificationPair.Value.NewValue);
                }
            }
        }

        /// <summary>
        /// Look up the "real" item by using its clone, and return the real item.
        /// See <see cref="_cloneTable"/> for explanation of the clone table.
        /// </summary>
        private ProjectItemInstance RetrieveOriginalFromCloneTable(ProjectItemInstance item)
        {
            ProjectItemInstance original;
            if (_cloneTable != null)
            {
                if (_cloneTable.TryGetValue(item, out original))
                {
                    item = original;
                }
            }

            return item;
        }

        /// <summary>
        /// Applies a list of modifications to the appropriate <see cref="ItemDictionary{ProjectItemInstance}" /> in a main table.
        /// If any modifications conflict, these modifications win.
        /// </summary>
        private void ApplyModificationsToTable(ItemDictionary<ProjectItemInstance> table, string itemType, ItemsMetadataUpdateDictionary modify)
        {
            ICollection<ProjectItemInstance> existing = table[itemType];
            if (existing != null)
            {
                foreach (var kvPair in modify)
                {
                    if (table.Contains(kvPair.Key))
                    {
                        var itemToModify = kvPair.Key;
                        var modificationsToApply = kvPair.Value;

                        ApplyMetadataModificationsToItem(modificationsToApply, itemToModify);
                    }
                }
            }
        }

        /// <summary>
        /// Applies a modification to an item in a table of modifications.
        /// If the item already exists in the table, merges in the modifications; if there is a conflict
        /// the mergeType indicates which should win.
        /// </summary>
        private void MergeModificationsIntoModificationTable(Dictionary<ProjectItemInstance, MetadataModifications> modifiesOfType,
                                                             KeyValuePair<ProjectItemInstance, MetadataModifications> modify,
                                                             ModifyMergeType mergeType)
        {
            MetadataModifications existingMetadataChanges;
            if (modifiesOfType.TryGetValue(modify.Key, out existingMetadataChanges))
            {
                // There's already modifications for this item; merge with those
                if (mergeType == ModifyMergeType.SecondWins)
                {
                    // Merge the new modifications on top of the existing modifications.
                    existingMetadataChanges.ApplyModifications(modify.Value);
                }
                else
                {
                    // Only apply explicit modifications.
                    foreach (var metadataChange in modify.Value.ExplicitModifications)
                    {
                        // If the existing metadata change list has an entry for this metadata, ignore this change.
                        // We continue to allow changes made when KeepOnlySpecified is set because it is assumed that explicit metadata changes 
                        // always trump implicit ones.
                        if (!existingMetadataChanges.ContainsExplicitModification(metadataChange.Key))
                        {
                            existingMetadataChanges[metadataChange.Key] = metadataChange.Value;
                        }
                    }
                }
            }
            else
            {
                modifiesOfType.Add(modify.Key, modify.Value);
            }
        }

#if DEBUG
        /// <summary>
        /// Verify item is not in the table
        /// </summary>
        private void MustNotBeInTable(ItemDictionary<ProjectItemInstance> table, ProjectItemInstance item)
        {
            if (table != null && table.ItemTypes.Contains(item.ItemType))
            {
                ICollection<ProjectItemInstance> tableOfItemsOfSameType = table[item.ItemType];
                if (tableOfItemsOfSameType != null)
                {
                    ErrorUtilities.VerifyThrow(!tableOfItemsOfSameType.Contains(item), "Item should not be in table");
                }
            }
        }

        /// <summary>
        /// Verify item is not in the modify table
        /// </summary>
        private void MustNotBeInTable(ItemTypeToItemsMetadataUpdateDictionary table, ProjectItemInstance item)
        {
            if (table != null && table.ContainsKey(item.ItemType))
            {
                ItemsMetadataUpdateDictionary tableOfItemsOfSameType = table[item.ItemType];
                if (tableOfItemsOfSameType != null)
                {
                    ErrorUtilities.VerifyThrow(!tableOfItemsOfSameType.ContainsKey(item), "Item should not be in table");
                }
            }
        }

        /// <summary>
        /// Verify item is not in any table in any scope
        /// </summary>        
        private void MustNotBeInAnyTables(ProjectItemInstance item)
        {
            // This item should not already be in any table; there is no way a project can
            // create items that already existed
            // Obviously, do this only in debug, as it's a slow check for bugs.
            LinkedListNode<Scope> node = _lookupScopes.First;
            while (node != null)
            {
                Scope scope = node.Value;
                MustNotBeInTable(scope.Adds, item);
                MustNotBeInTable(scope.Removes, item);
                MustNotBeInTable(scope.Modifies, item);
                node = node.Next;
            }
        }

#endif

        /// <summary>
        /// Add/remove/modify/set directly on an outer scope would need to be handled separately - it would apply
        /// directly to the main tables. Our code isn't expected to do this.
        /// </summary>
        private void MustNotBeOuterScope()
        {
            ErrorUtilities.VerifyThrow(_lookupScopes.Count > 1, "Operation in outer scope not supported");
        }

        #endregion

        /// <summary>
        /// When merging metadata, we can deal with a conflict two different ways:
        /// FirstWins = any previous metadata with the name takes precedence
        /// SecondWins = the new metadata with the name takes precedence
        /// </summary>
        private enum ModifyMergeType
        {
            FirstWins = 1,
            SecondWins = 2
        }

        /// <summary>
        /// A class representing a set of additions, modifications or removal of metadata from items.
        /// </summary>
        internal class MetadataModifications
        {
            /// <summary>
            /// Flag indicating if the modifications should be interpreted such that the lack of an explicit entry for a metadata name
            /// means that that metadata should be removed.
            /// </summary>
            private bool _keepOnlySpecified;

            /// <summary>
            /// A set of explicitly-specified modifications.
            /// </summary>
            private HybridDictionary<string, MetadataModification> _modifications;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="keepOnlySpecified">When true, metadata which is not explicitly-specified here but which is present on the target
            /// item should be removed.  When false, only explicitly-specified modifications apply.</param>
            public MetadataModifications(bool keepOnlySpecified)
            {
                _keepOnlySpecified = keepOnlySpecified;
                _modifications = new HybridDictionary<string, MetadataModification>(MSBuildNameIgnoreCaseComparer.Default);
            }

            /// <summary>
            /// Cloning constructor.
            /// </summary>
            /// <param name="other">The metadata modifications to clone.</param>
            private MetadataModifications(MetadataModifications other)
            {
                _keepOnlySpecified = other._keepOnlySpecified;
                _modifications = new HybridDictionary<string, MetadataModification>(other._modifications, MSBuildNameIgnoreCaseComparer.Default);
            }

            /// <summary>
            /// Clones this modification set.
            /// </summary>
            /// <returns>A copy of the modifications.</returns>
            public MetadataModifications Clone()
            {
                return new MetadataModifications(this);
            }

            /// <summary>
            /// A flag indicating whether or not there are any changes which might apply.
            /// </summary>
            public bool HasChanges
            {
                get { return _modifications.Count > 0 || _keepOnlySpecified; }
            }

            /// <summary>
            /// A flag indicating whether only those metadata explicitly-specified should be retained on a target item.
            /// </summary>
            public bool KeepOnlySpecified
            {
                get { return _keepOnlySpecified; }
            }

            /// <summary>
            /// Applies the modifications from the specified modifications to this one, performing a proper merge.
            /// </summary>
            /// <param name="other">The set of metadata modifications to merge into this one.</param>
            public void ApplyModifications(MetadataModifications other)
            {
                // Apply implicit modifications
                if (other._keepOnlySpecified)
                {
                    // Any metadata not specified in other must be removed from this one.
                    var metadataToRemove = new List<string>(_modifications.Keys.Where(m => other[m].Remove));
                    foreach (var metadata in metadataToRemove)
                    {
                        _modifications.Remove(metadata);
                    }
                }

                _keepOnlySpecified |= other._keepOnlySpecified;

                // Now apply the explicit modifications from the other table
                foreach (var modificationPair in other.ExplicitModifications)
                {
                    MetadataModification existingModification;
                    if (modificationPair.Value.KeepValue && _modifications.TryGetValue(modificationPair.Key, out existingModification))
                    {
                        // The incoming modification requests we maintain the "current value" of the metadata.  If we have
                        // an existing change, maintain that as-is.  Otherwise, fall through and apply our change directly.
                        if (existingModification.Remove || existingModification.NewValue != null)
                        {
                            continue;
                        }
                    }

                    // Just copy over the changes from the other table to this one.
                    _modifications[modificationPair.Key] = modificationPair.Value;
                }
            }

            /// <summary>
            /// Returns true if this block contains an explicitly-specified modification for the provided metadata name.
            /// </summary>
            /// <param name="metadataName">The name of the metadata.</param>
            /// <returns>True if there is an explicit modification for this metadata, false otherwise.</returns>
            /// <remarks>The return value of this method is unaffected by the <see cref="KeepOnlySpecified"/> property.</remarks>
            public bool ContainsExplicitModification(string metadataName)
            {
                return _modifications.ContainsKey(metadataName);
            }

            /// <summary>
            /// Adds metadata to the modification table.
            /// </summary>
            /// <param name="metadataName">The name of the metadata to add (or change) in the target item.</param>
            /// <param name="metadataValue">The metadata value.</param>
            public void Add(string metadataName, string metadataValue)
            {
                _modifications.Add(metadataName, MetadataModification.CreateFromNewValue(metadataValue));
            }

            /// <summary>
            /// Provides an enumeration of the explicit metadata modifications.
            /// </summary>
            public IEnumerable<KeyValuePair<string, MetadataModification>> ExplicitModifications
            {
                get { return _modifications; }
            }

            /// <summary>
            /// Sets or retrieves a modification from the modifications table.
            /// </summary>
            /// <param name="metadataName">The metadata name.</param>
            /// <returns>If <see cref="KeepOnlySpecified"/> is true, this will return a modification with <see cref="MetadataModification.Remove"/>
            /// set to true if the metadata has no other explicitly-specified modification.  Otherwise it will return only the explicitly-specified
            /// modification if one exists.</returns>
            /// <exception cref="System.Collections.Generic.KeyNotFoundException">When <see cref="KeepOnlySpecified"/> if false, this is thrown if the metadata
            /// specified does not exist when attempting to retrieve a metadata modification.</exception>
            public MetadataModification this[string metadataName]
            {
                get
                {
                    MetadataModification modification = null;
                    if (!_modifications.TryGetValue(metadataName, out modification))
                    {
                        if (_keepOnlySpecified)
                        {
                            // This metadata was not specified and we are only keeping specified metadata, so remove it.
                            return MetadataModification.CreateFromRemove();
                        }

                        return MetadataModification.CreateFromNoChange();
                    }

                    return modification;
                }

                set
                {
                    ErrorUtilities.VerifyThrowInternalNull(value, "value");
                    _modifications[metadataName] = value;
                }
            }
        }

        /// <summary>
        /// A type of metadata modification.
        /// </summary>
        internal enum ModificationType
        {
            /// <summary>
            /// Indicates the metadata value should be kept unchanged.
            /// </summary>
            Keep,

            /// <summary>
            /// Indicates the metadata value should be changed.
            /// </summary>
            Update,

            /// <summary>
            /// Indicates the metadata value should be removed.
            /// </summary>
            Remove
        }

        /// <summary>
        /// Represents a modification for a single metadata.
        /// </summary>
        internal class MetadataModification
        {
            /// <summary>
            /// When true, indicates the metadata should be removed from the target item.
            /// </summary>
            private readonly bool _remove;

            /// <summary>
            /// The value to which the metadata should be set.  If null, the metadata value should be retained unmodified.
            /// </summary>
            private readonly string _newValue;

            /// <summary>
            /// A modification which indicates the metadata value should be retained without modification.
            /// </summary>
            private static readonly MetadataModification s_keepModification = new MetadataModification(ModificationType.Keep);

            /// <summary>
            /// A modification which indicates the metadata should be removed.
            /// </summary>
            private static readonly MetadataModification s_removeModification = new MetadataModification(ModificationType.Remove);

            /// <summary>
            /// Constructor for metadata modifications of type Keep or Remove.
            /// </summary>
            /// <param name="modificationType">The type of modification to make.</param>
            private MetadataModification(ModificationType modificationType)
            {
                ErrorUtilities.VerifyThrow(modificationType != ModificationType.Update, "Modification type may only be update when a value is specified.");
                _remove = modificationType == ModificationType.Remove;
                _newValue = null;
            }

            /// <summary>
            /// Constructor for metadata modifications of type Update.
            /// </summary>
            /// <param name="value">The new value for the metadata.</param>
            private MetadataModification(string value)
            {
                _remove = false;
                _newValue = value;
            }

            /// <summary>
            /// Creates a metadata modification of type Keep.
            /// </summary>
            /// <returns>The metadata modification.</returns>
            public static MetadataModification CreateFromNoChange()
            {
                return s_keepModification;
            }

            /// <summary>
            /// Creates a metadata modification of type Update with the specified metadata value.
            /// </summary>
            /// <param name="newValue">The new metadata value.</param>
            /// <returns>The metadata modification.</returns>
            public static MetadataModification CreateFromNewValue(string newValue)
            {
                return new MetadataModification(newValue);
            }

            /// <summary>
            /// Creates a metadata modification of type Remove.
            /// </summary>
            /// <returns>The metadata modification.</returns>
            public static MetadataModification CreateFromRemove()
            {
                return s_removeModification;
            }

            /// <summary>
            /// When true, this modification indicates the associated metadata should be removed.
            /// </summary>
            public bool Remove
            {
                get { return _remove; }
            }

            /// <summary>
            /// When true, this modification indicates the associated metadata should retain its existing value.
            /// </summary>
            public bool KeepValue
            {
                get { return (_remove == false && _newValue == null); }
            }

            /// <summary>
            /// The new value of the metadata.  Only valid when <see cref="Remove"/> is false.
            /// </summary>
            public string NewValue
            {
                get { return _newValue; }
            }
        }

        /// <summary>
        /// Represents an entry in the lookup list.
        /// Class rather than a struct so that it can be modified in the list.
        /// </summary>
        internal class Scope
        {
            /// <summary>
            /// Contains all of the original items at this level in the Lookup
            /// </summary>
            private ItemDictionary<ProjectItemInstance> _items;

            /// <summary>
            /// Contains all of the items which have been added at this level in the Lookup
            /// </summary>
            private ItemDictionary<ProjectItemInstance> _adds;

            /// <summary>
            /// Contails all of the items which have been removed at this level in the Lookup
            /// </summary>
            private ItemDictionary<ProjectItemInstance> _removes;

            /// <summary>
            /// Contains all of the metadata which has been changed for items at this level in the Lookup.
            /// Schema: { K=type, V= { K=item, V=table of { K=metadata name, V=metadata value }}}
            /// </summary>
            private ItemTypeToItemsMetadataUpdateDictionary _modifies;

            /// <summary>
            /// Contains all of the original properties at this level in the Lookup
            /// </summary>
            private PropertyDictionary<ProjectPropertyInstance> _properties;

            /// <summary>
            /// Contains all of the properties which have been set at this level or above in the Lookup
            /// </summary>
            private PropertyDictionary<ProjectPropertyInstance> _propertySets;

            /// <summary>
            /// The managed thread id which entered this scope.
            /// </summary>
            private int _threadIdThatEnteredScope;

            /// <summary>
            /// A description of this scope, for error checking
            /// </summary>
            private string _description;

            /// <summary>
            /// The lookup which owns this scope, for error checking.
            /// </summary>
            private Lookup _owningLookup;

            /// <summary>
            /// Indicates whether or not further levels in the Lookup should be consulted beyond this one
            /// to find the actual value for the desired item or property.
            /// </summary>
            private bool _truncateLookupsAtThisScope;

            internal Scope(Lookup lookup, string description, ItemDictionary<ProjectItemInstance> items, PropertyDictionary<ProjectPropertyInstance> properties)
            {
                _owningLookup = lookup;
                _description = description;
                _items = items;
                _adds = null;
                _removes = null;
                _modifies = null;
                _properties = properties;
                _propertySets = null;
                _threadIdThatEnteredScope = Thread.CurrentThread.ManagedThreadId;
                _truncateLookupsAtThisScope = false;
            }

            /// <summary>
            /// The main table, populated with items that
            /// are initially visible in this scope. Does not 
            /// include adds or removes unless it's the table in 
            /// the outermost scope.
            /// </summary>
            internal ItemDictionary<ProjectItemInstance> Items
            {
                get { return _items; }
                set { _items = value; }
            }
            /// <summary>
            /// Adds made in this scope or above.
            /// </summary>
            internal ItemDictionary<ProjectItemInstance> Adds
            {
                get { return _adds; }
                set { _adds = value; }
            }
            /// <summary>
            /// Removes made in this scope or above.
            /// </summary>
            internal ItemDictionary<ProjectItemInstance> Removes
            {
                get { return _removes; }
                set { _removes = value; }
            }
            /// <summary>
            /// Modifications made in this scope or above.
            /// </summary>
            internal ItemTypeToItemsMetadataUpdateDictionary Modifies
            {
                get { return _modifies; }
                set { _modifies = value; }
            }
            /// <summary>
            /// The main property table, populated with properties
            /// that are initially visible in this scope. Does not
            /// include sets unless it's the table in the outermost scope.
            /// </summary>
            internal PropertyDictionary<ProjectPropertyInstance> Properties
            {
                get { return _properties; }
                set { _properties = value; }
            }
            /// <summary>
            /// Properties set in this scope or above.
            /// </summary>
            internal PropertyDictionary<ProjectPropertyInstance> PropertySets
            {
                get { return _propertySets; }
                set { _propertySets = value; }
            }
            /// <summary>
            /// ID of thread owning this scope
            /// </summary>
            internal int ThreadIdThatEnteredScope
            {
                get { return _threadIdThatEnteredScope; }
            }
            /// <summary>
            /// Whether to stop lookups going beyond this scope downwards
            /// </summary>
            internal bool TruncateLookupsAtThisScope
            {
                get { return _truncateLookupsAtThisScope; }
            }

            /// <summary>
            /// The description assigned to this scope.
            /// </summary>
            internal string Description
            {
                get { return _description; }
            }

            /// <summary>
            /// Leaves the current lookup scope.
            /// </summary>
            internal void LeaveScope()
            {
                _owningLookup.LeaveScope(this);
            }
        }
    }
}
