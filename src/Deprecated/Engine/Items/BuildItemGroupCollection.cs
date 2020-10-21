// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a collection of persisted &lt;ItemGroup&gt;'s.  Each
    /// MSBuild project has exactly one BuildItemGroupCollection, which includes
    /// all the imported ItemGroups as well as the ones in the main project file.
    /// 
    /// The implementation of this class is that it's basically a Facade.  It just
    /// calls into the GroupingCollection within the Project to do it's work.  It
    /// doesn't maintain any BuildPropertyGroup state on its own.
    /// </summary>
    /// <owner>DavidLe</owner>
    public class BuildItemGroupCollection : IEnumerable, ICollection
    {
        #region Member Data

        private GroupingCollection groupingCollection;
        #endregion

        #region Constructors

        /// <summary>
        /// Private default constructor.  This object can't be instantiated by
        /// OM consumers.
        /// </summary>
        /// <owner>DavidLe, RGoel</owner>
        private BuildItemGroupCollection
            (
            )
        {
        }

        /// <summary>
        /// Constructor that takes the GroupingCollection that this sits over.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="groupingCollection"></param>
        internal BuildItemGroupCollection
            (
            GroupingCollection groupingCollection
            )
        {
            error.VerifyThrow(groupingCollection != null, "GroupingCollection is null!");

            this.groupingCollection = groupingCollection;
        }
        #endregion

        #region Properties

        /// <summary>
        /// Read-only property which returns the number of ItemGroups contained
        /// in our collection.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        public int Count
        {
            get
            {
                return this.groupingCollection.ItemGroupCount;
            }
        }

        /// <summary>
        /// This ICollection property tells whether this object is thread-safe.
        /// </summary>
        /// <owner>DavidLe</owner>
        public bool IsSynchronized
        {
            get
            {
                return this.groupingCollection.IsSynchronized;
            }
        }

        /// <summary>
        /// This ICollection property returns the object to be used to synchronize
        /// access to the class.
        /// </summary>
        /// <owner>DavidLe</owner>
        public object SyncRoot
        {
            get
            {
                return this.groupingCollection.SyncRoot;
            }
        }

        /// <summary>
        /// This looks through all the local item groups (those in the main
        /// project file, as opposed to any imported project files).  It returns
        /// the last one that comes before any imported item groups.  This
        /// is the heuristic we use to determine where to add new item groups
        /// into the project file.
        /// </summary>
        /// <owner>DavidLe</owner>
        internal BuildItemGroup LastLocalItemGroup
        {
            get
            {
                BuildItemGroup lastLocalItemGroup = null;

                foreach (BuildItemGroup itemGroup in this.groupingCollection.ItemGroupsTopLevel)
                {
                    if (itemGroup.IsImported)
                    {
                        // As soon as we hit an imported BuildItemGroup, we want to 
                        // completely bail out.  The goal of this function is 
                        // to return the last itemGroup that is *before* any
                        // imported itemGroups.
                        break;
                    }
                    else
                    {
                        lastLocalItemGroup = itemGroup;
                    }
                }

                return lastLocalItemGroup;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This ICollection method copies the contents of this collection to an 
        /// array.
        /// </summary>
        /// <owner>DavidLe</owner>
        /// <param name="array"></param>
        /// <param name="index"></param>
        public void CopyTo
        (
            Array array,
            int index
        )
        {
            this.groupingCollection.ItemCopyTo(array, index);
        }

        /// <summary>
        /// This IEnumerable method returns an IEnumerator object, which allows
        /// the caller to enumerate through the BuildItemGroup objects contained in
        /// this BuildItemGroupCollection.
        /// </summary>
        /// <owner>DavidLe</owner>
        public IEnumerator GetEnumerator
            (
            )
        {
            return this.groupingCollection.GetItemEnumerator();
        }

        /// <summary>
        /// Adds a new BuildItemGroup to our collection, at the specified insertion
        /// point.  This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <owner>DavidLe</owner>
        /// <param name="insertionPoint"></param>
        /// <param name="newItemGroup"></param>
        internal void InsertAfter
        (
            BuildItemGroup newItemGroup,
            BuildItemGroup insertionPoint
        )
        {
            this.groupingCollection.InsertAfter(newItemGroup, insertionPoint);
        }

        /// <summary>
        /// Adds a new BuildItemGroup as the last element of our collection.
        /// This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <owner>DavidLe</owner>
        /// <param name="newItemGroup"></param>
        internal void InsertAtEnd
        (
            BuildItemGroup newItemGroup
        )
        {
            this.groupingCollection.InsertAtEnd(newItemGroup);
        }

        /// <summary>
        /// Removes a BuildItemGroup from our collection.  This method does nothing
        /// to manipulate the project's XML content.
        /// </summary>
        /// <owner>DavidLe</owner>
        /// <param name="itemGroup"></param>
        internal void RemoveItemGroup
        (
            BuildItemGroup itemGroup
        )
        {
            this.groupingCollection.RemoveItemGroup(itemGroup);
        }

        #endregion
    }
}
