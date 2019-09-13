// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectMetadataElement"/>
    /// </summary>
    public abstract class ProjectMetadataElementLink : ProjectElementLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectMetadataElement.Value"/>.
        /// </summary>
        public abstract string Value { get; set; }

        /// <summary>
        /// Help implement rename.
        /// </summary>
        public abstract void ChangeName(string newName);
    }
}
