// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;

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
