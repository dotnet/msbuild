// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

#nullable disable

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectTargetElement"/>
    /// </summary>
    public abstract class ProjectTargetElementLink : ProjectElementContainerLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectTargetElement.Name"/>.
        /// </summary>
        public abstract string Name { get; set; }

        /// <summary>
        /// Access to remote <see cref="ProjectTargetElement.Returns"/>.
        /// </summary>
        public abstract string Returns { set; }
    }
}
