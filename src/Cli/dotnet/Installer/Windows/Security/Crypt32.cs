// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Defines various constants related to Windows cryptographic APIs.
    /// </summary>
    internal partial class Crypt32
    {
        //encoded single certificate
        internal const int CERT_QUERY_CONTENT_CERT = 1;
        //encoded single CTL
        internal const int CERT_QUERY_CONTENT_CTL = 2;
        //encoded single CRL
        internal const int CERT_QUERY_CONTENT_CRL = 3;
        //serialized store
        internal const int CERT_QUERY_CONTENT_SERIALIZED_STORE = 4;
        //serialized single certificate
        internal const int CERT_QUERY_CONTENT_SERIALIZED_CERT = 5;
        //serialized single CTL
        internal const int CERT_QUERY_CONTENT_SERIALIZED_CTL = 6;
        //serialized single CRL
        internal const int CERT_QUERY_CONTENT_SERIALIZED_CRL = 7;
        //a PKCS#7 signed message
        internal const int CERT_QUERY_CONTENT_PKCS7_SIGNED = 8;
        //a PKCS#7 message, such as enveloped message.  But it is not a signed message,
        internal const int CERT_QUERY_CONTENT_PKCS7_UNSIGNED = 9;
        //a PKCS7 signed message embedded in a file
        internal const int CERT_QUERY_CONTENT_PKCS7_SIGNED_EMBED = 10;
        //an encoded PKCS#10
        internal const int CERT_QUERY_CONTENT_PKCS10 = 11;
        //an encoded PFX BLOB
        internal const int CERT_QUERY_CONTENT_PFX = 12;
        //an encoded CertificatePair (contains forward and/or reverse cross certs)
        internal const int CERT_QUERY_CONTENT_CERT_PAIR = 13;
        //an encoded PFX BLOB, which was loaded to phCertStore
        internal const int CERT_QUERY_CONTENT_PFX_AND_LOAD = 14;

        internal const int CERT_QUERY_FORMAT_BINARY = 1;
        internal const int CERT_QUERY_FORMAT_BASE64_ENCODED = 2;
        internal const int CERT_QUERY_FORMAT_ASN_ASCII_HEX_ENCODED = 3;

        // See https://docs.microsoft.com/en-us/windows/win32/api/wincrypt/nf-wincrypt-cryptmsggetparam
        public const int CMSG_TYPE_PARAM = 1;
        public const int CMSG_CONTENT_PARAM = 2;
        public const int CMSG_BARE_CONTENT_PARAM = 3;
        public const int CMSG_INNER_CONTENT_TYPE_PARAM = 4;
        public const int CMSG_SIGNER_COUNT_PARAM = 5;
        public const int CMSG_SIGNER_INFO_PARAM = 6;
        public const int CMSG_SIGNER_CERT_INFO_PARAM = 7;
        public const int CMSG_SIGNER_HASH_ALGORITHM_PARAM = 8;
        public const int CMSG_SIGNER_AUTH_ATTR_PARAM = 9;
        public const int CMSG_SIGNER_UNAUTH_ATTR_PARAM = 10;
        public const int CMSG_CERT_COUNT_PARAM = 11;
        public const int CMSG_CERT_PARAM = 12;
        public const int CMSG_CRL_COUNT_PARAM = 13;
        public const int CMSG_CRL_PARAM = 14;
        public const int CMSG_ENVELOPE_ALGORITHM_PARAM = 15;
        public const int CMSG_RECIPIENT_COUNT_PARAM = 17;
        public const int CMSG_RECIPIENT_INDEX_PARAM = 18;
        public const int CMSG_RECIPIENT_INFO_PARAM = 19;
        public const int CMSG_HASH_ALGORITHM_PARAM = 20;
        public const int CMSG_HASH_DATA_PARAM = 21;
        public const int CMSG_COMPUTED_HASH_PARAM = 22;
        public const int CMSG_ENCRYPT_PARAM = 26;
        public const int CMSG_ENCRYPTED_DIGEST = 27;
        public const int CMSG_ENCODED_SIGNER = 28;
        public const int CMSG_ENCODED_MESSAGE = 29;
        public const int CMSG_VERSION_PARAM = 30;
        public const int CMSG_ATTR_CERT_COUNT_PARAM = 31;
        public const int CMSG_ATTR_CERT_PARAM = 32;
        public const int CMSG_CMS_RECIPIENT_COUNT_PARAM = 33;
        public const int CMSG_CMS_RECIPIENT_INDEX_PARAM = 34;
        public const int CMSG_CMS_RECIPIENT_ENCRYPTED_KEY_INDEX_PARAM = 35;
        public const int CMSG_CMS_RECIPIENT_INFO_PARAM = 36;
        public const int CMSG_UNPROTECTED_ATTR_PARAM = 37;
        public const int CMSG_SIGNER_CERT_ID_PARAM = 38;
        public const int CMSG_CMS_SIGNER_INFO_PARAM = 39;
    }
}
