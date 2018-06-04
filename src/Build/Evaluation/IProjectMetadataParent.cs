// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Represents the parent of a ProjectMetadata object -
    /// either a ProjectItem or a ProjectItemDefinition.
    /// </summary>
    internal interface IProjectMetadataParent : IMetadataTable
    {
        /// <summary>
        /// The owning project
        /// </summary>
        Project Project
        {
            get;
        }

        /// <summary>
        /// The item type of the parent item definition or item.
        /// </summary>
        string ItemType
        {
            get;
        }
    }
}
