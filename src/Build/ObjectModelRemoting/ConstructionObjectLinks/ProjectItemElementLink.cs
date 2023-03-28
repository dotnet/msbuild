// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

#nullable disable

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
