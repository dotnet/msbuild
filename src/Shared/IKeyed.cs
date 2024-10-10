// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Interface allowing items and metadata and properties to go into keyed collections
    /// </summary>
    /// <remarks>
    /// This can be internal as it is a constraint only on internal collections.
    /// </remarks>
    internal interface IKeyed
    {
        /// <summary>
        /// Returns some value useful for a key in a dictionary
        /// </summary>
        string Key
        {
            get;
        }
    }
}
