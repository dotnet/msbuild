// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectItemElement"/>
    /// </summary>
    public abstract class ProjectItemElementLink : ProjectElementContainerLink
    {
        /// <summary>
        /// Help implement ItemType setter for remote objects.
        /// </summary>
        /// <param name="newType"></param>
        public abstract void ChangeItemType(string newType);
    }
}
