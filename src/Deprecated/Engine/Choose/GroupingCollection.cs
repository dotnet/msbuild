// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Data;
using System.Xml;
using System.Collections;
using System.Globalization;

using Microsoft.Build.BuildEngine.Shared;


using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Collection that contains all the Choose, BuildPropertyGroup and BuildItemGroup blocks
    /// in a Project.
    /// </summary>
    internal class GroupingCollection : IEnumerable
    {
        #region Member Data

        // This is the list of groups contained in this collection.
        private ArrayList combinedGroupList = null;
        private int propertyGroupCount = 0;
        private int itemGroupCount = 0;
        private int chooseCount = 0;
        private GroupingCollection parentGroupingCollection;

        #endregion

        #region Constructors

        /// <summary>
        /// GroupingCollection constructor.  Basically just initializes internal
        /// data structures.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <param name="parentGroupingCollection">The parent collection of this grouping collection, null for the master collection</param>
        /// <owner>DavidLe</owner>
        /// <returns>IEnumerator</returns>
        internal GroupingCollection
            (
            GroupingCollection parentGroupingCollection
            )
        {
            this.combinedGroupList = new ArrayList();
            this.parentGroupingCollection = parentGroupingCollection;
        }

        #endregion

        #region PropertyGroupCollection ICollection support

        /// <summary>
        /// Encapsulates updating the property group count for this and any parent grouping collections.
        /// </summary>
        /// <param name="delta"></param>
        /// <owner>LukaszG</owner>
        internal void ChangePropertyGroupCount(int delta)
        {
            this.propertyGroupCount += delta;
            ErrorUtilities.VerifyThrow(this.propertyGroupCount >= 0, "The property group count should never be negative");

            if (parentGroupingCollection != null)
            {
                parentGroupingCollection.ChangePropertyGroupCount(delta);
            }
        }

        /// <summary>
        /// Read-only property returns the number of PropertyGroups stored within the collection.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        internal int PropertyGroupCount
        {
            get
            {
                return this.propertyGroupCount;
            }
        }
        
        /// <summary>
        /// </summary>
        internal object SyncRoot
        {
            get
            {
                return this.combinedGroupList.SyncRoot;
            }
        }

        /// <summary>
        /// </summary>
        internal bool IsSynchronized
        {
            get
            {
                return this.combinedGroupList.IsSynchronized;
            }
        }
        /// <summary>
        /// This ICollection method copies the contents of this collection to an 
        /// array.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="array"></param>
        /// <param name="index"></param>
        internal void PropertyCopyTo
        (
            Array array,
            int index
        )
        {
            foreach (BuildPropertyGroup propertyGroup in this.PropertyGroupsAll)
            {
                array.SetValue(propertyGroup, index++);
            }
        }

        /// <summary>
        /// This method returns an IEnumerator object, which allows
        /// the caller to enumerate through the BuildPropertyGroup objects
        /// contained in this GroupCollection.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <returns>IEnumerator</returns>
        internal IEnumerator GetPropertyEnumerator
            (
            )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");
            return new GroupEnumeratorHelper(this, GroupEnumeratorHelper.ListType.PropertyGroupsAll).GetEnumerator();
        }

        #endregion

        #region ItemGroupCollection ICollection support

        /// <summary>
        /// Encapsulates updating the item group count for this and any parent grouping collections.
        /// </summary>
        /// <param name="delta"></param>
        /// <owner>LukaszG</owner>
        internal void ChangeItemGroupCount(int delta)
        {
            this.itemGroupCount += delta;
            ErrorUtilities.VerifyThrow(this.itemGroupCount >= 0, "The item group count should never be negative");

            if (parentGroupingCollection != null)
            {
                parentGroupingCollection.ChangeItemGroupCount(delta);
            }
        }

        /// <summary>
        /// Read-only property returns the number of ItemGroups stored within the collection.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        internal int ItemGroupCount
        {
            get
            {
                return this.itemGroupCount;
            }
        }

        /// <summary>
        /// This ICollection method copies the contents of this collection to an 
        /// array.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="array"></param>
        /// <param name="index"></param>
        /// <returns>IEnumerator</returns>
        internal void ItemCopyTo
        (
            Array array,
            int index
        )
        {
            foreach (BuildItemGroup itemGroup in this.ItemGroupsAll)
            {
                array.SetValue(itemGroup, index++);
            }
        }

        /// <summary>
        /// This method returns an IEnumerator object, which allows
        /// the caller to enumerate through the BuildItemGroup objects
        /// contained in this GroupCollection.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <returns>IEnumerator</returns>
        internal IEnumerator GetItemEnumerator
            (
            )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");
            return new GroupEnumeratorHelper(this, GroupEnumeratorHelper.ListType.ItemGroupsAll).GetEnumerator();
        }

        #endregion

        #region Various enumerators for selecting different groups from this collection

        internal GroupEnumeratorHelper PropertyGroupsTopLevelAndChooses
        {
            get
            {
                return new GroupEnumeratorHelper(this, GroupEnumeratorHelper.ListType.PropertyGroupsTopLevelAndChoose);
            }
        }

        internal GroupEnumeratorHelper ItemGroupsTopLevelAndChooses
        {
            get
            {
                return new GroupEnumeratorHelper(this, GroupEnumeratorHelper.ListType.ItemGroupsTopLevelAndChoose);
            }
        }

        internal GroupEnumeratorHelper PropertyGroupsTopLevel
        {
            get
            {
                return new GroupEnumeratorHelper(this, GroupEnumeratorHelper.ListType.PropertyGroupsTopLevel);
            }
        }

        internal GroupEnumeratorHelper ItemGroupsTopLevel
        {
            get
            {
                return new GroupEnumeratorHelper(this, GroupEnumeratorHelper.ListType.ItemGroupsTopLevel);
            }
        }

        internal GroupEnumeratorHelper PropertyGroupsAll
        {
            get
            {
                return new GroupEnumeratorHelper(this, GroupEnumeratorHelper.ListType.PropertyGroupsAll);
            }
        }

        internal GroupEnumeratorHelper ItemGroupsAll
        {
            get
            {
                return new GroupEnumeratorHelper(this, GroupEnumeratorHelper.ListType.ItemGroupsAll);
            }
        }

        internal GroupEnumeratorHelper ChoosesTopLevel
        {
            get
            {
                return new GroupEnumeratorHelper(this, GroupEnumeratorHelper.ListType.ChoosesTopLevel);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This IEnumerable method returns an IEnumerator object, which allows
        /// the caller to enumerate through the Grouping objects (Choose, BuildPropertyGroup,
        /// and BuildItemGroup) contained in this BuildItemGroupCollection.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <returns>IEnumerator</returns>
        public IEnumerator GetEnumerator
            (
            )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");

            return combinedGroupList.GetEnumerator();
        }

        /// <summary>
        /// Adds a new BuildPropertyGroup as the first element of our collection.
        /// This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="newPropertyGroup"></param>
        internal void InsertAtBeginning
        (
            BuildPropertyGroup newPropertyGroup
        )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");

            this.combinedGroupList.Insert(0, newPropertyGroup);
            newPropertyGroup.ParentCollection = this;
            ChangePropertyGroupCount(1);
        }

        /// <summary>
        /// Adds a new BuildPropertyGroup to our collection, at the specified insertion
        /// point.  This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="newPropertyGroup"></param>
        /// <param name="insertionPoint"></param>
        internal void InsertAfter
        (
            BuildPropertyGroup newPropertyGroup,
            BuildPropertyGroup insertionPoint
        )
        {
            InsertAfter((IItemPropertyGrouping)newPropertyGroup, (IItemPropertyGrouping)insertionPoint);
        }

        /// <summary>
        /// Adds a new BuildPropertyGroup as the last element of our collection.
        /// This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="newPropertyGroup"></param>
        internal void InsertAtEnd
        (
            BuildPropertyGroup newPropertyGroup
        )
        {
            InsertAtEnd((IItemPropertyGrouping)newPropertyGroup);
        }

        /// <summary>
        /// Removes a BuildPropertyGroup from our collection.  This method does nothing
        /// to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="propertyGroup"></param>
        internal void RemovePropertyGroup
        (
            BuildPropertyGroup propertyGroup
        )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");

            this.combinedGroupList.Remove(propertyGroup);
            ChangePropertyGroupCount(-1);
            propertyGroup.ParentCollection = null;
            error.VerifyThrow(this.propertyGroupCount >= 0, "Too many calls to RemovePropertyGroup().");
        }

        /// <summary>
        /// Adds a new BuildItemGroup to our collection, at the specified insertion
        /// point.  This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="newItemGroup"></param>
        /// <param name="insertionPoint"></param>
        internal void InsertAfter
        (
            BuildItemGroup newItemGroup,
            BuildItemGroup insertionPoint
        )
        {
            InsertAfter((IItemPropertyGrouping)newItemGroup, (IItemPropertyGrouping)insertionPoint);
        }

        /// <summary>
        /// Adds a new BuildItemGroup as the last element of our collection.
        /// This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="newItemGroup"></param>
        internal void InsertAtEnd
        (
            BuildItemGroup newItemGroup
        )
        {
            InsertAtEnd((IItemPropertyGrouping)newItemGroup);
        }

        /// <summary>
        /// Removes a BuildItemGroup from our collection.  This method does nothing
        /// to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="itemGroupToRemove"></param>
        internal void RemoveItemGroup
        (
            BuildItemGroup itemGroupToRemove
        )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");

            this.combinedGroupList.Remove(itemGroupToRemove);
            itemGroupToRemove.ParentCollection = null;

            ChangeItemGroupCount(-1);
            error.VerifyThrow(this.itemGroupCount >= 0, "Too many calls to RemoveItemGroup().");

        }

        /// <summary>
        /// Inserts a new item group after the specified insertion point.  This method
        /// does nothing to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="newGroup"></param>
        /// <param name="insertionPoint"></param>
        internal void InsertAfter
        (
            IItemPropertyGrouping newGroup,
            IItemPropertyGrouping insertionPoint
        )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");

            this.combinedGroupList.Insert(this.combinedGroupList.IndexOf(insertionPoint) + 1,
                newGroup);
            if (newGroup is BuildItemGroup)
            {
                ((BuildItemGroup)newGroup).ParentCollection = this;
                ChangeItemGroupCount(1);
            }
            else if (newGroup is BuildPropertyGroup)
            {
                ((BuildPropertyGroup)newGroup).ParentCollection = this;
                ChangePropertyGroupCount(1);
            }
            else if (newGroup is Choose)
                this.chooseCount++;
        }

        /// <summary>
        /// Inserts a new BuildItemGroup at the end of the list of ItemGroups.  This method
        /// does nothing to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="newGroup"></param>
        internal void InsertAtEnd
        (
            IItemPropertyGrouping newGroup
        )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");

            this.combinedGroupList.Add(newGroup);
            if (newGroup is BuildItemGroup)
            {
                ((BuildItemGroup)newGroup).ParentCollection = this;
                ChangeItemGroupCount(1);
            }
            else if (newGroup is BuildPropertyGroup)
            {
                ((BuildPropertyGroup)newGroup).ParentCollection = this;
                ChangePropertyGroupCount(1);
            }
            else if (newGroup is Choose)
                this.chooseCount++;
        }

        /// <summary>
        /// Removes a Choose block from the list.  This method does nothing to manipulate
        /// the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="choose"></param>
        internal void RemoveChoose
        (
            Choose choose
        )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");

            this.combinedGroupList.Remove(choose);
            this.chooseCount--;
            error.VerifyThrow(this.chooseCount >= 0, "Too many calls to RemoveChoose().");
        }

        /// <summary>
        /// Removes all ItemGroups, PropertyGroups and Choose's from our
        /// collection.  This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        internal void Clear
            (
            )
        {
            error.VerifyThrow(this.combinedGroupList != null, "Arraylist not initialized!");

            this.combinedGroupList.Clear();
            ChangeItemGroupCount(-this.itemGroupCount); // set to 0 and update parent collections
            ChangePropertyGroupCount(-this.propertyGroupCount); // set to 0 and update parent collections
            chooseCount = 0;
        }

        /// <summary>
        /// Removes all PropertyGroups from the collection.  Recurses into Chooses
        /// in the collection removing PropertyGroups as well.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        internal void RemoveAllPropertyGroups()
        {
            // Need to copy the collection to an array, because we're not allowed
            // to modify a collection while it's being enumerated.
            ArrayList localPropertyGroups = new ArrayList(this.propertyGroupCount);

            foreach (BuildPropertyGroup propertyGroup in this.PropertyGroupsAll)
            {
                if (!propertyGroup.IsImported)
                {
                    localPropertyGroups.Add(propertyGroup);
                }
            }

            foreach (BuildPropertyGroup propertyGroupToRemove in localPropertyGroups)
            {
                propertyGroupToRemove.ParentProject.RemovePropertyGroup(propertyGroupToRemove);
            }
        }

        /// <summary>
        /// Removes all PropertyGroups with a given condtion from the collection.
        /// Recurses into Chooses in the collection removing PropertyGroups as well.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        internal void RemoveAllPropertyGroupsByCondition(string condition, bool includeImportedPropertyGroups)
        {
            ArrayList propertiesToRemove = new ArrayList();
            foreach (BuildPropertyGroup propertyGroup in this.PropertyGroupsAll)
            {
                if (0 == String.Compare(condition.Trim(), propertyGroup.Condition.Trim(), StringComparison.OrdinalIgnoreCase) 
                    && (!propertyGroup.IsImported || includeImportedPropertyGroups))
                {
                    propertiesToRemove.Add(propertyGroup);
                }
            }
            foreach (BuildPropertyGroup propertyGroup in propertiesToRemove)
            {
                if (propertyGroup.IsImported)
                {
                    propertyGroup.ParentProject.RemoveImportedPropertyGroup(propertyGroup);
                }
                else
                {
                    propertyGroup.ParentProject.RemovePropertyGroup(propertyGroup);
                }
            }
        }

        /// <summary>
        /// Removes all ItemGroups from the collection.
        /// Recurses into Chooses in the collection removing ItemGroups as well.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        internal void RemoveAllItemGroups()
        {
            // Need to copy the collection to an array, because we're not allowed
            // to modify a collection while it's being enumerated.
            ArrayList localItemGroups = new ArrayList(this.itemGroupCount);

            foreach (BuildItemGroup itemGroup in ItemGroupsAll)
            {
                if (!itemGroup.IsImported)
                {
                    localItemGroups.Add(itemGroup);
                }
            }

            foreach (BuildItemGroup itemGroupToRemove in localItemGroups)
            {
                itemGroupToRemove.ParentProject.RemoveItemGroup(itemGroupToRemove);
            }
        }

        /// <summary>
        /// Removes all ItemGroups with a given condtion from the collection.
        /// Recurses into Chooses in the collection removing ItemGroups as well.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="condition"></param>
        internal void RemoveAllItemGroupsByCondition(string condition)
        {
            ArrayList itemsToRemove = new ArrayList(this.itemGroupCount);

            foreach (BuildItemGroup itemGroup in this.ItemGroupsAll)
            {
                if (0 == String.Compare(condition.Trim(), itemGroup.Condition.Trim(), StringComparison.OrdinalIgnoreCase) 
                    && !itemGroup.IsImported)
                {
                    itemsToRemove.Add(itemGroup);
                }
            }
            foreach (BuildItemGroup itemGroup in itemsToRemove)
            {
                itemGroup.ParentProject.RemoveItemGroup(itemGroup);
            }
        }

        /// <summary>
        /// Removes all Items of a given typoe from the collection.
        /// Recurses into Chooses in the collection removing Items as well.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="itemName"></param>
        internal void RemoveItemsByName(string itemName)
        {
            BuildItemGroup itemsToRemove = new BuildItemGroup();

            foreach (BuildItemGroup itemGroup in this.ItemGroupsAll)
            {
                // Now loop through the Items in the BuildItemGroup, and keep track of the
                // ones that are of the requested item type.
                foreach (BuildItem item in itemGroup)
                {
                    if ((0 == String.Compare(item.Name, itemName, StringComparison.OrdinalIgnoreCase)) &&
                            !item.IsImported
                        )
                    {
                        // We're not allowed to remove an item from a collection while
                        // the collection is being enumerated.  So we have to track it
                        // in a separate list.
                        itemsToRemove.AddItem(item);
                    }
                }
            }
            foreach (BuildItem itemToRemove in itemsToRemove)
            {
                itemToRemove.ParentPersistedItemGroup.ParentProject.RemoveItem(itemToRemove);
            }
        }

        #endregion
    }
}
