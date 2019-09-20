// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;

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
