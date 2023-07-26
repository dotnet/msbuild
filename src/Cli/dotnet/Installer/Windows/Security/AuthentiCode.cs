// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Contains various utilities methods around verifying AuthentiCode signatures on Windows.
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    internal static class AuthentiCode
    {
        /// <summary>
        /// A set of trusted organizations used to verify the certificates associated with an AuthentiCode signature.
        /// </summary>
        public static readonly string[] TrustedOrganizations = { "Microsoft Corporation" };

        /// <summary>
        /// Object identifier value for nested signature.
        /// </summary>
        private const string OidNestedSignature = "1.3.6.1.4.1.311.2.4.1";

        /// <summary>
        /// Verifies the authenticode signature of the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to verify.</param>
        /// <returns><see langword="true"/> if the signature is valid; <see langword="false"/> otherwise.</returns>
        public static bool IsSigned(string path)
        {
            WinTrustFileInfo fileInfo = new()
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo)),
                pcwszFilePath = Path.GetFullPath(path),
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            WinTrustData data = new()
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustData)),
                dwProvFlags = 0,
                dwStateAction = StateAction.WTD_STATEACTION_IGNORE,
                dwUIChoice = UIChoice.WTD_UI_NONE,
                dwUIContext = 0,
                dwUnionChoice = UnionChoice.WTD_CHOICE_FILE,
                fdwRevocationChecks = RevocationChecks.WTD_REVOKE_NONE,
                hWVTStateData = IntPtr.Zero,
                pWinTrustInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustFileInfo))),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero
            };

            Marshal.StructureToPtr(fileInfo, data.pWinTrustInfo, false);

            IntPtr pGuid = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Guid)));
            IntPtr pData = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinTrustData)));
            Marshal.StructureToPtr(data, pData, true);
            Marshal.StructureToPtr(NativeMethods.WINTRUST_ACTION_GENERIC_VERIFY_V2, pGuid, true);

            uint result = NativeMethods.WinVerifyTrust(IntPtr.Zero, pGuid, pData);

            Marshal.FreeHGlobal(pGuid);
            Marshal.FreeHGlobal(pData);
            Marshal.FreeHGlobal(data.pWinTrustInfo);

            // The return value is a LONG, not an HRESULT.
            // Do not use HRESULT macros such as SUCCEEDED to determine whether the function succeeded.
            // Instead, check the return value for equality to zero.
            return result == 0;
        }

        /// <summary>
        /// Determines if the file contains a signature associated with one of the specified organizations. The primary certificate is queried
        /// before looking at nested certificates.
        /// </summary>
        /// <param name="path">The path of the signed file.</param>
        /// <param name="organizations">A set of trusted organizations.</param>
        /// <returns><see langword="true"/> if organization described in the certificate subject matches any of the specified 
        /// organizations; <see langword="false"/> otherwise.</returns>
        internal static bool IsSignedByTrustedOrganization(string path, params string[] organizations)
        {
            try
            {
                IEnumerable<X509Certificate2> certificates = GetCertificates(path);

                return certificates.Any(c => organizations.Any(o => c.Subject.Contains($"O={o}", StringComparison.OrdinalIgnoreCase)));
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        /// <summary>
        /// Retrieves the certificates from each signature in the specified file, including nested signatures.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>A collection of one or more certificates.</returns>
        internal static List<X509Certificate2> GetCertificates(string path)
        {
            List<X509Certificate2> certificates = new();

            try
            {
                certificates.Add(new X509Certificate2(X509Certificate.CreateFromSignedFile(path)));
            }
            catch (CryptographicException)
            {
                return certificates;
            }

            SignedCms cms = CreateSignedCmsFromFile(path);

            foreach (CryptographicAttributeObject attribute in cms.SignerInfos[0].UnsignedAttributes)
            {
                if (attribute.Oid.Value.Equals(OidNestedSignature))
                {
                    foreach (AsnEncodedData value in attribute.Values)
                    {
                        SignedCms nestedCms = new();
                        nestedCms.Decode(value.RawData);

                        certificates.Add(nestedCms.Certificates[0]);
                    }
                }
            }

            return certificates;
        }

        /// <summary>
        /// Creates a <see cref="SignedCms"/> object from all the certificate data in a file.
        /// </summary>
        /// <param name="path">The full path of the file to use.</param>
        /// <returns></returns>
        private static SignedCms CreateSignedCmsFromFile(string path)
        {
            int msgAndCertEncodingType;
            int msgContentType;
            int formatType;

            IntPtr certStore = IntPtr.Zero;
            IntPtr phMessage = IntPtr.Zero;
            IntPtr context = IntPtr.Zero;
            IntPtr pvObject = Marshal.StringToHGlobalUni(path);

            try
            {
                if (!NativeMethods.CryptQueryObject(CryptQueryObjectType.File, pvObject,
                    CertQueryContentFlags.All, CertQueryFormatFlags.All, 0,
                    out msgAndCertEncodingType, out msgContentType, out formatType, ref certStore, ref phMessage, ref context))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                int cbData = 0;

                // Passing in NULL to pvData retrieves the size of the encoded message, allowing us to allocate a buffer and then
                // call the function again to retrieve it.
                if (!NativeMethods.CryptMsgGetParam(phMessage, Crypt32.CMSG_ENCODED_MESSAGE, 0, IntPtr.Zero, ref cbData))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                byte[] pvData = new byte[cbData];
                if (!NativeMethods.CryptMsgGetParam(phMessage, Crypt32.CMSG_ENCODED_MESSAGE, 0, pvData, ref cbData))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var signedCms = new SignedCms();
                signedCms.Decode(pvData);

                return signedCms;
            }
            finally
            {
                Marshal.FreeHGlobal(pvObject);
                Marshal.FreeHGlobal(phMessage);
            }
        }
    }
}
