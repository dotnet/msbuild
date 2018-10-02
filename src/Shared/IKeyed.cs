// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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