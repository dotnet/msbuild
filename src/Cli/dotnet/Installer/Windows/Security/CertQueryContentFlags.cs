// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Flags describing different content objects that can be queried.
    /// </summary>
    [Flags]
    public enum CertQueryContentFlags
    {
        /// <summary>
        /// The content is a single certificate
        /// </summary>
        Certificate = 1 << Crypt32.CERT_QUERY_CONTENT_CERT,

        /// <summary>
        /// The content is a single certificate trust list (CTL).
        /// </summary>
        CertificateTrustList = 1 << Crypt32.CERT_QUERY_CONTENT_CTL,

        /// <summary>
        /// The content is a single certificate revocation list (CRL).
        /// </summary>
        CertificalRevocationList = 1 << Crypt32.CERT_QUERY_CONTENT_CRL,

        /// <summary>
        /// The content is a serialized store.
        /// </summary>
        SerializedStore = 1 << Crypt32.CERT_QUERY_CONTENT_SERIALIZED_STORE,

        /// <summary>
        /// The content is a single, serialized certificate.
        /// </summary>
        SerializedCertificate = 1 << Crypt32.CERT_QUERY_CONTENT_SERIALIZED_CERT,

        /// <summary>
        /// The content is a single, serialized certificate trust list (CTL).
        /// </summary>
        SerializedCertificateTrustList = 1 << Crypt32.CERT_QUERY_CONTENT_SERIALIZED_CTL,

        /// <summary>
        /// The content is a single, serialized certificate revocation list (CRL).
        /// </summary>
        SerializedCertificateRevocationList = 1 << Crypt32.CERT_QUERY_CONTENT_SERIALIZED_CRL,

        /// <summary>
        /// The content is a PKCS #7 signed message.
        /// </summary>
        Pkcs7Signed = 1 << Crypt32.CERT_QUERY_CONTENT_PKCS7_SIGNED,

        /// <summary>
        /// The content is a PKCS #7 unsigned message.
        /// </summary>
        Pkcs7Unsigned = 1 << Crypt32.CERT_QUERY_CONTENT_PKCS7_UNSIGNED,

        /// <summary>
        /// The content is an embedded PKCS #7 signed message.
        /// </summary>
        Pkcs7SignedEmbed = 1 << Crypt32.CERT_QUERY_CONTENT_PKCS7_SIGNED_EMBED,

        /// <summary>
        /// The content is a PKCS #10 message.
        /// </summary>
        Pkcs10 = 1 << Crypt32.CERT_QUERY_CONTENT_PKCS10,

        /// <summary>
        /// The content is an encoded PFX blob.
        /// </summary>
        Pfx = 1 << Crypt32.CERT_QUERY_CONTENT_PFX,

        /// <summary>
        /// An encoded CertificatePair (contains forward and/or reverse cross certs)
        /// </summary>
        CertificatePair = 1 << Crypt32.CERT_QUERY_CONTENT_CERT_PAIR,

        /// <summary>
        /// The content is an encoded PFX blob and will be loaded subject to various conditions.
        /// See <see href="https://docs.microsoft.com/en-us/windows/win32/api/wincrypt/nf-wincrypt-cryptqueryobject">this</see> for more details.
        /// </summary>
        PfxAndLoad = 1 << Crypt32.CERT_QUERY_CONTENT_PFX_AND_LOAD,

        /// <summary>
        /// The content can be any type, except <see cref="PfxAndLoad"/>.
        /// </summary>
        All = Certificate | CertificateTrustList | CertificalRevocationList |
           SerializedStore | SerializedCertificate | SerializedCertificateTrustList | SerializedCertificateRevocationList |
           Pkcs7Signed | Pkcs7Unsigned | Pkcs7SignedEmbed | Pkcs10 | Pfx |
           CertificatePair
    }
}
