// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// The type of object to be queried.
    /// </summary>
    public enum CryptQueryObjectType : uint
    {
        /// <summary>
        /// The object is stored in a file.
        /// </summary>
        File = 1,

        /// <summary>
        /// The object is stored in memory.
        /// </summary>
        Blob = 2
    }
}
