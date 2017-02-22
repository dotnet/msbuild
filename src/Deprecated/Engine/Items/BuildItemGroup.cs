// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using System.Diagnostics;

using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a collection of items.  It may be represented
    /// physically by an &lt;ItemGroup&gt; element persisted in the project file, 
    /// or it may just be a virtual BuildItemGroup (e.g., the evaluated items).
    /// </summary>
    [DebuggerDisplay("BuildItemGroup (Count = { Count }, Condition = { Condition })")]
    public class BuildItemGroup : IItemPropertyGrouping, IEnumerable
    {
        #region Member Data

        // Object holding the backing Xml, if any
        private BuildItemGroupXml xml;

        // Whether there is backing Xml
        // Can't use (xml==null) because it is consulted during construction of the backing Xml
        // Can't use (parentProject!=null) because Clone() sets it to null, but expects to create a persisted group...
        private bool isPersisted = false;

        // If this is a persisted <ItemGroup>, this boolean tells us whether
        // it came from the main project file, or an imported project file.
        bool importedFromAnotherProject = false;

        // These are the loose Items beneath this BuildItemGroup.  This is
        // valid for both persisted and virtual ItemGroups.
        private List<BuildItem> items;

        // If we are ever asked to back up our persisted items, we put the list
        // in here, so we can restore them later.
        private List<BuildItem> persistedItemBackup;

        // Collection propertygroup belongs to.
        private GroupingCollection parentCollection = null;

        // If this is a persisted BuildItemGroup, it has a parent Project object.
        private Project parentProject = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor, which initializes a virtual (non-persisted) BuildItemGroup.
        /// </summary>
        public BuildItemGroup()
        {
            this.items = new List<BuildItem>();
        }

        /// <summary>
        /// This constructor initializes the BuildItemGroup from an &lt;ItemGroup&gt; element
        /// in the project file.  It might come from the main project file or an
        /// imported project file.
        /// </summary>
        internal BuildItemGroup(XmlElement itemGroupElement, bool importedFromAnotherProject, Project parentProject)
            : this()
        {
            this.isPersisted = true;
            this.xml = new BuildItemGroupXml(itemGroupElement);
            this.importedFromAnotherProject = importedFromAnotherProject;
            this.parentProject = parentProject;

            List<XmlElement> children = xml.GetChildren();
            EnsureCapacity(children.Count);
            for (int i = 0; i < children.Count; i++)
            {
                AddExistingItem(new BuildItem(children[i], importedFromAnotherProject, parentProject.ItemDefinitionLibrary));
            }
        }

        /// <summary>
        /// Constructor, which creates a new &lt;ItemGroup&gt; element in the XML document
        /// specified.
        /// </summary>
        internal BuildItemGroup(XmlDocument ownerDocument, bool importedFromAnotherProject, Project parentProject)
            : this()
        {
            this.isPersisted = true;
            this.xml = new BuildItemGroupXml(ownerDocument);
            this.importedFromAnotherProject = importedFromAnotherProject;
            this.parentProject = parentProject;
        }

        #endregion

        #region Properties

        /// <summary>
        /// This returns a boolean telling you whether this particular item
        /// group was imported from another project, or whether it was defined
        /// in the main project.  For virtual item groups which have no
        /// persistence, this is false.
        /// </summary>
        public bool IsImported
        {
            get { return importedFromAnotherProject; }
        }

        /// <summary>
        /// Accessor for the condition on the item group.
        /// </summary>
        public string Condition
        {
            get
            {
                return (IsPersisted ? xml.Condition : String.Empty);
            }

            set
            {
                MustBePersisted("CannotSetCondition");
                MustNotBeImported();
                xml.Condition = value;
                MarkItemGroupAsDirty();
            }
        }

        /// <summary>
        /// Accessor for the XmlElement representing this item.  This is internal
        /// to MSBuild, and is read-only.
        /// </summary>
        internal XmlElement ItemGroupElement
        {
            get { return xml.Element; }
        }

        /// <summary>
        /// Accessor for the parent Project object.
        /// </summary>
        internal Project ParentProject
        {
            get { return this.parentProject; }
        }

        /// <summary>
        /// Setter for parent project field that makes explicit that's it's only for clearing it.
        /// </summary>
        internal void ClearParentProject()
        {
            parentProject = null;
        }

        /// <summary>
        /// Number of items in this group.
        /// </summary>
        public int Count
        {
            get { return items.Count; }
        }

        /// <summary>
        /// Gets the item at the specified index.
        /// </summary>
        public BuildItem this[int index]
        {
            get { return items[index]; }
        }

        /// <summary>
        /// Gets the actual list of items contained
        /// in this group.
        /// </summary>
        internal List<BuildItem> Items
        {
            get { return items; }
        }

        /// <summary>
        /// Accessor for the ParentCollection that the BuildPropertyGroup belongs to
        /// </summary>
        internal GroupingCollection ParentCollection
        {
            get { return parentCollection; }
            set { parentCollection = value; }
        }

        /// <summary>
        /// Accessor for the parent XML element
        /// </summary>
        internal XmlElement ParentElement
        {
            get { return xml.ParentElement; }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Copies the items in this group into a new array.
        /// NOTE: the copies are NOT clones i.e. only the references are copied
        /// </summary>
        public BuildItem[] ToArray()
        {
            return items.ToArray();
        }

        /// <summary>
        /// This IEnumerable method returns an IEnumerator object, which allows
        /// the caller to enumerate through the BuildItem objects contained in
        /// this BuildItemGroup.
        /// </summary>
        public IEnumerator GetEnumerator()
        {
            return items.GetEnumerator();
        }

        /// <summary>
        /// Import a bunch of items from another BuildItemGroup. This is an O(n) operation.
        /// </summary>
        internal void ImportItems(BuildItemGroup itemsToImport)
        {
            ErrorUtilities.VerifyThrow(itemsToImport != null, "Null BuildItemGroup passed in.");
            ErrorUtilities.VerifyThrow(itemsToImport != this, "Can't import into self.");

            EnsureCapacity(this.Count + itemsToImport.Count); // PERF: important to pre-size

            // Loop through all the Items in the given BuildItemGroup, and add them to
            // our own BuildItemGroup.
            foreach (BuildItem itemToImport in itemsToImport)
            {
                AddItem(itemToImport);
            }
        }

        /// <summary>
        /// Remove a bunch of items. This is an O(n) operation.
        /// </summary>
        internal void RemoveItems(BuildItemGroup itemsToRemove)
        {
            ErrorUtilities.VerifyThrow(itemsToRemove != null, "Null BuildItemGroup passed in.");
            ErrorUtilities.VerifyThrow(itemsToRemove != this, "Can't remove self.");

            foreach (BuildItem itemToRemove in itemsToRemove)
            {
                RemoveItem(itemToRemove);
            }
        }

        internal void RemoveItemsWithBackup(BuildItemGroup itemsToRemove)
        {
            ErrorUtilities.VerifyThrow(itemsToRemove != null, "Null BuildItemGroup passed in.");
            ErrorUtilities.VerifyThrow(itemsToRemove != this, "Can't remove self.");

            foreach (BuildItem itemToRemove in itemsToRemove)
            {
                RemoveItemWithBackup(itemToRemove);
            }
        }

        /// <summary>
        /// Applies each of the item modifications in order.
        /// Items are replaced with a virtual clone before they are modified.
        /// If an item does not exist in this group, the modification is skipped.
        /// If any modifications conflict, these modifications win.
        /// Returns the cloned item made, or null if it does not exist in this group.
        /// </summary>
        internal BuildItem ModifyItemAfterCloningUsingVirtualMetadata(BuildItem item, Dictionary<string, string> metadata)
        {
            int index = items.IndexOf(item);
            if (index > -1)
            {
                BuildItem clone = items[index].VirtualClone();
                items[index] = clone;

                foreach (KeyValuePair<string, string> pair in metadata)
                {
                    clone.SetVirtualMetadata(pair.Key, pair.Value);
                }

                return clone;
            }

            return null;
        }

        /// <summary>
        /// Applies each of the item modifications in order.
        /// Items are NOT cloned.
        /// Metadata is set as virtual metadata, so it is reset by Project.ResetBuildStatus().
        /// If an item does not exist in this group, the modification is skipped.
        /// If any modifications conflict, these modifications win.
        /// </summary>
        internal void ModifyItemsUsingVirtualMetadata(Dictionary<BuildItem, Dictionary<string, string>> modifies)
        {
            foreach (KeyValuePair<BuildItem, Dictionary<string, string>> modify in modifies)
            {
                int index = items.IndexOf(modify.Key);
                if (index > -1)
                {
                    foreach (KeyValuePair<string, string> pair in modify.Value)
                    {
                        items[index].SetVirtualMetadata(pair.Key, pair.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Pre-allocates space in the item list.
        /// PERF: Call this first before adding a known quantity of items to a group, to avoid
        /// repeated expansions of the backing list.
        /// </summary>
        internal void EnsureCapacity(int capacity)
        {
            // Don't reduce capacity here; that's a list copy, too
            if (capacity > items.Capacity)
            {
                items.Capacity = capacity;
            }
        }

        /// <summary>
        /// Adds an existing BuildItem to the list of items, does not attempt to add
        /// backing Xml for the item.
        /// </summary>
        internal void AddExistingItem(BuildItem itemToAdd)
        {
            AddExistingItemAt(items.Count, itemToAdd);
        }

        /// <summary>
        /// Adds an existing BuildItem to the list of items at the specified index.
        /// Does not attempt to add backing Xml for the item.
        /// </summary>
        internal void AddExistingItemAt(int index, BuildItem itemToAdd)
        {
            ErrorUtilities.VerifyThrow(items != null, "BuildItemGroup has not been initialized.");
            ErrorUtilities.VerifyThrow(index <= items.Count, "Index out of range");

            items.Insert(index, itemToAdd);

            if (parentProject != null)
            {
                itemToAdd.ItemDefinitionLibrary = parentProject.ItemDefinitionLibrary;
            }

            // If this BuildItemGroup is a persisted <ItemGroup>, then we need the 
            // items to have a reference back to their parent BuildItemGroup.  This
            // makes it *much* easier to delete items through the object model.
            if (IsPersisted)
            {
                itemToAdd.ParentPersistedItemGroup = this;
            }
            MarkItemGroupAsDirty();
        }

        /// <summary>
        /// Adds an BuildItem to this BuildItemGroup.  If this is a persisted BuildItemGroup, then
        /// this method also inserts the BuildItem's XML into the appropriate location
        /// in the XML document.  For persisted ItemGroups, the behavior is that 
        /// it tries to insert the new BuildItem such that it is "near" other items of the
        /// same type.  ("Near" is defined as just after the last existing item
        /// of the same type, or at the end if none is found.)
        /// </summary>
        internal void AddItem(BuildItem itemToAdd)
        {
            MustBeInitialized();

            // If we are a persisted <ItemGroup>, add the item element as a new child.
            if (IsPersisted)
            {
                MustNotBeImported();

                // Make sure the item to be added has an XML element backing it,
                // and that its XML belongs to the same XML document as our BuildItemGroup.
                ErrorUtilities.VerifyThrow(itemToAdd.IsBackedByXml, "Item does not have an XML element");
                ErrorUtilities.VerifyThrow(itemToAdd.ItemElement.OwnerDocument == xml.OwnerDocument, "Cannot add an Item with a different XML owner document.");

                // Generally, the desired behavior is to keep items of the same Type physically
                // contiguous within the BuildItemGroup.  (It's just easier to read that way.)  So we 
                // scan through the existing items in our BuildItemGroup, and try to find the spot where
                // the new item would fit in alphabetically.  This is nice because it helps 
                // source code control scenarios where multiple clients are adding items to 
                // the same list.  By putting them in alphabetical order, there's less of a 
                // chance of merge conflicts.
                int insertionIndex = items.Count;
                for (int i = 0; i < items.Count; i++)
                {
                    if ( 0 == String.Compare(itemToAdd.Name, items[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        insertionIndex = i + 1;

                        if ( 0 > String.Compare(itemToAdd.Include, items[i].Include, StringComparison.OrdinalIgnoreCase))
                        {
                            insertionIndex = i;
                            break;
                        }
                    }
                }

                // If there is at least one item in this group, then insert this new item
                // at the correct location based on our simple scan for similar item types.
                if (items.Count > 0)
                {
                    if (insertionIndex == items.Count)
                    {
                        XmlElement itemElementToInsertAfter = items[items.Count - 1].ItemElement;
                        xml.InsertAfter((XmlElement)itemElementToInsertAfter.ParentNode, itemToAdd.ItemElement, itemElementToInsertAfter);
                    }
                    else
                    {
                        XmlElement itemElementToInsertBefore = items[insertionIndex].ItemElement;
                        xml.InsertBefore((XmlElement)itemElementToInsertBefore.ParentNode, itemToAdd.ItemElement, itemElementToInsertBefore);
                    }

                    AddExistingItemAt(insertionIndex, itemToAdd);
                }
                else
                {
                    // This BuildItemGroup is currently empty.
                    // add the new BuildItem as a child of the <ItemGroup> tag.
                    xml.AppendChild(itemToAdd.ItemElement);

                    // Add the item to the end of our list.
                    AddExistingItem(itemToAdd);
                }
            }
            else
            {
                // Add the item to the end of our list.
                AddExistingItem(itemToAdd);
            }
        }

        /// <summary>
        /// Creates a new BuildItem defined by the given "Type" and "Include", and 
        /// adds it to the end of this BuildItemGroup.
        /// If the group is persisted, the item is persisted; otherwise it is virtual
        /// </summary>
        public BuildItem AddNewItem(string itemName, string itemInclude)
        {
            BuildItem newItem;

            if (IsPersisted)
            {
                // We are a persisted <ItemGroup>, so create a new persisted item object.
                newItem = new BuildItem(xml.OwnerDocument, itemName, itemInclude, parentProject.ItemDefinitionLibrary);
            }
            else
            { 
                // Create a new virtual BuildItem.
                newItem = new BuildItem(itemName, itemInclude);
            }

            AddItem(newItem);
            return newItem;
        }

        /// <summary>
        /// Adds a new item to the ItemGroup, optional treating the item Include as literal so that
        /// any special characters will be escaped before persisting it.
        /// </summary>
        public BuildItem AddNewItem(string itemName, string itemInclude, bool treatItemIncludeAsLiteral)
        {
            return AddNewItem(itemName, treatItemIncludeAsLiteral ? EscapingUtilities.Escape(itemInclude) : itemInclude);
        }

        /// <summary>
        /// Removes the given BuildItem from this BuildItemGroup.
        /// If the item is part of the project manifest (ie, it's declared outside of a target) then
        /// makes a backup of persisted items so that later the item group can be reverted to that backup,
        /// reversing this change.
        /// </summary>
        internal void RemoveItemWithBackup(BuildItem itemToRemove)
        {
            MustBeInitialized();

            if (itemToRemove.IsPartOfProjectManifest)
            {
                // We're about to remove an item that's part of the project manifest;
                // this must be reverted when we reset the project, so make sure we've got a backup
                BackupPersistedItems();
            }

            // Don't remove the XML node, or mark the itemgroup as dirty; this is 
            // strictly an operation on temporary items, because we'll be un-backing up the
            // persisted items at the end of the build

            items.Remove(itemToRemove);
        }

        /// <summary>
        /// Removes the given BuildItem from this BuildItemGroup.
        /// If item is not in this group, does nothing.
        /// </summary>
        public void RemoveItem(BuildItem itemToRemove)
        {
            MustBeInitialized();
            RemoveItemElement(itemToRemove);
            items.Remove(itemToRemove);
            MarkItemGroupAsDirty();
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If index is out of bounds</exception>
        public void RemoveItemAt(int index)
        {
            MustBeInitialized();
            BuildItem item = items[index];
            RemoveItemElement(item);
            items.RemoveAt(index);
            MarkItemGroupAsDirty();
        }

        /// <summary>
        /// If this is a persisted group, removes the XML element corresponding to the given item.
        /// If this is not a persisted group, does nothing.
        /// </summary>
        private void RemoveItemElement(BuildItem item)
        {
            if (IsPersisted)
            {
                MustNotBeImported();
                MustHaveThisParentElement(item);
                MustHaveThisParentGroup(item);
                xml.Element.RemoveChild(item.ItemElement);
                item.ParentPersistedItemGroup = null;
            }
        }

        /// <summary>
        /// Clones the BuildItemGroup.  A shallow clone here is one that references
        /// the same BuildItem objects as the original, whereas a deep clone actually
        /// clones the BuildItem objects as well.  If this is a persisted BuildItemGroup, 
        /// only deep clones are allowed, because you can't have the same XML 
        /// element belonging to two parents.
        /// </summary>
        public BuildItemGroup Clone(bool deepClone)
        {
            BuildItemGroup clone;

            if (IsPersisted)
            {
                // Only deep clones are permitted when dealing with a persisted <ItemGroup>.
                // This is because a shallow clone would attempt to add the same item
                // elements to two different parent <ItemGroup> elements, and this is
                // not allowed.
                ErrorUtilities.VerifyThrowInvalidOperation(deepClone, "ShallowCloneNotAllowed");

                // Do not set the ParentProject on the cloned BuildItemGroup, because it isn't really
                // part of the project.
                clone = new BuildItemGroup(xml.OwnerDocument, importedFromAnotherProject, null);
                clone.Condition = Condition;
            }
            else
            {
                clone = new BuildItemGroup();
            }

            // Loop through every BuildItem in our collection, and add those same Items
            // to the cloned collection.

            clone.EnsureCapacity(this.Count); // PERF: important to pre-size

            foreach (BuildItem item in this)
            {
                // If the caller requested a deep clone, then clone the BuildItem object,
                // and add the new BuildItem to the new BuildItemGroup.  Otherwise, just add
                // a reference to the existing BuildItem object to the new BuildItemGroup.
                clone.AddItem(deepClone ? item.Clone() : item);
            }

            return clone;
        }

        /// <summary>
        /// Does a shallow clone, creating a new group with pointers to the same items as the old group.
        /// </summary>
        internal BuildItemGroup ShallowClone()
        {
            return Clone(false /* shallow */);
        }

        /// <summary>
        /// Removes all Items from this BuildItemGroup, and also deletes the Condition
        /// and Name.
        /// </summary>
        public void Clear()
        {
            MustBeInitialized();

            if (IsPersisted)
            {
                MustNotBeImported();

                foreach(BuildItem itemToRemove in items)
                {
                    XmlElement itemElement = itemToRemove.ItemElement;
                    MustHaveThisParentElement(itemToRemove);

                    itemElement.ParentNode.RemoveChild(itemElement);
                    itemToRemove.ParentPersistedItemGroup = null;
                }

                xml.Condition = null;
            }

            items.Clear();
            MarkItemGroupAsDirty();
        }

        /// <summary>
        /// Removes all virtual (intermediate) items from this BuildItemGroup.  This
        /// is used to reset the state of the build back to the initial state,
        /// when we only knew about the items that were actually declared in the
        /// project XML.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void RemoveAllIntermediateItems()
        {
            MustBeInitialized();
            MustBeVirtual("InvalidInPersistedItemGroup");

            if (IsBackedUp)
            {
                // Revert removes of persisted items
                items = persistedItemBackup;
                persistedItemBackup = null;
            }
            else
            {
                // Delete all virtual (those without XML backing) items.
                List<BuildItem> itemsToKeep = new List<BuildItem>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].IsPartOfProjectManifest)
                    {
                        itemsToKeep.Add(items[i]);
                    }
                }
                items = itemsToKeep;
            }

            // Revert changes to persisted items' metadata
            for (int i = 0; i < items.Count; i++)
            {
                items[i].RevertToPersistedMetadata();
            }

            MarkItemGroupAsDirty();
        }

        /// <summary>
        /// Marks the parent project as dirty.
        /// </summary>
        private void MarkItemGroupAsDirty()
        {
            if (parentProject != null)
            {
                parentProject.MarkProjectAsDirty();
            }
        }

        /// <summary>
        /// Create a secret backup list of our persisted items only.
        /// Then, we can revert back to this later when we're done with the build,
        /// and we want to remove any virtual items and revert any removes of
        /// persisted items.
        /// </summary>
        internal void BackupPersistedItems()
        {
            if (!IsBackedUp)
            {
                persistedItemBackup = new List<BuildItem>();

                foreach (BuildItem item in items)
                {
                    if (item.IsPartOfProjectManifest)
                    {
                        BuildItem itemClone = item.Clone();
                        persistedItemBackup.Add(itemClone);
                    }
                }
            }
        }

        /// <summary>
        /// Call this method to verify that this item group is a well-formed
        /// virtual item group.
        /// </summary>
        private void MustBeVirtual(string errorResourceName)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(!IsPersisted, errorResourceName);
        }

        /// <summary>
        /// Returns whether this is a persisted group.
        /// </summary>
        internal bool IsPersisted
        {
            get { return isPersisted; }
        }

        /// <summary>
        /// Returns whether the persisted items have been backed up for later
        /// recovery.
        /// </summary>
        internal bool IsBackedUp
        {
            get { return (persistedItemBackup != null); }
        }

        /// <summary>
        /// Verifies this is a persisted group.
        /// </summary>
        private void MustBePersisted(string errorResourceName)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(IsPersisted, errorResourceName);
        }

        /// <summary>
        /// Verifies this is not an imported item group.
        /// </summary>
        private void MustNotBeImported()
        {
            ErrorUtilities.VerifyThrowInvalidOperation(!importedFromAnotherProject, "CannotModifyImportedProjects");
        }

        /// <summary>
        /// Verifies that the list of items has been created.
        /// </summary>
        private void MustBeInitialized()
        {
            ErrorUtilities.VerifyThrow(this.items != null, "BuildItemGroup has not been initialized.");
        }

        /// <summary>
        /// Verifies that the item's parent element is indeed this item group's element.
        /// </summary>
        private void MustHaveThisParentElement(BuildItem item)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(item != null && item.ItemElement != null && item.ItemElement.ParentNode == xml.Element, "ItemDoesNotBelongToItemGroup");
        }

        /// <summary>
        /// Verifies the parent item group is indeed this item group.
        /// </summary>
        /// <param name="item"></param>
        private void MustHaveThisParentGroup(BuildItem item)
        {
            ErrorUtilities.VerifyThrow(item.ParentPersistedItemGroup == this, "This item doesn't belong to this itemgroup");
        }

        /// <summary>
        /// Evaluates an item group that's *outside* of a Target.
        /// Metadata is not allowed on conditions, and we against the parent project.
        /// </summary>
        internal void Evaluate
        (
            BuildPropertyGroup existingProperties,
            Hashtable existingItemsByName,
            bool collectItemsIgnoringCondition, 
            bool collectItemsRespectingCondition, 
            ProcessingPass pass
        )
        {
            ErrorUtilities.VerifyThrow(pass == ProcessingPass.Pass2, "Pass should be Pass2 for ItemGroups.");
            ErrorUtilities.VerifyThrow(collectItemsIgnoringCondition || collectItemsRespectingCondition, "collectItemsIgnoringCondition and collectItemsRespectingCondition can't both be false.");

            Expander expander = new Expander(existingProperties, existingItemsByName, ExpanderOptions.ExpandAll);

            bool itemGroupCondition = Utilities.EvaluateCondition(Condition,
                                                         (IsPersisted ? xml.ConditionAttribute : null),
                                                         expander,
                                                         ParserOptions.AllowPropertiesAndItemLists,
                                                         parentProject);

            if (!itemGroupCondition && !collectItemsIgnoringCondition)
            {
                // Neither list needs updating
                return;
            }

            foreach (BuildItem currentItem in this)
            {
                bool itemCondition = Utilities.EvaluateCondition(currentItem.Condition,
                                                             currentItem.ConditionAttribute,
                                                             expander,
                                                             ParserOptions.AllowPropertiesAndItemLists,
                                                             parentProject);

                if (!itemCondition && !collectItemsIgnoringCondition)
                {
                    // Neither list needs updating
                    continue;
                }

                if (collectItemsIgnoringCondition)
                {
                    // Since we're re-evaluating the project, clear out the previous list of child items
                    // for each persisted item tag.
                    currentItem.ChildItems.Clear();
                }

                currentItem.EvaluateAllItemMetadata(expander, ParserOptions.AllowPropertiesAndItemLists, parentProject.ParentEngine.LoggingServices, parentProject.ProjectBuildEventContext);
                BuildItemGroup items = BuildItemGroup.ExpandItemIntoItems(parentProject.ProjectDirectory, currentItem, expander, false /* do not expand metadata */);

                foreach (BuildItem item in items)
                {
                    BuildItem newItem = BuildItem.CreateClonedParentedItem(item, currentItem);

                    if (itemGroupCondition && itemCondition && collectItemsRespectingCondition)
                    {
                        parentProject.AddToItemListByName(newItem);
                    }

                    if (collectItemsIgnoringCondition)
                    {
                        parentProject.AddToItemListByNameIgnoringCondition(newItem);

                        // Set up the other half of the parent/child relationship.
                        newItem.ParentPersistedItem.ChildItems.AddItem(newItem);
                    }
                }
            }
        }

        /// <summary>
        /// Processes the "include" list and the "exclude" list for an item element, and returns
        /// the resultant virtual group of items. Ignores any condition on the item.
        /// </summary>
        /// <param name="baseDirectory">Where relative paths should be evaluated from</param>
        /// <param name="originalItem">The "mother" item that's being expanded</param>
        /// <param name="expander">Expander to evaluated items and properties</param>
        /// <param name="expandMetadata">Whether metadata expressions should be expanded, or left as literals</param>
        internal static BuildItemGroup ExpandItemIntoItems
        (
            string baseDirectory,
            BuildItem originalItem,
            Expander expander,
            bool expandMetadata
        )
        {
            ErrorUtilities.VerifyThrow(originalItem != null, "Can't add a null item to project.");
            ErrorUtilities.VerifyThrow(expander != null, "expander can't be null.");

            // Take the entire string specified in the "Include" attribute, and split
            // it up by semi-colons.  Then loop over the individual pieces.
            // Expand only with properties first, so that expressions like Include="@(foo)" will transfer the metadata of the "foo" items as well, not just their item specs.
            List<string> itemIncludePieces = (new Expander(expander, ExpanderOptions.ExpandProperties).ExpandAllIntoStringListLeaveEscaped(originalItem.Include, originalItem.IncludeAttribute));
            BuildItemGroup itemsToInclude = new BuildItemGroup();
            for (int i = 0; i < itemIncludePieces.Count; i++)
            {
                BuildItemGroup itemizedGroup = expander.ExpandSingleItemListExpressionIntoItemsLeaveEscaped(itemIncludePieces[i], originalItem.IncludeAttribute);
                if (itemizedGroup == null)
                {
                    // The expression did not represent a single @(...) item list reference. 
                    if (expandMetadata)
                    {
                        // We're inside a target: metadata expressions like %(foo) are legal, so expand them now
                        itemIncludePieces[i] = expander.ExpandMetadataLeaveEscaped(itemIncludePieces[i]);
                    }
                    // Now it's a string constant, possibly with wildcards.
                    // Take each individual path or file expression, and expand any
                    // wildcards.  Then loop through each file returned.
                    if (itemIncludePieces[i].Length > 0)
                    {
                        string[] includeFileList = EngineFileUtilities.GetFileListEscaped(baseDirectory, itemIncludePieces[i]);
                        for (int j = 0; j < includeFileList.Length; j++)
                        {
                            BuildItem newItem = itemsToInclude.AddNewItem(originalItem.Name, originalItem.Include);
                            newItem.SetEvaluatedItemSpecEscaped(itemIncludePieces[i]);   // comes from XML include --- "arbitrarily escaped"
                            newItem.SetFinalItemSpecEscaped(includeFileList[j]);  // comes from file system matcher -- "canonically escaped"
                        }
                    }
                }
                else
                {
                    itemsToInclude.ImportItems(itemizedGroup);
                }
            }

            List<BuildItem> matchingItems = FindItemsMatchingSpecification(itemsToInclude, originalItem.Exclude, originalItem.ExcludeAttribute, expander, baseDirectory);

            if (matchingItems != null)
            {
                foreach (BuildItem item in matchingItems)
                {
                    itemsToInclude.RemoveItem(item);
                }
            }
            
            return itemsToInclude;
        }

        /// <summary>
        /// Returns a list of all items in the provided item group whose itemspecs match the specification, after it is split and any wildcards are expanded.
        /// If not items match, returns null.
        /// </summary>
        internal static List<BuildItem> FindItemsMatchingSpecification(BuildItemGroup items, string specification, XmlAttribute attribute, Expander expander, string baseDirectory)
        {
            if (items.Count == 0 || specification.Length == 0)
            {
                return null;
            }

            // This is a hashtable whose key is the filename for the individual items
            // in the Exclude list, after wildcard expansion.  The value in the hash table
            // is just an empty string.
            Hashtable specificationsToFind = new Hashtable(StringComparer.OrdinalIgnoreCase);

            // Split by semicolons
            List<string> specificationPieces = expander.ExpandAllIntoStringListLeaveEscaped(specification, attribute);

            foreach (string piece in specificationPieces)
            {
                // Take each individual path or file expression, and expand any
                // wildcards.  Then loop through each file returned, and add it
                // to our hashtable.

                // Don't unescape wildcards just yet - if there were any escaped, the caller wants to treat them
                // as literals. Everything else is safe to unescape at this point, since we're only matching
                // against the file system.
                string[] fileList = EngineFileUtilities.GetFileListEscaped(baseDirectory, piece);

                foreach (string file in fileList)
                {
                    // Now unescape everything, because this is the end of the road for this filename.
                    // We're just going to compare it to the unescaped include path to filter out the
                    // file excludes.
                    specificationsToFind[EscapingUtilities.UnescapeAll(file)] = String.Empty;
                }
            }

            if (specificationsToFind.Count == 0)
            {
                return null;
            }

            // Now loop through our list and filter out any that match a
            // filename in the remove list.
            List<BuildItem> itemsRemoved = new List<BuildItem>();

            foreach (BuildItem item in items)
            {
                // Even if the case for the excluded files is different, they
                // will still get excluded, as expected.  However, if the excluded path
                // references the same file in a different way, such as by relative
                // path instead of absolute path, we will not realize that they refer
                // to the same file, and thus we will not exclude it.
                if (specificationsToFind.ContainsKey(item.FinalItemSpec))
                {
                    itemsRemoved.Add(item);
                }
            }

            return itemsRemoved;
        }

        #endregion
    }
}
