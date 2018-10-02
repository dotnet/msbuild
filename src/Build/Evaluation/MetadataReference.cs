// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// This struct represents a reference to a piece of item metadata.  For example,
    /// %(EmbeddedResource.Culture) or %(Culture) in the project file.  In this case,
    /// "EmbeddedResource" is the item name, and "Culture" is the metadata name.
    /// The item name is optional.
    /// </summary>
    internal struct MetadataReference
    {
        /// <summary>
        /// The item name
        /// </summary>
        internal string ItemName;       // Could be null if the %(...) is not qualified with an item name.

        /// <summary>
        /// The metadata name
        /// </summary>
        internal string MetadataName;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="itemName">Name of the item</param>
        /// <param name="metadataName">Name of the metadata</param>
        internal MetadataReference
        (
            string itemName,
            string metadataName
        )
        {
            this.ItemName = itemName;
            this.MetadataName = metadataName;
        }
    }
}
