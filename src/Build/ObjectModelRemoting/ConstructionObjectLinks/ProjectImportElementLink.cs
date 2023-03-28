// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

#nullable disable

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
