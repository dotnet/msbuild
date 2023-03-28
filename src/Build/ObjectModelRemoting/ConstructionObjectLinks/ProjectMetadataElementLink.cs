// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

#nullable disable

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
