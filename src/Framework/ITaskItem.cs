// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface defines a project item that can be consumed and emitted by tasks.
    /// </summary>
    [ComVisible(true)]
    [Guid("8661674F-2148-4F71-A92A-49875511C528")]
    public interface ITaskItem
    {
        /// <summary>
        /// Gets or sets the item "specification" e.g. for disk-based items this would be the file path.
        /// </summary>
        /// <remarks>
        /// This should be named "EvaluatedInclude" but that would be a breaking change to this interface.
        /// </remarks>
        /// <value>The item-spec string.</value>
        string ItemSpec
        {
            get;

            set;
        }

        /// <summary>
        /// Gets the names of all the metadata on the item.
        /// Includes the built-in metadata like "FullPath".
        /// </summary>
        /// <value>The list of metadata names.</value>
        ICollection MetadataNames
        {
            get;
        }

        /// <summary>
        /// Gets the number of pieces of metadata on the item. Includes
        /// both custom and built-in metadata.
        /// </summary>
        /// <value>Count of pieces of metadata.</value>
        int MetadataCount
        {
            get;
        }

        /// <summary>
        /// Allows the values of metadata on the item to be queried.
        /// </summary>
        /// <param name="metadataName">The name of the metadata to retrieve.</param>
        /// <returns>The value of the specified metadata.</returns>
        string GetMetadata(string metadataName);

        /// <summary>
        /// Allows a piece of custom metadata to be set on the item.
        /// </summary>
        /// <param name="metadataName">The name of the metadata to set.</param>
        /// <param name="metadataValue">The metadata value.</param>
        void SetMetadata(string metadataName, string metadataValue);

        /// <summary>
        /// Allows the removal of custom metadata set on the item.
        /// </summary>
        /// <param name="metadataName">The name of the metadata to remove.</param>
        void RemoveMetadata(string metadataName);

        /// <summary>
        /// Allows custom metadata on the item to be copied to another item.
        /// </summary>
        /// <remarks>
        /// RECOMMENDED GUIDELINES FOR METHOD IMPLEMENTATIONS:
        /// 1) this method should NOT copy over the item-spec
        /// 2) if a particular piece of metadata already exists on the destination item, it should NOT be overwritten
        /// 3) if there are pieces of metadata on the item that make no semantic sense on the destination item, they should NOT be copied
        /// </remarks>
        /// <param name="destinationItem">The item to copy metadata to.</param>
        void CopyMetadataTo(ITaskItem destinationItem);

        /// <summary>
        /// Get the collection of custom metadata. This does not include built-in metadata.
        /// </summary>
        /// <remarks>
        /// RECOMMENDED GUIDELINES FOR METHOD IMPLEMENTATIONS:
        /// 1) this method should return a clone of the metadata
        /// 2) writing to this dictionary should not be reflected in the underlying item.
        /// </remarks>
        /// <returns>Dictionary of cloned metadata</returns>
        IDictionary CloneCustomMetadata();
    }
}
