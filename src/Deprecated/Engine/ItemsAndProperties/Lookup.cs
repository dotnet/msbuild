// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BuildEngine.Shared;
using System.Threading;

namespace Microsoft.Build.BuildEngine
{
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
    /// <remarks>
    /// THREAD SAFETY:
    ///     - BuildItemGroups are currently unsafe for concurrent reading and writing (they have a List field). So a Lookup cannot be read and written to 
    ///       concurrently.
    ///     - To avoid this problem, the lookup can be populated with a clone of an item group, and lookup can be Truncate()'d at the level of that clone 
    ///       until control of the lookup goes back to the safe thread.
    /// 
    /// FUTURE:
    ///     - We could eliminate all the code performing resetting of project build state (currently implemented using special tables for Output properties and 
    ///       backups of persisted item groups and metadata before modification) by using a Lookup, entering scope at the start of a build, 
    ///       then when build state needs to be reset, throwing away the Lookup (rather than leaving scope).
    /// </remarks>
    internal class Lookup
    {
        #region Fields

        /// <summary>
        /// Ordered list of entries used for lookup.
        /// Each entry contains multiple tables:
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
        private LinkedList<LookupEntry> lookupEntries = new LinkedList<LookupEntry>();

        /// <summary>
        /// Projects store their items in a hashtable of item groups by name (which we handle in our lookup table)
        /// but also in a single item group. When we leave scope the last time, we have to update this item group as 
        /// well. This is only used when we leave scope the last time.
        /// </summary>
        private BuildItemGroup projectItems;

        /// <summary>
        /// When we are asked for all the items of a certain type using the GetItems() method, we may have to handle items
        /// that have been modified earlier with ModifyItems(). These pending modifications can't be applied immediately to
        /// the item because that would affect other batches. Instead we clone the item, apply the modification, and hand that over.
        /// The problem is that later we might get asked to remove or modify that item. We want to make sure that we record that as
        /// a remove or modify of the real item, not the clone we handed over. So we keep a lookup of (clone, original) to consult.
        /// The "beautiful" alternative to this would probably involve giving items the concept of a pending change, but this works
        /// for what we need it to do right now.
        /// </summary>
        private Dictionary<BuildItem, BuildItem> cloneTable;

        /// <summary>
        /// Read-only wrapper around this lookup.
        /// </summary>
        private ReadOnlyLookup readOnlyLookup;

        /// <summary>
        /// Library of default metadata to apply to items added to this lookup.
        /// </summary>
        private ItemDefinitionLibrary itemDefinitionLibrary;

        #endregion

        #region Constructors

        internal Lookup(Hashtable itemsByName, BuildPropertyGroup properties, ItemDefinitionLibrary itemDefinitionLibrary)
            : this(itemsByName, new BuildItemGroup(), properties, itemDefinitionLibrary)
        { }

        internal Lookup(Hashtable itemsByName, BuildItemGroup projectItems, BuildPropertyGroup properties, ItemDefinitionLibrary itemDefinitionLibrary)
        {
            ErrorUtilities.VerifyThrow(itemDefinitionLibrary != null, "Expect library");

            this.projectItems = projectItems;
            this.itemDefinitionLibrary = itemDefinitionLibrary;
            LookupEntry entry = new LookupEntry(itemsByName, properties);
            lookupEntries.AddFirst(entry);
        }

        /// <summary>
        /// Copy constructor (called via Clone() - clearer semantics)
        /// </summary>
        private Lookup(Lookup that)
        {
            // Add the same tables from the original
            foreach (LookupEntry entry in that.lookupEntries)
            {
                this.lookupEntries.AddLast(entry);
            }
            this.projectItems = that.projectItems;
            this.itemDefinitionLibrary = that.itemDefinitionLibrary;

            // Clones need to share an (item)clone table; the batching engine asks for items from the lookup,
            // then populates buckets with them, which have clone lookups.
            this.cloneTable = that.cloneTable;
        }

#endregion

        #region Properties

        /// <summary>
        /// Returns a read-only wrapper around this lookup
        /// </summary>
        internal ReadOnlyLookup ReadOnlyLookup
        {
            get
            {
                if (readOnlyLookup == null)
                {
                    readOnlyLookup = new ReadOnlyLookup(this);
                }
                return readOnlyLookup;
            }
        }

