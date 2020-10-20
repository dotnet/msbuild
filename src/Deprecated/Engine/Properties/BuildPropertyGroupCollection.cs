// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a collection of persisted &lt;PropertyGroup&gt;'s.  Each
    /// MSBuild project has exactly one BuildPropertyGroupCollection, which includes
    /// all the imported PropertyGroups as well as the ones in the main project file.
    /// 
    /// The implementation of this class is that it's basically a Facade.  It just
    /// calls into the GroupingCollection within the Project to do it's work.  It
    /// doesn't maintain any BuildPropertyGroup state on its own.
    /// </summary>
    /// <owner>DavidLe</owner>
    public class BuildPropertyGroupCollection : ICollection, IEnumerable
    {
        #region Member Data

        private GroupingCollection groupingCollection = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Private default constructor.  This object can't be instantiated by
        /// OM consumers.
        /// </summary>
        /// <owner>DavidLe, RGoel</owner>
        private BuildPropertyGroupCollection
            (
            )
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="groupingCollection"></param>
        /// <owner>rgoel</owner>
        internal BuildPropertyGroupCollection
            (
            GroupingCollection groupingCollection
            )
        {
            this.groupingCollection = groupingCollection;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Read-only property which returns the number of PropertyGroups contained
        /// in our collection.
        /// </summary>
        /// <owner>RGoel</owner>
        public int Count
        {
            get
            {
                return this.groupingCollection.PropertyGroupCount;
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
                return this.groupingCollection.IsSynchronized;
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
                return this.groupingCollection.SyncRoot;
            }
        }

        /// <summary>
        /// This looks through all the local property groups (those in the main
        /// project file, as opposed to any imported project files).  It returns
        /// the last one that comes before any imported property groups.  This
        /// is the heuristic we use to determine where to add new property groups
        /// into the project file.
        /// </summary>
        /// <owner>RGoel</owner>
        internal BuildPropertyGroup LastLocalPropertyGroup
        {
            get
            {
                BuildPropertyGroup lastLocalPropertyGroup = null;
                foreach (BuildPropertyGroup propertyGroup in this.groupingCollection.PropertyGroupsTopLevel)
                {
                    if (propertyGroup.IsImported)
                    {
                        // As soon as we hit an imported BuildPropertyGroup, we want to 
                        // completely bail out.  The goal of this function is 
                        // to return the last BuildPropertyGroup that is *before* any
                        // imported PropertyGroups.
                        break;
                    }
                    else
                    {
                        lastLocalPropertyGroup = propertyGroup;
                    }
                }

                return lastLocalPropertyGroup;
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
            this.groupingCollection.PropertyCopyTo(array, index);
        }

        /// <summary>
        /// This IEnumerable method returns an IEnumerator object, which allows
        /// the caller to enumerate through the BuildPropertyGroup objects contained in
        /// this BuildPropertyGroupCollection.
        /// </summary>
        /// <owner>RGoel</owner>
        public IEnumerator GetEnumerator
            (
            )
        {
            return this.groupingCollection.GetPropertyEnumerator();
        }

        /// <summary>
        /// Adds a new BuildPropertyGroup as the first element of our collection.
        /// This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void InsertAtBeginning
        (
            BuildPropertyGroup newPropertyGroup
        )
        {
            this.groupingCollection.InsertAtBeginning(newPropertyGroup);
        }

        /// <summary>
        /// Adds a new BuildPropertyGroup to our collection, at the specified insertion
        /// point.  This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void InsertAfter
        (
            BuildPropertyGroup newPropertyGroup,
            BuildPropertyGroup insertionPoint
        )
        {
            this.groupingCollection.InsertAfter(newPropertyGroup, insertionPoint);
        }

        /// <summary>
        /// Adds a new BuildPropertyGroup as the last element of our collection.
        /// This method does nothing to manipulate the project's XML content.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void InsertAtEnd
        (
            BuildPropertyGroup newPropertyGroup
        )
        {
            this.groupingCollection.InsertAtEnd(newPropertyGroup);
        }

        /// <summary>
        /// Removes a BuildPropertyGroup from our collection.  This method does nothing
        /// to manipulate the project's XML content.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void RemovePropertyGroup
        (
            BuildPropertyGroup propertyGroup
        )
        {
            this.groupingCollection.RemovePropertyGroup(propertyGroup);
        }

        #endregion
    }
}
