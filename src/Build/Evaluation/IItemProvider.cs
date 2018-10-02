// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// This interface represents an object which can act as a source of items for the Expander.
    /// </summary>
    /// <typeparam name="T">The type of items provided by the implementation.</typeparam>
    internal interface IItemProvider<T> where T : IItem
    {
        /// <summary>
        /// Returns a list of items with the specified item type.
        /// 
        /// If there are no items of this type, returns an empty list.
        /// </summary>
        /// <param name="itemType">The item type of items to return.</param>
        /// <returns>A list of matching items.</returns>
        ICollection<T> GetItems(string itemType);
    }
}