        // Convenience private properties
        // "Primary" is the "top" or "innermost" scope
        // "Secondary" is the next from the top.
        private Hashtable PrimaryTable
        {
            get { return lookupEntries.First.Value.Items; }
            set { lookupEntries.First.Value.Items = value; }
        }

        private Hashtable PrimaryAddTable
        {
            get { return lookupEntries.First.Value.Adds; }
            set { lookupEntries.First.Value.Adds = value; }
        }

        private Hashtable PrimaryRemoveTable
        {
            get { return lookupEntries.First.Value.Removes; }
            set { lookupEntries.First.Value.Removes = value; }
        }

        private Dictionary<string, Dictionary<BuildItem, Dictionary<string, string>>> PrimaryModifyTable
        {
            get { return lookupEntries.First.Value.Modifies; }
            set { lookupEntries.First.Value.Modifies = value; }
        }

        private BuildPropertyGroup PrimaryPropertySets
        {
            get { return lookupEntries.First.Value.PropertySets; }
            set { lookupEntries.First.Value.PropertySets = value; }
        }

        private Hashtable SecondaryTable
        {
            get { return lookupEntries.First.Next.Value.Items; }
            set { lookupEntries.First.Next.Value.Items = value; }
        }

        private Hashtable SecondaryAddTable
        {
            get { return lookupEntries.First.Next.Value.Adds; }
            set { lookupEntries.First.Next.Value.Adds = value; }
        }

        private Hashtable SecondaryRemoveTable
        {
            get { return lookupEntries.First.Next.Value.Removes; }
            set { lookupEntries.First.Next.Value.Removes = value; }
        }

        private Dictionary<string, Dictionary<BuildItem, Dictionary<string, string>>> SecondaryModifyTable
        {
            get { return lookupEntries.First.Next.Value.Modifies; }
            set { lookupEntries.First.Next.Value.Modifies = value; }
        }

        private BuildPropertyGroup SecondaryProperties
        {
            get { return lookupEntries.First.Next.Value.Properties; }
            set { lookupEntries.First.Next.Value.Properties = value; }
        }

        private BuildPropertyGroup SecondaryPropertySets
        {
            get { return lookupEntries.First.Next.Value.PropertySets; }
            set { lookupEntries.First.Next.Value.PropertySets = value; }
        }

#endregion

        #region Internal Methods

