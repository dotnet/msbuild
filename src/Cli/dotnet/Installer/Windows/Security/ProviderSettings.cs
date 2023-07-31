// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Trust provider settings.
    /// </summary>
    [Flags]
    public enum ProviderSettings : uint
    {
        /// <summary>
        /// The trust is verified in the same manner as implemented by Internet Explorer 4.0.
        /// </summary>
        WTD_USE_IE4_TRUST_FLAG = 0x00000001,

        /// <summary>
        /// The Internet Explorer 4.0 chain functionality is not used.
        /// </summary>
        WTD_NO_IE4_CHAIN_FLAG = 0x00000002,

        /// <summary>
        /// The default verification of the policy provider, such as code signing for Authenticode, is not performed, and the certificate is assumed valid for all usages.
        /// </summary>
        WTD_NO_POLICY_USAGE_FLAG = 0x00000004,

        /// <summary>
        /// Revocation checking is not performed.
        /// </summary>
        WTD_REVOCATION_CHECK_NONE = 0x00000010,

        /// <summary>
        /// Revocation checking is performed on the end certificate only.
        /// </summary>
        WTD_REVOCATION_CHECK_END_CERT = 0x00000020,

        /// <summary>
        /// Revocation checking is performed on the entire certificate chain.
        /// </summary>
        WTD_REVOCATION_CHECK_CHAIN = 0x00000040,

        /// <summary>
        /// Revocation checking is performed on the entire certificate chain, excluding the root certificate.
        /// </summary>
        WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT = 0x00000080,

        /// <summary>
        /// Not supported.
        /// </summary>
        WTD_SAFER_FLAG = 0x00000100,

        /// <summary>
        /// Only the hash is verified.
        /// </summary>
        WTD_HASH_ONLY_FLAG = 0x00000200,

        /// <summary>
        /// The default operating system version checking is performed. This flag is only used for verifying catalog-signed files.
        /// </summary>
        WTD_USE_DEFAULT_OSVER_CHECK = 0x00000400,

        /// <summary>
        /// If this flag is not set, all time stamped signatures are considered valid forever. Setting this flag limits the valid lifetime of the signature to the lifetime of the signing certificate. This allows time stamped signatures to expire.
        /// </summary>
        WTD_LIFETIME_SIGNING_FLAG = 0x00000800,

        /// <summary>
        /// Use only the local cache for revocation checks. Prevents revocation checks over the network. This value is not supported on Windows XP.        
        /// </summary>
        WTD_CACHE_ONLY_URL_RETRIEVAL = 0x00001000,

        /// <summary>
        /// Disable the use of MD2 and MD4 hashing algorithms. If a file is signed by using MD2 or MD4 and if this flag is set, an NTE_BAD_ALGID error is returned.
        /// This flag is only supported on Windows 7 SP1 and later.
        /// </summary>
        WTD_DISABLE_MD2_MD4 = 0x00002000,

        /// <summary>
        /// If this flag is specified it is assumed that the file being verified has been downloaded from the web and has the Mark of the Web attribute. Policies that are meant to apply to Mark of the Web files will be enforced.
        /// This flag is only supported on Windows 8.1 and later or system that installed KB2862966.
        /// </summary>
        WTD_MOTW = 0x00004000
    }
}
