// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using System.Collections.Generic;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectItemDefinition"/>
    /// </summary>
    public abstract class ProjectItemDefinitionLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectItemDefinition.Project"/>.
        /// </summary>
        public abstract Project Project { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectItemDefinition.ItemType"/>.
        /// </summary>
        public abstract string ItemType { get; }

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItemDefinition.Metadata"/> and <see cref="ProjectItemDefinition.MetadataCount"/>.
        /// </summary>
        public abstract ICollection<ProjectMetadata> Metadata { get; }

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItemDefinition.GetMetadata"/>.
        /// </summary>
        public abstract ProjectMetadata GetMetadata(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItemDefinition.GetMetadataValue"/>.
        /// </summary>
        public abstract string GetMetadataValue(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectItemDefinition.SetMetadataValue"/>.
        /// </summary>
        public abstract ProjectMetadata SetMetadataValue(string name, string unevaluatedValue);
    }
}