        /// <summary>
        /// Compares the primary property sets of the passed in lookups to determine if there are properties which are shared between
        /// the lookups. We find these shared property names because this indicates that the current Lookup is overriding the property value of another Lookup
        /// When an override is detected a messages is generated to inform the users that the property is being changed between batches
        /// </summary>
        /// <returns>array or error messages to log </returns>
        internal List<string> GetPropertyOverrideMessages(Hashtable lookupHash)
        {
            List<string> errorMessages = null;
            // For each batch lookup list we need to compare the property items to see if they have already been set
            if (PrimaryPropertySets != null)
            {
                foreach (BuildProperty property in PrimaryPropertySets)
                {
                    string propertyName = property.Name;
                    // If the hash contains the property name, output a messages that displays the previous property value and the new property value
                    if (lookupHash.ContainsKey(propertyName))
                    {
                        if (errorMessages == null)
                        {
                            errorMessages = new List<string>();
                        }
                        errorMessages.Add(ResourceUtilities.FormatResourceString("PropertyOutputOverridden", propertyName, lookupHash[propertyName], property.FinalValueEscaped));
                    }

                    // Set the value of the hash to the new property value
                    lookupHash[propertyName] = property.FinalValueEscaped;
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
        /// Push the tables down and add a fresh new primary entry at the top.
        /// Returns the new scope. In general, callers should not use this returned scope.
        /// </summary>
        internal LookupEntry EnterScope()
        {
            // We don't create the tables unless we need them
            LookupEntry entry = new LookupEntry(null, null);
            lookupEntries.AddFirst(entry);
            return entry;
        }

        /// <summary>
        /// Moves all tables up one: the tertiary table becomes the secondary table, and so on. The primary
        /// and secondary table are merged. This has the effect of "applying" the adds applied to the primary
        /// table into the secondary table.
        /// </summary>
        internal void LeaveScope()
        {
            MustBeOwningThread();
            ErrorUtilities.VerifyThrowNoAssert(lookupEntries.Count >= 2, "Too many calls to Leave().");

            // Our lookup works by stopping the first time it finds an item group of the appropriate type. 
            // So we can't apply an add directly into the table below because that could create a new group 
            // of that type, which would cause the next lookup to stop there and miss any existing items in a table below.
            // Instead we keep adds stored separately until we're leaving the very last scope. Until then
            // we only move adds down into the next add table below, and when we lookup we consider both tables.
            // Same applies to removes.
            if (lookupEntries.Count == 2)
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
            cloneTable = null;

            // Move all tables up one, discarding the primary tables
            lookupEntries.RemoveFirst();
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
                    foreach (DictionaryEntry entry in PrimaryAddTable)
                    {
                        ImportItemsIntoTable(SecondaryAddTable, (string)entry.Key, (BuildItemGroup)entry.Value);
                    }
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
                    foreach (DictionaryEntry entry in PrimaryRemoveTable)
                    {
                        ImportItemsIntoTable(SecondaryRemoveTable, (string)entry.Key, (BuildItemGroup)entry.Value);
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
                    foreach (KeyValuePair<string, Dictionary<BuildItem, Dictionary<string, string>>> entry in PrimaryModifyTable)
                    {
                        Dictionary<BuildItem, Dictionary<string, string>> modifiesOfType;
                        if (SecondaryModifyTable.TryGetValue(entry.Key, out modifiesOfType))
                        {
                            // There are already modifies of this type: add to the existing table
                            foreach (KeyValuePair<BuildItem, Dictionary<string, string>> modify in entry.Value)
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
                foreach (DictionaryEntry entry in PrimaryAddTable)
                {
                    SecondaryTable = Utilities.CreateTableIfNecessary(SecondaryTable);
                    ImportItemsIntoTable(SecondaryTable, (string)entry.Key, (BuildItemGroup)entry.Value);
                    projectItems.ImportItems((BuildItemGroup)entry.Value);
                }
            }

            if (PrimaryRemoveTable != null)
            {
                foreach (DictionaryEntry entry in PrimaryRemoveTable)
                {
                    SecondaryTable = Utilities.CreateTableIfNecessary(SecondaryTable);
                    RemoveItemsFromTableWithBackup(SecondaryTable, (string)entry.Key, (BuildItemGroup)entry.Value);
                    projectItems.RemoveItemsWithBackup((BuildItemGroup)entry.Value);
                }
            }

            if (PrimaryModifyTable != null)
            {
                foreach (KeyValuePair<string, Dictionary<BuildItem, Dictionary<string, string>>> entry in PrimaryModifyTable)
                {
                    SecondaryTable = Utilities.CreateTableIfNecessary(SecondaryTable);
                    ApplyModificationsToTable(SecondaryTable, entry.Key, entry.Value);
                    // Don't have to touch projectItems -- it contains the same set of items
                }
            }

            if (PrimaryPropertySets != null)
            {
                SecondaryProperties = CreatePropertyGroupIfNecessary(SecondaryProperties);

                // At present, this automatically does a backup of any
                // original persisted property because we're using Output properties.
                SecondaryProperties.ImportProperties(PrimaryPropertySets);
            }
        }

        /// <summary>
        /// Gets the effective property for the current scope.
        /// If no match is found, returns null.
        /// Caller must not modify the property returned.
        /// </summary>
        internal BuildProperty GetProperty(string name)
        {
            // Walk down the tables and stop when the first 
            // property with this name is found
            foreach (LookupEntry entry in lookupEntries)
            {
                if (entry.PropertySets != null)
                {
                    BuildProperty property = entry.PropertySets[name];
                    if (property != null)
                    {
                        return property;
                    }
                }

                if (entry.Properties != null)
                {
                    BuildProperty property = entry.Properties[name];
                    if (property != null)
                    {
                        return property;
                    }
                }

                if (entry.TruncateLookupsAtThisScope)
                {
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the items of the specified type that are visible in the current scope.
        /// If no match is found, returns null.
        /// Caller must not modify the group returned.
        /// </summary>
        internal BuildItemGroup GetItems(string name)
        {
            // The visible items consist of the adds (accumulated as we go down)
            // plus the first set of regular items we encounter
            // minus any removes
            BuildItemGroup allAdds = null;
            BuildItemGroup allRemoves = null;
            Dictionary<BuildItem, Dictionary<string, string>> allModifies = null;
            BuildItemGroup groupFound = null;

            foreach (LookupEntry entry in lookupEntries)
            {
                // Accumulate adds while we look downwards
                if (entry.Adds != null)
                {
                    BuildItemGroup adds = (BuildItemGroup)entry.Adds[name];
                    if (adds != null)
                    {
                        allAdds = CreateItemGroupIfNecessary(allAdds);
                        allAdds.ImportItems(adds);
                    }
                }

                // Accumulate removes while we look downwards
                if (entry.Removes != null)
                {
                    BuildItemGroup removes = (BuildItemGroup)entry.Removes[name];
                    if (removes != null)
                    {
                        allRemoves = CreateItemGroupIfNecessary(allRemoves);
                        allRemoves.ImportItems(removes);
                    }
                }

                // Accumulate modifications as we look downwards
                if (entry.Modifies != null)
                {
                    Dictionary<BuildItem, Dictionary<string, string>> modifies;
                    if (entry.Modifies.TryGetValue(name, out modifies))
                    {
                        if (allModifies == null)
                        {
                            allModifies = new Dictionary<BuildItem, Dictionary<string, string>>();
                        }

                        // We already have some modifies for this type
                        foreach (KeyValuePair<BuildItem, Dictionary<string, string>> modify in modifies)
                        {
                            // If earlier scopes modify the same metadata on the same item,
                            // they have priority
                            MergeModificationsIntoModificationTable(allModifies, modify, ModifyMergeType.FirstWins);
                        }
                    }
                }

                if (entry.Items != null)
                {
                    groupFound = (BuildItemGroup)entry.Items[name];
                    if (groupFound != null)
                    {
                        // Found a group: we go no further
                        break;
                    }
                }

                if (entry.TruncateLookupsAtThisScope)
                {
                    break;
                }
            }

            if ((allAdds == null || allAdds.Count == 0) &&
                (allRemoves == null || allRemoves.Count == 0) &&
                (allModifies == null || allModifies.Count == 0))
            {
                // We can just hand out this group verbatim -
                // that avoids any importing
                if (groupFound == null)
                {
                    groupFound = new BuildItemGroup();
                }

                return groupFound;
            }

            // We have adds and/or removes and/or modifies to incorporate.
            // We can't modify the group, because that might
            // be visible to other batches; we have to create
            // a new one.
            BuildItemGroup result = new BuildItemGroup();

            if (groupFound != null)
            {
                result.ImportItems(groupFound);
            }
            // Removes are processed after adds; this means when we remove there's no need to concern ourselves
            // with the case where the item being removed is in an add table somewhere. The converse case is not possible
            // using a project file: a project file cannot create an item that was already removed, it can only create
            // a unique new item.
            if (allAdds != null)
            {
                result.ImportItems(allAdds);
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

            return result;
        }

        /// <summary>
        /// Populates with an item group. This is done before the item lookup is used in this scope.
        /// Assumes all the items in the group have the same, provided, type.
        /// Assumes there is no item group of this type in the primary table already.
        /// </summary>
        internal void PopulateWithItems(string name, BuildItemGroup group)
        {
            MustBeOwningThread();

            PrimaryTable = Utilities.CreateTableIfNecessary(PrimaryTable);
            BuildItemGroup existing = (BuildItemGroup)PrimaryTable[name];
            ErrorUtilities.VerifyThrow(existing == null, "Cannot add an itemgroup of this type.");
            PrimaryTable[name] = group;
        }

        /// <summary>
        /// Populates with an item. This is done before the item lookup is used in this scope.
        /// There may or may not already be a group for it.
        /// </summary>
        internal void PopulateWithItem(BuildItem item)
        {
            MustBeOwningThread();

            PrimaryTable = Utilities.CreateTableIfNecessary(PrimaryTable);
            ImportItemIntoTable(PrimaryTable, item);
        }

        /// <summary>
        /// Apply a property to this scope.
        /// </summary>
        internal void SetProperty(BuildProperty property)
        {
            MustBeOwningThread();

            // At present resetting of build state is done by marking properties as output properties;
            // until resetting is also done using scopes, we can expect that all new properties will be output properties,
            // so they can be reset.
            ErrorUtilities.VerifyThrow(property.Type == PropertyType.OutputProperty, "Expected output property");

            // Setting in outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

            // Put in the set table
            PrimaryPropertySets = CreatePropertyGroupIfNecessary(PrimaryPropertySets);
            PrimaryPropertySets.SetProperty(property);
        }

        /// <summary>
        /// Implements a true add, an item that has been created in a batch.
        /// </summary>
        internal void AddNewItems(BuildItemGroup group)
        {
            MustBeOwningThread();

             // Adding to outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

#if DEBUG
            foreach (BuildItem item in group)
            {
                MustNotBeInAnyTables(item);
            }
#endif

            if (group.Count == 0)
            {
                return;
            }

            foreach (BuildItem item in group)
            {
                // We only expect to add virtual items during the build
                ErrorUtilities.VerifyThrow(!item.IsPartOfProjectManifest, "Cannot dynamically add manifest items");
                item.ItemDefinitionLibrary = this.itemDefinitionLibrary;
            }

            // Put them in the add table
            PrimaryAddTable = Utilities.CreateTableIfNecessary(PrimaryAddTable);
            ImportItemsIntoTable(PrimaryAddTable, group[0].Name, group);
        }

        /// <summary>
        /// Implements a true add, an item that has been created in a batch.
        /// </summary>
        internal void AddNewItem(BuildItem item)
        {
            MustBeOwningThread();
            // We only expect to add virtual items during the build
            ErrorUtilities.VerifyThrow(!item.IsPartOfProjectManifest, "Cannot dynamically add manifest items");

            // Adding to outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

#if DEBUG
            // This item must not be in any table already; a project cannot create an item
            // that already exists
            MustNotBeInAnyTables(item);
#endif
            item.ItemDefinitionLibrary = this.itemDefinitionLibrary;

            // Put in the add table
            PrimaryAddTable = Utilities.CreateTableIfNecessary(PrimaryAddTable);
            ImportItemIntoTable(PrimaryAddTable, item);
        }

        /// <summary>
        /// Remove a bunch of items from this scope
        /// </summary>
        internal void RemoveItems(List<BuildItem> items)
        {
            MustBeOwningThread();

            foreach (BuildItem item in items)
            {
                RemoveItem(item);
            }
        }

        /// <summary>
        /// Remove an item from this scope
        /// </summary>
        internal void RemoveItem(BuildItem item)
        {
            MustBeOwningThread();

            // Removing from outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

            item = RetrieveOriginalFromCloneTable(item);

            // Put in the remove table
            PrimaryRemoveTable = Utilities.CreateTableIfNecessary(PrimaryRemoveTable);
            ImportItemIntoTable(PrimaryRemoveTable, item);

            // No need to remove this item from the primary add table if it's 
            // already there -- we always apply removes after adds, so that add 
            // will be reversed anyway.
        }

        /// <summary>
        /// Modifies items in this scope with the same set of metadata modifications.
        /// Assumes all the items in the group have the same, provided, type.
        /// </summary>
        internal void ModifyItems(string name, BuildItemGroup group, Dictionary<string, string> metadataChanges)
        {
            MustBeOwningThread();

            // Modifying in outer scope could be easily implemented, but our code does not do it at present
            MustNotBeOuterScope();

#if DEBUG
            // This item should not already be in any remove table; there is no way a project can 
            // modify items that were already removed
            // Obviously, do this only in debug, as it's a slow check for bugs.
            LinkedListNode<LookupEntry> node = lookupEntries.First;
            while (node != null)
            {
                LookupEntry entry = node.Value;
                foreach (BuildItem item in group)
                {
                    BuildItem actualItem = RetrieveOriginalFromCloneTable(item);
                    MustNotBeInTable(entry.Removes, actualItem);
                }
                node = node.Next;
            }
#endif

            if (metadataChanges.Count == 0)
            {
                return;
            }

            // Put in the modify table

            // We don't need to check whether the item is in the add table vs. the main table; either
            // way the modification will be applied.
            PrimaryModifyTable = CreateTableIfNecessary(PrimaryModifyTable);
            Dictionary<BuildItem, Dictionary<string, string>> modifiesOfType;
            if (!PrimaryModifyTable.TryGetValue(name, out modifiesOfType))
            {
                modifiesOfType = new Dictionary<BuildItem, Dictionary<string, string>>();
                PrimaryModifyTable[name] = modifiesOfType;
            }

            foreach (BuildItem item in group)
            {
                // If we're asked to modify a clone we handed out, record it as a modify of the original
                // instead
                BuildItem actualItem = RetrieveOriginalFromCloneTable(item);
                KeyValuePair<BuildItem, Dictionary<string, string>> modify = new KeyValuePair<BuildItem, Dictionary<string, string>>(actualItem, metadataChanges);
                MergeModificationsIntoModificationTable(modifiesOfType, modify, ModifyMergeType.SecondWins);
            }
        }

#endregion

        #region Private Methods

        /// <summary>
        /// Apply modifies to a temporary result group.
        /// Items to be modified are virtual-cloned so the original isn't changed.
        /// </summary>
        private void ApplyModifies(BuildItemGroup result, Dictionary<BuildItem, Dictionary<string, string>> allModifies)
        {
            // Clone, because we're modifying actual items, and this would otherwise be visible to other batches,
            // and would be "published" even if a target fails.
            // FUTURE - don't need to clone here for non intrinsic tasks, but at present, they don't do modifies

            // Store the clone, in case we're asked to modify or remove it later (we will record it against the real item)
            if (cloneTable == null)
            {
                cloneTable = new Dictionary<BuildItem, BuildItem>();
            }

            foreach (KeyValuePair<BuildItem, Dictionary<string, string>> modify in allModifies)
            {
                BuildItem clone = result.ModifyItemAfterCloningUsingVirtualMetadata(modify.Key, modify.Value);

                // This will be null if the item wasn't in the result group, ie, it had been removed after being modified
                if (clone != null)
                {
#if DEBUG
                    ErrorUtilities.VerifyThrow(!cloneTable.ContainsKey(clone), "Should be new, not already in table!");
#endif
                    cloneTable[clone] = modify.Key;
                }
            }
        }

        /// <summary>
        /// Look up the "real" item by using its clone, and return the real item.
        /// See <see cref="cloneTable"/> for explanation of the clone table.
        /// </summary>
        private BuildItem RetrieveOriginalFromCloneTable(BuildItem item)
        {
            BuildItem original;
            if (cloneTable != null)
            {
                if (cloneTable.TryGetValue(item, out original))
                {
                    item = original;
                }
            }

            return item;
        }

        /// <summary>
        /// Puts items from the group into the table.
        /// Assumes all the items in the group have the same, provided, type.
        /// There may or may not already be a group for it.
        /// </summary>
        private void ImportItemsIntoTable(Hashtable table, string name, BuildItemGroup group)
        {
            BuildItemGroup existing = (BuildItemGroup)table[name];
            if (existing == null)
            {
                table[name] = group;
            }
            else
            {
                existing.ImportItems(group);
            }
        }

        /// <summary>
        /// Removes items from the group from the table.
        /// Assumes all the items in the group have the same, provided, type.
        /// </summary>
        private void RemoveItemsFromTableWithBackup(Hashtable table, string name, BuildItemGroup group)
        {
            BuildItemGroup existing = (BuildItemGroup)table[name];
            existing?.RemoveItemsWithBackup(group);
        }

        /// <summary>
        /// Applies a list of modifications to the appropriate BuildItemGroup in a main table.
        /// If any modifications conflict, these modifications win.
        /// </summary>
        private void ApplyModificationsToTable(Hashtable table, string name, Dictionary<BuildItem, Dictionary<string, string>> modify)
        {
            BuildItemGroup existing = (BuildItemGroup)table[name];
            existing?.ModifyItemsUsingVirtualMetadata(modify);
        }

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
        /// Applies a modification to an item in a table of modifications.
        /// If the item already exists in the table, merges in the modifications; if there is a conflict
        /// the mergeType indicates which should win.
        /// </summary>
        private void MergeModificationsIntoModificationTable(Dictionary<BuildItem, Dictionary<string, string>> modifiesOfType,
                                                             KeyValuePair<BuildItem, Dictionary<string, string>> modify,
                                                             ModifyMergeType mergeType)
        {
            Dictionary<string, string> existingMetadataChanges;
            if (modifiesOfType.TryGetValue(modify.Key, out existingMetadataChanges))
            {
                // There's already modifications for this item; merge with those
                foreach (KeyValuePair<string, string> metadataChange in modify.Value)
                {
                    if (mergeType == ModifyMergeType.SecondWins)
                    {
                        existingMetadataChanges[metadataChange.Key] = metadataChange.Value;
                    }
                    else
                    {
                        // Any existing value wins
                        if (!existingMetadataChanges.ContainsKey(metadataChange.Key))
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

        /// <summary>
        /// Puts the item into the table.
        /// </summary>
        private void ImportItemIntoTable(Hashtable table, BuildItem item)
        {
            BuildItemGroup existing = (BuildItemGroup)table[item.Name];
            if (existing == null)
            {
                existing = new BuildItemGroup();
                table.Add(item.Name, existing);
            }
            existing.AddItem(item);
        }

        /// <summary>
        /// Helper useful since we only create tables if they're needed
        /// </summary>
        private Dictionary<string, Dictionary<BuildItem, Dictionary<string, string>>> CreateTableIfNecessary(Dictionary<string, Dictionary<BuildItem, Dictionary<string, string>>> table)
        {
            if (table == null)
            {
                return new Dictionary<string, Dictionary<BuildItem, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            }

            return table;
        }

        /// <summary>
        /// Helper useful since we only create groups if they're needed
        /// </summary>
        private BuildPropertyGroup CreatePropertyGroupIfNecessary(BuildPropertyGroup properties)
        {
            if (properties == null)
            {
                return new BuildPropertyGroup();
            }

            return properties;
        }

        /// <summary>
        /// Helper useful since we only create groups if they're needed
        /// </summary>
        private BuildItemGroup CreateItemGroupIfNecessary(BuildItemGroup items)
        {
            if (items == null)
            {
                return new BuildItemGroup();
            }

            return items;
        }

#if DEBUG
        /// <summary>
        /// Verify item is not in the table
        /// </summary>
        private void MustNotBeInTable(Hashtable table, BuildItem item)
        {
            BuildItemGroup group = new BuildItemGroup();
            group.AddItem(item);
            MustNotBeInTable(table, item.Name, group);
        }

        /// <summary>
        /// Verify item is not in the modify table
        /// </summary>
        private void MustNotBeInTable(Dictionary<string, Dictionary<BuildItem, Dictionary<string, string>>> table, BuildItem item)
        {
            if (table?.ContainsKey(item.Name) == true)
            {
                Dictionary<BuildItem, Dictionary<string, string>> tableOfItemsOfSameType = table[item.Name];
                if (tableOfItemsOfSameType != null)
                {
                    ErrorUtilities.VerifyThrowNoAssert(!tableOfItemsOfSameType.ContainsKey(item), "Item should not be in table");
                }
            }
        }

        /// <summary>
        /// Verify items in the group are not in the table
        /// Assumes all items in the group have the same, specified, type
        /// </summary>
        private void MustNotBeInTable(Hashtable table, string name, BuildItemGroup group)
        {
            if (table?.ContainsKey(name) == true)
            {
                BuildItemGroup existing = (BuildItemGroup)table[name];
                if (existing != null)
                {
                    foreach (BuildItem item in group)
                    {
                        ErrorUtilities.VerifyThrowNoAssert(!existing.Items.Contains(item), "Item should not be in table");
                    }
                }
            }
        }

        /// <summary>
        /// Verify item is not in any table in any scope
        /// </summary>        
        private void MustNotBeInAnyTables(BuildItem item)
        {
            // This item should not already be in any table; there is no way a project can
            // create items that already existed
            // Obviously, do this only in debug, as it's a slow check for bugs.
            LinkedListNode<LookupEntry> node = lookupEntries.First;
            while (node != null)
            {
                LookupEntry entry = node.Value;
                MustNotBeInTable(entry.Adds, item);
                MustNotBeInTable(entry.Removes, item);
                MustNotBeInTable(entry.Modifies, item);
                node = node.Next;
            }
        }

#endif

        /// <summary>
        /// Verify the thread that created the scope is the one modifying it.
        /// </summary>
        private void MustBeOwningThread()
        {
            int threadIdThatEnteredCurrentScope = lookupEntries.First.Value.ThreadIdThatEnteredScope;
            ErrorUtilities.VerifyThrowNoAssert(threadIdThatEnteredCurrentScope == Thread.CurrentThread.ManagedThreadId, "Only the thread that entered a scope may modify or leave it");
        }

        /// <summary>
        /// Add/remove/modify/set directly on an outer scope would need to be handled separately - it would apply
        /// directly to the main tables. Our code isn't expected to do this.
        /// </summary>
        private void MustNotBeOuterScope()
        {
            ErrorUtilities.VerifyThrowNoAssert(lookupEntries.Count > 1, "Operation in outer scope not supported");
        }

        #endregion
    }

    #region Related Types

    /// <summary>
    /// Read-only wrapper around a lookup.
    /// Passed to Expander and ItemExpander, which only need to
    /// use a lookup in a read-only fashion, thus increasing 
    /// encapsulation of the data in the Lookup.
    /// </summary>
    internal class ReadOnlyLookup
    {
        private Lookup lookup;

        internal ReadOnlyLookup(Lookup lookup)
        {
            this.lookup = lookup;
        }

        internal ReadOnlyLookup(Hashtable items, BuildPropertyGroup properties)
        {
            // Lookup only needs ItemDefinitionLibrary to mark new items with it.
            // Since we're a read-only lookup, we don't need a real one.
            this.lookup = new Lookup(items, properties, new ItemDefinitionLibrary(null));
        }

        internal BuildItemGroup GetItems(string name)
        {
            return lookup.GetItems(name);
        }

        internal BuildProperty GetProperty(string name)
        {
            return lookup.GetProperty(name);
        }
    }

    /// <summary>
    /// Represents an entry in the lookup list.
    /// Class rather than a struct so that it can be modified in the list.
    /// </summary>
    internal class LookupEntry
    {
        // This is a table of K=type, V=BuildItemGroup
        // The type is dictated by the storage in the project (and by thread safety)
        private Hashtable items;
        private Hashtable adds;
        private Hashtable removes;
        // Table of K=type, V= { table of K=item, V=table of { K=metadata name, V=metadata value }}
        private Dictionary<string, Dictionary<BuildItem, Dictionary<string, string>>> modifies;
        private BuildPropertyGroup properties;
        private BuildPropertyGroup propertySets;
        private int threadIdThatEnteredScope;
        private bool truncateLookupsAtThisScope;

        internal LookupEntry(Hashtable items, BuildPropertyGroup properties)
        {
            this.items = items;
            this.adds = null;
            this.removes = null;
            this.modifies = null;
            this.properties = properties;
            this.propertySets = null;
            this.threadIdThatEnteredScope = Thread.CurrentThread.ManagedThreadId;
            this.truncateLookupsAtThisScope = false;
        }

        /// <summary>
        /// The main table, populated with items that
        /// are initially visible in this scope. Does not 
        /// include adds or removes unless it's the table in 
        /// the outermost scope.
        /// </summary>
        internal Hashtable Items
        {
            get { return items; }
            set { items = value; }
        }
        /// <summary>
        /// Adds made in this scope or above.
        /// </summary>
        internal Hashtable Adds
        {
            get { return adds; }
            set { adds = value; }
        }
        /// <summary>
        /// Removes made in this scope or above.
        /// </summary>
        internal Hashtable Removes
        {
            get { return removes; }
            set { removes = value; }
        }
        /// <summary>
        /// Modifications made in this scope or above.
        /// </summary>
        internal Dictionary<string, Dictionary<BuildItem, Dictionary<string, string>>> Modifies
        {
            get { return modifies; }
            set { modifies = value; }
        }
        /// <summary>
        /// The main property table, populated with properties
        /// that are initially visible in this scope. Does not
        /// include sets unless it's the table in the outermost scope.
        /// </summary>
        internal BuildPropertyGroup Properties
        {
            get { return properties; }
            set { properties = value; }
        }
        /// <summary>
        /// Properties set in this scope or above.
        /// </summary>
        internal BuildPropertyGroup PropertySets
        {
            get { return propertySets; }
            set { propertySets = value; }
        }
        /// <summary>
        /// ID of thread owning this scope
        /// </summary>
        internal int ThreadIdThatEnteredScope
        {
            get { return threadIdThatEnteredScope; }
        }
        /// <summary>
        /// Whether to stop lookups going beyond this scope downwards
        /// </summary>
        internal bool TruncateLookupsAtThisScope
        {
            get { return truncateLookupsAtThisScope; }
            set { truncateLookupsAtThisScope = value; }
        }
    }
#endregion

}
