// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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
