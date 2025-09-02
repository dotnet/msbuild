// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

#nullable disable

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectItem"/>
    /// </summary>
    public abstract class ProjectItemLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectItem.Project"/>.
        /// </summary>
        public abstract Project Project { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectItem.Xml"/>.
        /// </summary>
        public abstract ProjectItemElement Xml { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectItem.EvaluatedInclude"/>.
        /// </summary>
        public abstract string EvaluatedInclude { get; }

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItem.Metadata"/> and <see cref="ProjectItem.MetadataCount"/>.
        /// </summary>
        public abstract ICollection<ProjectMetadata> MetadataCollection { get; }

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItem.DirectMetadata"/> and <see cref="ProjectItem.DirectMetadataCount"/>.
        /// </summary>
        public abstract ICollection<ProjectMetadata> DirectMetadata { get; }

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItem.HasMetadata"/>.
        /// </summary>
        public abstract bool HasMetadata(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItem.GetMetadata"/>.
        /// </summary>
        public abstract ProjectMetadata GetMetadata(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItem.GetMetadataValue"/>.
        /// </summary>
        public abstract string GetMetadataValue(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItem.SetMetadataValue(string, string, bool)"/>.
        /// </summary>
        public abstract ProjectMetadata SetMetadataValue(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItem.RemoveMetadata"/>.
        /// </summary>
        public abstract bool RemoveMetadata(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItem.Rename"/>.
        /// </summary>
        public abstract void Rename(string name);

        /// <summary>
        /// Helps implementing the item type change for remoted objects>.
        /// </summary>
        public abstract void ChangeItemType(string newItemType);
    }
}
