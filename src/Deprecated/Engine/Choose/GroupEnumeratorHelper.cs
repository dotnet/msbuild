// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System.Collections;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Helper class that basically just implements an IEnumerable over a
    /// GroupingCollection.  This object allows you to use the GroupingCollection
    /// in foreach statements.
    /// </summary>
    internal sealed class GroupEnumeratorHelper : IEnumerable
    {
        internal enum ListType
        {
            PropertyGroupsTopLevelAndChoose,    // return Choose and BuildPropertyGroup
            ItemGroupsTopLevelAndChoose,        // return Choose and BuildItemGroup
            PropertyGroupsTopLevel,
            ItemGroupsTopLevel,
            PropertyGroupsAll,
            ItemGroupsAll,
            ChoosesTopLevel
        };

        #region Member Data

        // Reference to the GroupingCollection object to get the
        // enumerator from.
        private GroupingCollection groupingCollection;

        // Type of enumerator to return
        private ListType type;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor for the GroupEnumeratorHelper.  At construction
        /// time, you specify the GroupingCollection to use, and the type
        /// of enumerator you wish to get.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="groupingCollection"></param>
        /// <param name="type"></param>
        /// <returns>IEnumerator</returns>
        internal GroupEnumeratorHelper
            (
                GroupingCollection groupingCollection,
                ListType type
            )
        {
            error.VerifyThrow(groupingCollection != null, "GroupingCollection is null");

            this.groupingCollection = groupingCollection;
            this.type = type;
        }

        #endregion


        #region Methods

        /// <summary>
        /// Returns an enumerator into the GroupingCollection specified
        /// at instantiation time.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <returns>IEnumerator</returns>
        public IEnumerator GetEnumerator()
        {
            foreach (IItemPropertyGrouping group in this.groupingCollection)
            {
                if ((group is BuildItemGroup) &&
                    ((this.type == ListType.ItemGroupsTopLevel) || (this.type == ListType.ItemGroupsTopLevelAndChoose) || (this.type == ListType.ItemGroupsAll)))
                {
                    yield return group;
                }
                else if ((group is BuildPropertyGroup) &&
                         ((this.type == ListType.PropertyGroupsTopLevel) || (this.type == ListType.PropertyGroupsTopLevelAndChoose) || (this.type == ListType.PropertyGroupsAll)))
                {
                    yield return group;
                }
                else if (group is Choose)
                {
                    if ((this.type == ListType.ChoosesTopLevel) || (this.type == ListType.ItemGroupsTopLevelAndChoose) || (this.type == ListType.PropertyGroupsTopLevelAndChoose))
                    {
                        yield return group;
                    }
                    // Recurse into Choose groups to find all item/property groups
                    else if ((this.type == ListType.ItemGroupsAll) || (this.type == ListType.PropertyGroupsAll))
                    {
                        Choose choose = (Choose)group;

                        foreach (When when in choose.Whens)
                        {
                            if (this.type == ListType.ItemGroupsAll)
                            {
                                foreach (IItemPropertyGrouping nestedGroup in when.PropertyAndItemLists.ItemGroupsAll)
                                {
                                    yield return nestedGroup;
                                }
                            }
                            else if (this.type == ListType.PropertyGroupsAll)
                            {
                                foreach (IItemPropertyGrouping nestedGroup in when.PropertyAndItemLists.PropertyGroupsAll)
                                {
                                    yield return nestedGroup;
                                }
                            }
                        }

                        if (choose.Otherwise != null)
                        {
                            if (this.type == ListType.ItemGroupsAll)
                            {
                                foreach (IItemPropertyGrouping nestedGroup in choose.Otherwise.PropertyAndItemLists.ItemGroupsAll)
                                {
                                    yield return nestedGroup;
                                }
                            }
                            else if (this.type == ListType.PropertyGroupsAll)
                            {
                                foreach (IItemPropertyGrouping nestedGroup in choose.Otherwise.PropertyAndItemLists.PropertyGroupsAll)
                                {
                                    yield return nestedGroup;
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion
    }
}
