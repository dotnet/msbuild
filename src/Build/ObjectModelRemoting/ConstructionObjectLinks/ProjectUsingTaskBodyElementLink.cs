// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectUsingTaskBodyElement"/>
    /// </summary>
    public abstract class ProjectUsingTaskBodyElementLink : ProjectElementLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectUsingTaskBodyElement.TaskBody"/>.
        /// </summary>
        public abstract string TaskBody { get; set; }
    }
}
