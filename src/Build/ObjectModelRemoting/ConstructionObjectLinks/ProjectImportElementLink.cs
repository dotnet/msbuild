// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectImportElement"/>
    /// </summary>
    public abstract class ProjectImportElementLink : ProjectElementLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectImportElement.ImplicitImportLocation"/>.
        /// </summary>
        public abstract ImplicitImportLocation ImplicitImportLocation { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectImportElement.OriginalElement"/>.
        /// </summary>
        public abstract ProjectElement OriginalElement { get; }
    }
}
