// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
