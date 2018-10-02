// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Represents the location of an implicit import.
    /// </summary>
    public enum ImplicitImportLocation
    {
        /// <summary>
        /// The import is not implicitly added and is explicitly added in a user-specified location.
        /// </summary>
        None,
        /// <summary>
        /// The import was implicitly added at the top of the project.
        /// </summary>
        Top,
        /// <summary>
        /// The import was implicitly added at the bottom of the project.
        /// </summary>
        Bottom
    }
}