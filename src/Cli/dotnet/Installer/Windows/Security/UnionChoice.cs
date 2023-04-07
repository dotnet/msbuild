// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Specifies the union member to use when verifying trust.
    /// </summary>
    public enum UnionChoice : uint
    {
        /// <summary>
        /// Use the file pointed to by pFile.
        /// </summary>
        WTD_CHOICE_FILE = 1,

        /// <summary>
        /// Use the catalog pointed to by pCatalog.
        /// </summary>
        WTD_CHOICE_CATALOG = 2,

        /// <summary>
        /// Use the BLOB pointed to by pBlob.
        /// </summary>
        WTD_CHOICE_BLOB,

        /// <summary>
        /// Use the WINTRUST_SGNR_INFO structure pointed to by pSgnr.
        /// </summary>
        WTD_CHOICE_SIGNER,

        /// <summary>
        /// Use the certificate pointed to by pCert.
        /// </summary>
        WTD_CHOICE_CERT
    }
}
