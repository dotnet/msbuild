// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

#nullable disable

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectExtensionsElement"/>
    /// </summary>
    public abstract class ProjectExtensionsElementLink : ProjectElementLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectExtensionsElement.Content"/>.
        /// </summary>
        public abstract string Content { get; set; }

        /// <summary>
        /// Helps implementing sub element indexer.
        /// </summary>
        public abstract string GetSubElement(string name);

        /// <summary>
        /// Helps implementing sub element indexer.
        /// </summary>
        public abstract void SetSubElement(string name, string value);
    }
}
