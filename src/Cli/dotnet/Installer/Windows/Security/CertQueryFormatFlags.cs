// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// The expected format of return types, expressed as a flag.
    /// </summary>
    public enum CertQueryFormatFlags : uint
    {
        /// <summary>
        /// The content is in binary format.
        /// </summary>
        Binary = 1 << Crypt32.CERT_QUERY_FORMAT_BINARY,

        /// <summary>
        /// The content is base64 encoded.
        /// </summary>
        Base64 = 1 << Crypt32.CERT_QUERY_FORMAT_BASE64_ENCODED,

        /// <summary>
        /// The content is in ASCII hex-encoded with a "{ASN}" prefix.
        /// </summary>
        AsnAsciiHex = 1 << Crypt32.CERT_QUERY_FORMAT_ASN_ASCII_HEX_ENCODED,

        /// <summary>
        /// The content can be returned in any format.
        /// </summary>
        All = Binary | Base64 | AsnAsciiHex
    }
}
