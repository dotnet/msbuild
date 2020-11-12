// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// This interface is used to describe a class which can act as a factory for creating
    /// items when the Expander expands certain expressions.
    /// </summary>
    /// <typeparam name="S">The type of items this factory can clone from.</typeparam>
    /// <typeparam name="T">The type of items this factory will create.</typeparam>
    internal interface IItemFactory<S, T>
        where S : class, IItem
        where T : class, IItem
    {
        /// <summary>
        /// The item type of the items that this factory will create.
        /// May be null, if the items will not have an itemtype (ie., for ITaskItems)
        /// May not be settable (eg., for ITaskItems and for ProjectItems)
        /// </summary>
        string ItemType
        {
            get;
            set;
        }

        /// <summary>
        /// Used in the evaluator
        /// </summary>
        ProjectItemElement ItemElement
        {
            set;
        }

        /// <summary>
        /// Creates an item with the specified evaluated include and defining project.
        /// Include must not be zero length.
        /// </summary>
        /// <param name="include">The include</param>
        /// <param name="definingProject">The project from which this item was created</param>
        /// <returns>A new item instance</returns>
        T CreateItem(string include, string definingProject);

        /// <summary>
        /// Creates an item based off the provided item, with cloning semantics.
        /// New item is associated with the passed in defining project, not that of the original item. 
        /// </summary>
        T CreateItem(S source, string definingProject);

        /// <summary>
        /// Creates an item with the specified include and the metadata from the specified base item
        /// New item is associated with the passed in defining project, not that of the original item. 
        /// </summary>
        T CreateItem(string include, S baseItem, string definingProject);

        /// <summary>
        /// Creates an item using the specified evaluated include, include before wildcard expansion, 
        /// and defining project.
        /// </summary>
        T CreateItem(string include, string includeBeforeWildcardExpansion, string definingProject);

        /// <summary>
        /// Applies the supplied metadata to the destination items.
        /// </summary>
        void SetMetadata(IEnumerable<Pair<ProjectMetadataElement, string>> metadata, IEnumerable<T> destinationItems);
    }
}
