// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


//
//


using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Runtime.InteropServices;

using _FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace System.Deployment.Internal.CodeSigning
{
    internal static class Win32
    {
        //
        // PInvoke dll's.
        //
        internal const String KERNEL32 = "kernel32.dll";
#if (true)

#if FEATURE_MAIN_CLR_MODULE_USES_CORE_NAME
        internal const String MSCORWKS = "coreclr.dll";
#elif USE_OLD_MSCORWKS_NAME // for updating devdiv toolset until it has clr.dll
        internal const String MSCORWKS = "mscorwks.dll";
#else //FEATURE_MAIN_CLR_MODULE_USES_CORE_NAME
        internal const String MSCORWKS = "clr.dll";
#endif //FEATURE_MAIN_CLR_MODULE_USES_CORE_NAME

#else
        internal const String MSCORWKS = "isowhidbey.dll";
#endif
        //
        // Constants.
        //
        internal const int S_OK = unchecked((int)0x00000000);
        internal const int NTE_BAD_KEY = unchecked((int)0x80090003);

        // Trust errors.
        internal const int TRUST_E_SYSTEM_ERROR = unchecked((int)0x80096001);
        internal const int TRUST_E_NO_SIGNER_CERT = unchecked((int)0x80096002);
        internal const int TRUST_E_COUNTER_SIGNER = unchecked((int)0x80096003);
        internal const int TRUST_E_CERT_SIGNATURE = unchecked((int)0x80096004);
        internal const int TRUST_E_TIME_STAMP = unchecked((int)0x80096005);
        internal const int TRUST_E_BAD_DIGEST = unchecked((int)0x80096010);
        internal const int TRUST_E_BASIC_CONSTRAINTS = unchecked((int)0x80096019);
        internal const int TRUST_E_FINANCIAL_CRITERIA = unchecked((int)0x8009601E);
        internal const int TRUST_E_PROVIDER_UNKNOWN = unchecked((int)0x800B0001);
        internal const int TRUST_E_ACTION_UNKNOWN = unchecked((int)0x800B0002);
        internal const int TRUST_E_SUBJECT_FORM_UNKNOWN = unchecked((int)0x800B0003);
        internal const int TRUST_E_SUBJECT_NOT_TRUSTED = unchecked((int)0x800B0004);
        internal const int TRUST_E_NOSIGNATURE = unchecked((int)0x800B0100);
        internal const int CERT_E_UNTRUSTEDROOT = unchecked((int)0x800B0109);
        internal const int TRUST_E_FAIL = unchecked((int)0x800B010B);
        internal const int TRUST_E_EXPLICIT_DISTRUST = unchecked((int)0x800B0111);
        internal const int CERT_E_CHAINING = unchecked((int)0x800B010A);


        // Values for dwFlags of CertVerifyAuthenticodeLicense.
        internal const int AXL_REVOCATION_NO_CHECK = unchecked((int)0x00000001);
        internal const int AXL_REVOCATION_CHECK_END_CERT_ONLY = unchecked((int)0x00000002);
        internal const int AXL_REVOCATION_CHECK_ENTIRE_CHAIN = unchecked((int)0x00000004);
        internal const int AXL_URL_CACHE_ONLY_RETRIEVAL = unchecked((int)0x00000008);
        internal const int AXL_LIFETIME_SIGNING = unchecked((int)0x00000010);
        internal const int AXL_TRUST_MICROSOFT_ROOT_ONLY = unchecked((int)0x00000020);

        // Wintrust Policy Flag
        //  These are set during install and can be modified by the user
        //  through various means.  The SETREG.EXE utility (found in the Authenticode
        //  Tools Pack) will select/deselect each of them.
        internal const int WTPF_IGNOREREVOKATION = (int)0x00000200;  // Do revocation check

        // The default WinVerifyTrust Authenticode policy is to treat all time stamped
        // signatures as being valid forever. This OID limits the valid lifetime of the
        // signature to the lifetime of the certificate. This allows timestamped
        // signatures to expire. Normally this OID will be used in conjunction with
        // szOID_PKIX_KP_CODE_SIGNING to indicate new time stamp semantics should be
        // used. Support for this OID was added in WXP.
        internal const string szOID_KP_LIFETIME_SIGNING = "1.3.6.1.4.1.311.10.3.13";
        internal const string szOID_RSA_signingTime = "1.2.840.113549.1.9.5";

        //
        // Structures.
        //
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct CRYPT_DATA_BLOB
        {
            internal uint cbData;
            internal IntPtr pbData;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct AXL_SIGNER_INFO
        {
            internal uint cbSize;             // sizeof(AXL_SIGNER_INFO).
            internal uint dwError;            // Error code.
            internal uint algHash;            // Hash algorithm (ALG_ID).
            internal IntPtr pwszHash;           // Hash.
            internal IntPtr pwszDescription;    // Description.
            internal IntPtr pwszDescriptionUrl; // Description URL.
            internal IntPtr pChainContext;      // Signer's chain context.
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct AXL_TIMESTAMPER_INFO
        {
            internal uint cbSize;             // sizeof(AXL_TIMESTAMPER_INFO).
            internal uint dwError;            // Error code.
            internal uint algHash;            // Hash algorithm (ALG_ID).
            internal _FILETIME ftTimestamp;        // Timestamp time.
            internal IntPtr pChainContext;      // Timestamper's chain context.
        }

        //
        // DllImport declarations.
        //
        [DllImport(KERNEL32, CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static
        IntPtr GetProcessHeap();

        [DllImport(KERNEL32, CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal extern static
        bool HeapFree(
            [In]    IntPtr hHeap,
            [In]    uint dwFlags,
            [In]    IntPtr lpMem);

        [DllImport(MSCORWKS, CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static
        int CertTimestampAuthenticodeLicense(
            [In]      ref CRYPT_DATA_BLOB pSignedLicenseBlob,
            [In]      string pwszTimestampURI,
            [In, Out]  ref CRYPT_DATA_BLOB pTimestampSignatureBlob);

        [DllImport(MSCORWKS, CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static
        int CertVerifyAuthenticodeLicense(
            [In]      ref CRYPT_DATA_BLOB pLicenseBlob,
            [In]      uint dwFlags,
            [In, Out]  ref AXL_SIGNER_INFO pSignerInfo,
            [In, Out]  ref AXL_TIMESTAMPER_INFO pTimestamperInfo);

        [DllImport(MSCORWKS, CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static
        int CertFreeAuthenticodeSignerInfo(
            [In]      ref AXL_SIGNER_INFO pSignerInfo);

        [DllImport(MSCORWKS, CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static
        int CertFreeAuthenticodeTimestamperInfo(
            [In]      ref AXL_TIMESTAMPER_INFO pTimestamperInfo);

        [DllImport(MSCORWKS, CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static
        int _AxlGetIssuerPublicKeyHash(
            [In]     IntPtr pCertContext,
            [In, Out] ref IntPtr ppwszPublicKeyHash);

        [DllImport(MSCORWKS, CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static
        int _AxlRSAKeyValueToPublicKeyToken(
            [In]     ref CRYPT_DATA_BLOB pModulusBlob,
            [In]     ref CRYPT_DATA_BLOB pExponentBlob,
            [In, Out] ref IntPtr ppwszPublicKeyToken);

        [DllImport(MSCORWKS, CharSet = CharSet.Auto, SetLastError = true)]
        internal extern static
        int _AxlPublicKeyBlobToPublicKeyToken(
            [In]     ref CRYPT_DATA_BLOB pCspPublicKeyBlob,
            [In, Out] ref IntPtr ppwszPublicKeyToken);
    }

    internal class ManifestSignedXml : SignedXml
    {
        private bool _verify = false;

        internal ManifestSignedXml() : base() { }
        internal ManifestSignedXml(XmlElement elem) : base(elem) { }
        internal ManifestSignedXml(XmlDocument document) : base(document) { }

        internal ManifestSignedXml(XmlDocument document, bool verify) : base(document)
        {
            _verify = verify;
        }

        private static XmlElement FindIdElement(XmlElement context, string idValue)
        {
            if (context == null)
                return null;

            XmlElement idReference = context.SelectSingleNode("//*[@Id=\"" + idValue + "\"]") as XmlElement;
            if (idReference != null)
                return idReference;
            idReference = context.SelectSingleNode("//*[@id=\"" + idValue + "\"]") as XmlElement;
            if (idReference != null)
                return idReference;
            return context.SelectSingleNode("//*[@ID=\"" + idValue + "\"]") as XmlElement;
        }

        public override XmlElement GetIdElement(XmlDocument document, string idValue)
        {
            // We only care about Id references inside of the KeyInfo section
            if (_verify)
                return base.GetIdElement(document, idValue);

            KeyInfo keyInfo = this.KeyInfo;
            if (keyInfo.Id != idValue)
                return null;
            return keyInfo.GetXml();
        }
    }

    internal class SignedCmiManifest
    {
        private XmlDocument _manifestDom = null;
        private CmiStrongNameSignerInfo _strongNameSignerInfo = null;
        private CmiAuthenticodeSignerInfo _authenticodeSignerInfo = null;

        private SignedCmiManifest() { }

        internal SignedCmiManifest(XmlDocument manifestDom)
        {
            if (manifestDom == null)
                throw new ArgumentNullException("manifestDom");
            _manifestDom = manifestDom;
        }

        internal void Sign(CmiManifestSigner signer)
        {
            Sign(signer, null);
        }

        internal void Sign(CmiManifestSigner signer, string timeStampUrl)
        {
            // Reset signer infos.
            _strongNameSignerInfo = null;
            _authenticodeSignerInfo = null;

            // Signer cannot be null.
            if (signer == null || signer.StrongNameKey == null)
            {
                throw new ArgumentNullException("signer");
            }

            // Remove existing SN signature.
            RemoveExistingSignature(_manifestDom);

            // Replace public key token in assemblyIdentity if requested.
            if ((signer.Flag & CmiManifestSignerFlag.DontReplacePublicKeyToken) == 0)
            {
                ReplacePublicKeyToken(_manifestDom, signer.StrongNameKey);
            }

            // No cert means don't Authenticode sign and timestamp.
            XmlDocument licenseDom = null;
            if (signer.Certificate != null)
            {
                // Yes. We will Authenticode sign, so first insert <publisherIdentity />
                // element, if necessary.
                InsertPublisherIdentity(_manifestDom, signer.Certificate);

                // Now create the license DOM, and then sign it.
                licenseDom = CreateLicenseDom(signer, ExtractPrincipalFromManifest(), ComputeHashFromManifest(_manifestDom));
                AuthenticodeSignLicenseDom(licenseDom, signer, timeStampUrl);
            }
            StrongNameSignManifestDom(_manifestDom, licenseDom, signer);
        }

        // throw cryptographic exception for any verification errors.
        internal void Verify(CmiManifestVerifyFlags verifyFlags)
        {
            // Reset signer infos.
            _strongNameSignerInfo = null;
            _authenticodeSignerInfo = null;

            XmlNamespaceManager nsm = new XmlNamespaceManager(_manifestDom.NameTable);
            nsm.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
            XmlElement signatureNode = _manifestDom.SelectSingleNode("//ds:Signature", nsm) as XmlElement;
            if (signatureNode == null)
            {
                throw new CryptographicException(Win32.TRUST_E_NOSIGNATURE);
            }

            // Make sure it is indeed SN signature, and it is an enveloped signature.
            string snIdName = "Id";
            if (!signatureNode.HasAttribute(snIdName))
            {
                snIdName = "id";
                if (!signatureNode.HasAttribute(snIdName))
                {
                    snIdName = "ID";
                    if (!signatureNode.HasAttribute(snIdName))
                    {
                        throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
                    }
                }
            }

            string snIdValue = signatureNode.GetAttribute(snIdName);
            if (snIdValue == null ||
                String.Compare(snIdValue, "StrongNameSignature", StringComparison.Ordinal) != 0)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            // Make sure it is indeed an enveloped signature.
            bool oldFormat = false;
            bool validFormat = false;
            XmlNodeList referenceNodes = signatureNode.SelectNodes("ds:SignedInfo/ds:Reference", nsm);
            foreach (XmlNode referenceNode in referenceNodes)
            {
                XmlElement reference = referenceNode as XmlElement;
                if (reference != null && reference.HasAttribute("URI"))
                {
                    string uriValue = reference.GetAttribute("URI");
                    if (uriValue != null)
                    {
                        // We expect URI="" (empty URI value which means to hash the entire document).
                        if (uriValue.Length == 0)
                        {
                            XmlNode transformsNode = reference.SelectSingleNode("ds:Transforms", nsm);
                            if (transformsNode == null)
                            {
                                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
                            }

                            // Make sure the transforms are what we expected.
                            XmlNodeList transforms = transformsNode.SelectNodes("ds:Transform", nsm);
                            if (transforms.Count < 2)
                            {
                                // We expect at least:
                                //  <Transform Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
                                //  <Transform Algorithm="http://www.w3.org/2000/09/xmldsig#enveloped-signature" /> 
                                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
                            }

                            bool c14 = false;
                            bool enveloped = false;
                            for (int i = 0; i < transforms.Count; i++)
                            {
                                XmlElement transform = transforms[i] as XmlElement;
                                string algorithm = transform.GetAttribute("Algorithm");
                                if (algorithm == null)
                                {
                                    break;
                                }
                                else if (String.Compare(algorithm, SignedXml.XmlDsigExcC14NTransformUrl, StringComparison.Ordinal) != 0)
                                {
                                    c14 = true;
                                    if (enveloped)
                                    {
                                        validFormat = true;
                                        break;
                                    }
                                }
                                else if (String.Compare(algorithm, SignedXml.XmlDsigEnvelopedSignatureTransformUrl, StringComparison.Ordinal) != 0)
                                {
                                    enveloped = true;
                                    if (c14)
                                    {
                                        validFormat = true;
                                        break;
                                    }
                                }
                            }
                        }
                        else if (String.Compare(uriValue, "#StrongNameKeyInfo", StringComparison.Ordinal) == 0)
                        {
                            oldFormat = true;

                            XmlNode transformsNode = referenceNode.SelectSingleNode("ds:Transforms", nsm);
                            if (transformsNode == null)
                            {
                                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
                            }

                            // Make sure the transforms are what we expected.
                            XmlNodeList transforms = transformsNode.SelectNodes("ds:Transform", nsm);
                            if (transforms.Count < 1)
                            {
                                // We expect at least:
                                //  <Transform Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
                                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
                            }

                            for (int i = 0; i < transforms.Count; i++)
                            {
                                XmlElement transform = transforms[i] as XmlElement;
                                string algorithm = transform.GetAttribute("Algorithm");
                                if (algorithm == null)
                                {
                                    break;
                                }
                                else if (String.Compare(algorithm, SignedXml.XmlDsigExcC14NTransformUrl, StringComparison.Ordinal) != 0)
                                {
                                    validFormat = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (!validFormat)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            // It is the DSig we want, now make sure the public key matches the token.
            string publicKeyToken = VerifyPublicKeyToken();

            // OK. We found the SN signature with matching public key token, so
            // instantiate the SN signer info property.
            _strongNameSignerInfo = new CmiStrongNameSignerInfo(Win32.TRUST_E_FAIL, publicKeyToken);

            // Now verify the SN signature, and Authenticode license if available.
            ManifestSignedXml signedXml = new ManifestSignedXml(_manifestDom, true);
            signedXml.LoadXml(signatureNode);

            AsymmetricAlgorithm key = null;
            bool dsigValid = signedXml.CheckSignatureReturningKey(out key);
            _strongNameSignerInfo.PublicKey = key;
            if (!dsigValid)
            {
                _strongNameSignerInfo.ErrorCode = Win32.TRUST_E_BAD_DIGEST;
                throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
            }

            // Verify license as well if requested.
            if ((verifyFlags & CmiManifestVerifyFlags.StrongNameOnly) != CmiManifestVerifyFlags.StrongNameOnly)
            {
                VerifyLicense(verifyFlags, oldFormat);
            }
        }

        internal CmiStrongNameSignerInfo StrongNameSignerInfo
        {
            get
            {
                return _strongNameSignerInfo;
            }
        }

        internal CmiAuthenticodeSignerInfo AuthenticodeSignerInfo
        {
            get
            {
                return _authenticodeSignerInfo;
            }
        }

        //
        // Privates.
        //
        private void VerifyLicense(CmiManifestVerifyFlags verifyFlags, bool oldFormat)
        {
            XmlNamespaceManager nsm = new XmlNamespaceManager(_manifestDom.NameTable);
            nsm.AddNamespace("asm", AssemblyNamespaceUri);
            nsm.AddNamespace("asm2", AssemblyV2NamespaceUri);
            nsm.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
            nsm.AddNamespace("msrel", MSRelNamespaceUri);
            nsm.AddNamespace("r", LicenseNamespaceUri);
            nsm.AddNamespace("as", AuthenticodeNamespaceUri);

            // We are done if no license.
            XmlElement licenseNode = _manifestDom.SelectSingleNode("asm:assembly/ds:Signature/ds:KeyInfo/msrel:RelData/r:license", nsm) as XmlElement;
            if (licenseNode == null)
            {
                return;
            }

            // Make sure this license is for this manifest.
            VerifyAssemblyIdentity(nsm);

            // Found a license, so instantiate signer info property.
            _authenticodeSignerInfo = new CmiAuthenticodeSignerInfo(Win32.TRUST_E_FAIL);

            unsafe
            {
                byte[] licenseXml = Encoding.UTF8.GetBytes(licenseNode.OuterXml);
                fixed (byte* pbLicense = licenseXml)
                {
                    Win32.AXL_SIGNER_INFO signerInfo = new Win32.AXL_SIGNER_INFO();
                    signerInfo.cbSize = (uint)Marshal.SizeOf<Win32.AXL_SIGNER_INFO>();
                    Win32.AXL_TIMESTAMPER_INFO timestamperInfo = new Win32.AXL_TIMESTAMPER_INFO();
                    timestamperInfo.cbSize = (uint)Marshal.SizeOf<Win32.AXL_TIMESTAMPER_INFO>();
                    Win32.CRYPT_DATA_BLOB licenseBlob = new Win32.CRYPT_DATA_BLOB();
                    IntPtr pvLicense = new IntPtr(pbLicense);
                    licenseBlob.cbData = (uint)licenseXml.Length;
                    licenseBlob.pbData = pvLicense;

                    int hr = Win32.CertVerifyAuthenticodeLicense(ref licenseBlob, (uint)verifyFlags, ref signerInfo, ref timestamperInfo);
                    if (Win32.TRUST_E_NOSIGNATURE != (int)signerInfo.dwError)
                    {
                        _authenticodeSignerInfo = new CmiAuthenticodeSignerInfo(signerInfo, timestamperInfo);
                    }

                    Win32.CertFreeAuthenticodeSignerInfo(ref signerInfo);
                    Win32.CertFreeAuthenticodeTimestamperInfo(ref timestamperInfo);

                    if (hr != Win32.S_OK)
                    {
                        throw new CryptographicException(hr);
                    }
                }
            }

            if (!oldFormat)
                // Make sure we have the intended Authenticode signer.
                VerifyPublisherIdentity(nsm);
        }

        private XmlElement ExtractPrincipalFromManifest()
        {
            XmlNamespaceManager nsm = new XmlNamespaceManager(_manifestDom.NameTable);
            nsm.AddNamespace("asm", AssemblyNamespaceUri);
            XmlNode assemblyIdentityNode = _manifestDom.SelectSingleNode("asm:assembly/asm:assemblyIdentity", nsm);
            if (assemblyIdentityNode == null)
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            return assemblyIdentityNode as XmlElement;
        }

        private void VerifyAssemblyIdentity(XmlNamespaceManager nsm)
        {
            XmlElement assemblyIdentity = _manifestDom.SelectSingleNode("asm:assembly/asm:assemblyIdentity", nsm) as XmlElement;
            XmlElement principal = _manifestDom.SelectSingleNode("asm:assembly/ds:Signature/ds:KeyInfo/msrel:RelData/r:license/r:grant/as:ManifestInformation/as:assemblyIdentity", nsm) as XmlElement;

            if (assemblyIdentity == null || principal == null ||
                !assemblyIdentity.HasAttributes || !principal.HasAttributes)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            XmlAttributeCollection asmIdAttrs = assemblyIdentity.Attributes;

            if (asmIdAttrs.Count == 0 || asmIdAttrs.Count != principal.Attributes.Count)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            foreach (XmlAttribute asmIdAttr in asmIdAttrs)
            {
                if (!principal.HasAttribute(asmIdAttr.LocalName) ||
                    asmIdAttr.Value != principal.GetAttribute(asmIdAttr.LocalName))
                {
                    throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
                }
            }

            VerifyHash(nsm);
        }

        private void VerifyPublisherIdentity(XmlNamespaceManager nsm)
        {
            // Nothing to do if no signature.
            if (_authenticodeSignerInfo.ErrorCode == Win32.TRUST_E_NOSIGNATURE)
            {
                return;
            }

            X509Certificate2 signerCert = _authenticodeSignerInfo.SignerChain.ChainElements[0].Certificate;

            // Find the publisherIdentity element.
            XmlElement publisherIdentity = _manifestDom.SelectSingleNode("asm:assembly/asm2:publisherIdentity", nsm) as XmlElement;
            if (publisherIdentity == null || !publisherIdentity.HasAttributes)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            // Get name and issuerKeyHash attribute values.
            if (!publisherIdentity.HasAttribute("name") || !publisherIdentity.HasAttribute("issuerKeyHash"))
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            string publisherName = publisherIdentity.GetAttribute("name");
            string publisherIssuerKeyHash = publisherIdentity.GetAttribute("issuerKeyHash");

            // Calculate the issuer key hash.
            IntPtr pIssuerKeyHash = new IntPtr();
            int hr = Win32._AxlGetIssuerPublicKeyHash(signerCert.Handle, ref pIssuerKeyHash);
            if (hr != Win32.S_OK)
            {
                throw new CryptographicException(hr);
            }

            string issuerKeyHash = Marshal.PtrToStringUni(pIssuerKeyHash);
            Win32.HeapFree(Win32.GetProcessHeap(), 0, pIssuerKeyHash);

            // Make sure name and issuerKeyHash match.
            if (String.Compare(publisherName, signerCert.SubjectName.Name, StringComparison.Ordinal) != 0 ||
                String.Compare(publisherIssuerKeyHash, issuerKeyHash, StringComparison.Ordinal) != 0)
            {
                throw new CryptographicException(Win32.TRUST_E_FAIL);
            }
        }

        private void VerifyHash(XmlNamespaceManager nsm)
        {
            XmlDocument manifestDom = new XmlDocument();
            // We always preserve white space as Fusion XML engine always preserve white space.
            manifestDom.PreserveWhitespace = true;
            manifestDom = (XmlDocument)_manifestDom.Clone();

            XmlElement manifestInformation = manifestDom.SelectSingleNode("asm:assembly/ds:Signature/ds:KeyInfo/msrel:RelData/r:license/r:grant/as:ManifestInformation", nsm) as XmlElement;
            if (manifestInformation == null)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            if (!manifestInformation.HasAttribute("Hash"))
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            string hash = manifestInformation.GetAttribute("Hash");
            if (hash == null || hash.Length == 0)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            // Now compute the hash for the manifest without the entire SN
            // signature element.

            // First remove the Signture element from the DOM.
            XmlElement dsElement = manifestDom.SelectSingleNode("asm:assembly/ds:Signature", nsm) as XmlElement;
            if (dsElement == null)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            dsElement.ParentNode.RemoveChild(dsElement);

            // Now compute the hash from the manifest, without the Signature element.
            byte[] hashBytes = HexStringToBytes(manifestInformation.GetAttribute("Hash"));
            byte[] computedHashBytes = ComputeHashFromManifest(manifestDom);

            // Do they match?
            if (hashBytes.Length == 0 || hashBytes.Length != computedHashBytes.Length)
            {
                byte[] computedOldHashBytes = ComputeHashFromManifest(manifestDom, true);

                // Do they match?
                if (hashBytes.Length == 0 || hashBytes.Length != computedOldHashBytes.Length)
                {
                    throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
                }

                for (int i = 0; i < hashBytes.Length; i++)
                {
                    if (hashBytes[i] != computedOldHashBytes[i])
                    {
                        throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
                    }
                }
            }

            for (int i = 0; i < hashBytes.Length; i++)
            {
                if (hashBytes[i] != computedHashBytes[i])
                {
#if (true) // BUGBUG: Remove before RTM once old format support is no longer needed.
                    byte[] computedOldHashBytes = ComputeHashFromManifest(manifestDom, true);

                    // Do they match?
                    if (hashBytes.Length == 0 || hashBytes.Length != computedOldHashBytes.Length)
                    {
                        throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
                    }

                    for (i = 0; i < hashBytes.Length; i++)
                    {
                        if (hashBytes[i] != computedOldHashBytes[i])
                        {
                            throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
                        }
                    }
#else
                throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
#endif
                }
            }
        }

        private string VerifyPublicKeyToken()
        {
            XmlNamespaceManager nsm = new XmlNamespaceManager(_manifestDom.NameTable);
            nsm.AddNamespace("asm", AssemblyNamespaceUri);
            nsm.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);

            XmlElement snModulus = _manifestDom.SelectSingleNode("asm:assembly/ds:Signature/ds:KeyInfo/ds:KeyValue/ds:RSAKeyValue/ds:Modulus", nsm) as XmlElement;
            XmlElement snExponent = _manifestDom.SelectSingleNode("asm:assembly/ds:Signature/ds:KeyInfo/ds:KeyValue/ds:RSAKeyValue/ds:Exponent", nsm) as XmlElement;

            if (snModulus == null || snExponent == null)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            byte[] modulus = Encoding.UTF8.GetBytes(snModulus.InnerXml);
            byte[] exponent = Encoding.UTF8.GetBytes(snExponent.InnerXml);

            string tokenString = GetPublicKeyToken(_manifestDom);
            byte[] publicKeyToken = HexStringToBytes(tokenString);
            byte[] computedPublicKeyToken;

            unsafe
            {
                fixed (byte* pbModulus = modulus)
                {
                    fixed (byte* pbExponent = exponent)
                    {
                        Win32.CRYPT_DATA_BLOB modulusBlob = new Win32.CRYPT_DATA_BLOB();
                        Win32.CRYPT_DATA_BLOB exponentBlob = new Win32.CRYPT_DATA_BLOB();
                        IntPtr pComputedToken = new IntPtr();

                        modulusBlob.cbData = (uint)modulus.Length;
                        modulusBlob.pbData = new IntPtr(pbModulus);
                        exponentBlob.cbData = (uint)exponent.Length;
                        exponentBlob.pbData = new IntPtr(pbExponent);

                        // Now compute the public key token.
                        int hr = Win32._AxlRSAKeyValueToPublicKeyToken(ref modulusBlob, ref exponentBlob, ref pComputedToken);
                        if (hr != Win32.S_OK)
                        {
                            throw new CryptographicException(hr);
                        }

                        computedPublicKeyToken = HexStringToBytes(Marshal.PtrToStringUni(pComputedToken));
                        Win32.HeapFree(Win32.GetProcessHeap(), 0, pComputedToken);
                    }
                }
            }

            // Do they match?
            if (publicKeyToken.Length == 0 || publicKeyToken.Length != computedPublicKeyToken.Length)
            {
                throw new CryptographicException(Win32.TRUST_E_FAIL);
            }

            for (int i = 0; i < publicKeyToken.Length; i++)
            {
                if (publicKeyToken[i] != computedPublicKeyToken[i])
                {
                    throw new CryptographicException(Win32.TRUST_E_FAIL);
                }
            }

            return tokenString;
        }

        //
        // Statics.
        //
        private static void InsertPublisherIdentity(XmlDocument manifestDom, X509Certificate2 signerCert)
        {
            XmlNamespaceManager nsm = new XmlNamespaceManager(manifestDom.NameTable);
            nsm.AddNamespace("asm", AssemblyNamespaceUri);
            nsm.AddNamespace("asm2", AssemblyV2NamespaceUri);
            nsm.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);

            XmlElement assembly = manifestDom.SelectSingleNode("asm:assembly", nsm) as XmlElement;
            XmlElement assemblyIdentity = manifestDom.SelectSingleNode("asm:assembly/asm:assemblyIdentity", nsm) as XmlElement;
            if (assemblyIdentity == null)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            // Reuse existing node if exists
            XmlElement publisherIdentity = manifestDom.SelectSingleNode("asm:assembly/asm2:publisherIdentity", nsm) as XmlElement;
            if (publisherIdentity == null)
            {
                // create new if not exist
                publisherIdentity = manifestDom.CreateElement("publisherIdentity", AssemblyV2NamespaceUri);
            }
            // Get the issuer's public key blob hash.
            IntPtr pIssuerKeyHash = new IntPtr();
            int hr = Win32._AxlGetIssuerPublicKeyHash(signerCert.Handle, ref pIssuerKeyHash);
            if (hr != Win32.S_OK)
            {
                throw new CryptographicException(hr);
            }

            string issuerKeyHash = Marshal.PtrToStringUni(pIssuerKeyHash);
            Win32.HeapFree(Win32.GetProcessHeap(), 0, pIssuerKeyHash);

            publisherIdentity.SetAttribute("name", signerCert.SubjectName.Name);
            publisherIdentity.SetAttribute("issuerKeyHash", issuerKeyHash);

            XmlElement signature = manifestDom.SelectSingleNode("asm:assembly/ds:Signature", nsm) as XmlElement;
            if (signature != null)
            {
                assembly.InsertBefore(publisherIdentity, signature);
            }
            else
            {
                assembly.AppendChild(publisherIdentity);
            }
        }

        private static void RemoveExistingSignature(XmlDocument manifestDom)
        {
            XmlNamespaceManager nsm = new XmlNamespaceManager(manifestDom.NameTable);
            nsm.AddNamespace("asm", AssemblyNamespaceUri);
            nsm.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
            XmlNode signatureNode = manifestDom.SelectSingleNode("asm:assembly/ds:Signature", nsm);
            if (signatureNode != null)
                signatureNode.ParentNode.RemoveChild(signatureNode);
        }

        private static void ReplacePublicKeyToken(XmlDocument manifestDom, AsymmetricAlgorithm snKey)
        {
            // Make sure we can find the publicKeyToken attribute.
            XmlNamespaceManager nsm = new XmlNamespaceManager(manifestDom.NameTable);
            nsm.AddNamespace("asm", AssemblyNamespaceUri);
            XmlElement assemblyIdentity = manifestDom.SelectSingleNode("asm:assembly/asm:assemblyIdentity", nsm) as XmlElement;
            if (assemblyIdentity == null)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            if (!assemblyIdentity.HasAttribute("publicKeyToken"))
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            byte[] cspPublicKeyBlob = ((RSACryptoServiceProvider)snKey).ExportCspBlob(false);
            if (cspPublicKeyBlob == null || cspPublicKeyBlob.Length == 0)
            {
                throw new CryptographicException(Win32.NTE_BAD_KEY);
            }

            // Now compute the public key token.
            unsafe
            {
                fixed (byte* pbPublicKeyBlob = cspPublicKeyBlob)
                {
                    Win32.CRYPT_DATA_BLOB publicKeyBlob = new Win32.CRYPT_DATA_BLOB();
                    publicKeyBlob.cbData = (uint)cspPublicKeyBlob.Length;
                    publicKeyBlob.pbData = new IntPtr(pbPublicKeyBlob);
                    IntPtr pPublicKeyToken = new IntPtr();

                    int hr = Win32._AxlPublicKeyBlobToPublicKeyToken(ref publicKeyBlob, ref pPublicKeyToken);
                    if (hr != Win32.S_OK)
                    {
                        throw new CryptographicException(hr);
                    }

                    string publicKeyToken = Marshal.PtrToStringUni(pPublicKeyToken);
                    Win32.HeapFree(Win32.GetProcessHeap(), 0, pPublicKeyToken);

                    assemblyIdentity.SetAttribute("publicKeyToken", publicKeyToken);
                }
            }
        }

        private static string GetPublicKeyToken(XmlDocument manifestDom)
        {
            XmlNamespaceManager nsm = new XmlNamespaceManager(manifestDom.NameTable);
            nsm.AddNamespace("asm", AssemblyNamespaceUri);
            nsm.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);

            XmlElement assemblyIdentity = manifestDom.SelectSingleNode("asm:assembly/asm:assemblyIdentity", nsm) as XmlElement;

            if (assemblyIdentity == null || !assemblyIdentity.HasAttribute("publicKeyToken"))
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            return assemblyIdentity.GetAttribute("publicKeyToken");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Cryptographic.Standard", "CA5354:SHA1CannotBeUsed", Justification = "SHA1 is retained for compatibility reasons as an option in VisualStudio signing page and consequently in the trust manager, default is SHA2.")]
        private static byte[] ComputeHashFromManifest(XmlDocument manifestDom)
        {
            return ComputeHashFromManifest(manifestDom, false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Cryptographic.Standard", "CA5354:SHA1CannotBeUsed", Justification = "SHA1 is retained for compatibility reasons as an option in VisualStudio signing page and consequently in the trust manager, default is SHA2.")]
        private static byte[] ComputeHashFromManifest(XmlDocument manifestDom, bool oldFormat)
        {
            if (oldFormat)
            {
                XmlDsigExcC14NTransform exc = new XmlDsigExcC14NTransform();
                exc.LoadInput(manifestDom);
                using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
                {
                    byte[] hash = sha1.ComputeHash(exc.GetOutput() as MemoryStream);
                    if (hash == null)
                    {
                        throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
                    }

                    return hash;
                }
            }
            else
            {
                // Since the DOM given to us is not guaranteed to be normalized,
                // we need to normalize it ourselves. Also, we always preserve
                // white space as Fusion XML engine always preserve white space.
                XmlDocument normalizedDom = new XmlDocument();
                normalizedDom.PreserveWhitespace = true;

                // Normalize the document
                using (TextReader stringReader = new StringReader(manifestDom.OuterXml))
                {
                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.DtdProcessing = DtdProcessing.Parse;
                    XmlReader reader = XmlReader.Create(stringReader, settings, manifestDom.BaseURI);
                    normalizedDom.Load(reader);
                }

                XmlDsigExcC14NTransform exc = new XmlDsigExcC14NTransform();
                exc.LoadInput(normalizedDom);
                using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
                {
                    byte[] hash = sha1.ComputeHash(exc.GetOutput() as MemoryStream);
                    if (hash == null)
                    {
                        throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
                    }

                    return hash;
                }
            }
        }

        private const string AssemblyNamespaceUri = "urn:schemas-microsoft-com:asm.v1";
        private const string AssemblyV2NamespaceUri = "urn:schemas-microsoft-com:asm.v2";
        private const string MSRelNamespaceUri = "http://schemas.microsoft.com/windows/rel/2005/reldata";
        private const string LicenseNamespaceUri = "urn:mpeg:mpeg21:2003:01-REL-R-NS";
        private const string AuthenticodeNamespaceUri = "http://schemas.microsoft.com/windows/pki/2005/Authenticode";
        private const string licenseTemplate = "<r:license xmlns:r=\"" + LicenseNamespaceUri + "\" xmlns:as=\"" + AuthenticodeNamespaceUri + "\">" +
                                                    @"<r:grant>" +
                                                    @"<as:ManifestInformation>" +
                                                    @"<as:assemblyIdentity />" +
                                                    @"</as:ManifestInformation>" +
                                                    @"<as:SignedBy/>" +
                                                    @"<as:AuthenticodePublisher>" +
                                                    @"<as:X509SubjectName>CN=dummy</as:X509SubjectName>" +
                                                    @"</as:AuthenticodePublisher>" +
                                                    @"</r:grant><r:issuer></r:issuer></r:license>";

        private static XmlDocument CreateLicenseDom(CmiManifestSigner signer, XmlElement principal, byte[] hash)
        {
            XmlDocument licenseDom = new XmlDocument();
            licenseDom.PreserveWhitespace = true;
            licenseDom.LoadXml(licenseTemplate);
            XmlNamespaceManager nsm = new XmlNamespaceManager(licenseDom.NameTable);
            nsm.AddNamespace("r", LicenseNamespaceUri);
            nsm.AddNamespace("as", AuthenticodeNamespaceUri);
            XmlElement assemblyIdentityNode = licenseDom.SelectSingleNode("r:license/r:grant/as:ManifestInformation/as:assemblyIdentity", nsm) as XmlElement;
            assemblyIdentityNode.RemoveAllAttributes();
            foreach (XmlAttribute attribute in principal.Attributes)
            {
                assemblyIdentityNode.SetAttribute(attribute.Name, attribute.Value);
            }

            XmlElement manifestInformationNode = licenseDom.SelectSingleNode("r:license/r:grant/as:ManifestInformation", nsm) as XmlElement;

            manifestInformationNode.SetAttribute("Hash", hash.Length == 0 ? "" : BytesToHexString(hash, 0, hash.Length));
            manifestInformationNode.SetAttribute("Description", signer.Description == null ? "" : signer.Description);
            manifestInformationNode.SetAttribute("Url", signer.DescriptionUrl == null ? "" : signer.DescriptionUrl);

            XmlElement authenticodePublisherNode = licenseDom.SelectSingleNode("r:license/r:grant/as:AuthenticodePublisher/as:X509SubjectName", nsm) as XmlElement;
            authenticodePublisherNode.InnerText = signer.Certificate.SubjectName.Name;

            return licenseDom;
        }

        private static void AuthenticodeSignLicenseDom(XmlDocument licenseDom, CmiManifestSigner signer, string timeStampUrl)
        {
            // Make sure it is RSA, as this is the only one Fusion will support.
            if (signer.Certificate.PublicKey.Key.GetType() != typeof(RSACryptoServiceProvider))
            {
                throw new NotSupportedException();
            }

            // Setup up XMLDSIG engine.
            ManifestSignedXml signedXml = new ManifestSignedXml(licenseDom);
            signedXml.SigningKey = signer.Certificate.PrivateKey;
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;

            // Add the key information.
            signedXml.KeyInfo.AddClause(new RSAKeyValue(signer.Certificate.PublicKey.Key as RSA));
            signedXml.KeyInfo.AddClause(new KeyInfoX509Data(signer.Certificate, signer.IncludeOption));

            // Add the enveloped reference.
            Reference reference = new Reference();
            reference.Uri = "";

            // Add an enveloped and an Exc-C14N transform.
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.AddTransform(new XmlDsigExcC14NTransform());

            // Add the reference.
            signedXml.AddReference(reference);

            // Compute the signature.
            signedXml.ComputeSignature();

            // Get the XML representation
            XmlElement xmlDigitalSignature = signedXml.GetXml();
            xmlDigitalSignature.SetAttribute("Id", "AuthenticodeSignature");

            // Insert the signature node under the issuer element.
            XmlNamespaceManager nsm = new XmlNamespaceManager(licenseDom.NameTable);
            nsm.AddNamespace("r", LicenseNamespaceUri);
            XmlElement issuerNode = licenseDom.SelectSingleNode("r:license/r:issuer", nsm) as XmlElement;
            issuerNode.AppendChild(licenseDom.ImportNode(xmlDigitalSignature, true));

            // Time stamp it if requested.
            if (timeStampUrl != null && timeStampUrl.Length != 0)
            {
                TimestampSignedLicenseDom(licenseDom, timeStampUrl);
            }

            // Wrap it inside a RelData element.
            licenseDom.DocumentElement.ParentNode.InnerXml = "<msrel:RelData xmlns:msrel=\"" +
                                                             MSRelNamespaceUri + "\">" +
                                                             licenseDom.OuterXml + "</msrel:RelData>";
        }

        private static void TimestampSignedLicenseDom(XmlDocument licenseDom, string timeStampUrl)
        {
            Win32.CRYPT_DATA_BLOB timestampBlob = new Win32.CRYPT_DATA_BLOB();

            XmlNamespaceManager nsm = new XmlNamespaceManager(licenseDom.NameTable);
            nsm.AddNamespace("r", LicenseNamespaceUri);
            nsm.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
            nsm.AddNamespace("as", AuthenticodeNamespaceUri);

            byte[] licenseXml = Encoding.UTF8.GetBytes(licenseDom.OuterXml);

            unsafe
            {
                fixed (byte* pbLicense = licenseXml)
                {
                    Win32.CRYPT_DATA_BLOB licenseBlob = new Win32.CRYPT_DATA_BLOB();
                    IntPtr pvLicense = new IntPtr(pbLicense);
                    licenseBlob.cbData = (uint)licenseXml.Length;
                    licenseBlob.pbData = pvLicense;

                    int hr = Win32.CertTimestampAuthenticodeLicense(ref licenseBlob, timeStampUrl, ref timestampBlob);
                    if (hr != Win32.S_OK)
                    {
                        throw new CryptographicException(hr);
                    }
                }
            }

            byte[] timestampSignature = new byte[timestampBlob.cbData];
            Marshal.Copy(timestampBlob.pbData, timestampSignature, 0, timestampSignature.Length);
            Win32.HeapFree(Win32.GetProcessHeap(), 0, timestampBlob.pbData);

            XmlElement asTimestamp = licenseDom.CreateElement("as", "Timestamp", AuthenticodeNamespaceUri);
            asTimestamp.InnerText = Encoding.UTF8.GetString(timestampSignature);

            XmlElement dsObject = licenseDom.CreateElement("Object", SignedXml.XmlDsigNamespaceUrl);
            dsObject.AppendChild(asTimestamp);

            XmlElement signatureNode = licenseDom.SelectSingleNode("r:license/r:issuer/ds:Signature", nsm) as XmlElement;
            signatureNode.AppendChild(dsObject);
        }

        private static void StrongNameSignManifestDom(XmlDocument manifestDom, XmlDocument licenseDom, CmiManifestSigner signer)
        {
            RSA snKey = signer.StrongNameKey as RSA;

            // Make sure it is RSA, as this is the only one Fusion will support.
            if (snKey == null)
            {
                throw new NotSupportedException();
            }

            // Setup namespace manager.
            XmlNamespaceManager nsm = new XmlNamespaceManager(manifestDom.NameTable);
            nsm.AddNamespace("asm", AssemblyNamespaceUri);

            // Get to root element.
            XmlElement signatureParent = manifestDom.SelectSingleNode("asm:assembly", nsm) as XmlElement;
            if (signatureParent == null)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            // Setup up XMLDSIG engine.
            ManifestSignedXml signedXml = new ManifestSignedXml(signatureParent);
            signedXml.SigningKey = signer.StrongNameKey;
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;

            // Add the key information.
            signedXml.KeyInfo.AddClause(new RSAKeyValue(snKey));
            if (licenseDom != null)
            {
                signedXml.KeyInfo.AddClause(new KeyInfoNode(licenseDom.DocumentElement));
            }
            signedXml.KeyInfo.Id = "StrongNameKeyInfo";

            // Add the enveloped reference.
            Reference enveloped = new Reference();
            enveloped.Uri = "";

            // Add an enveloped then Exc-C14N transform.
            enveloped.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            enveloped.AddTransform(new XmlDsigExcC14NTransform());
            signedXml.AddReference(enveloped);

#if (false) // DSIE: New format does not sign KeyInfo.
            // Add the key info reference.
            Reference strongNameKeyInfo = new Reference();
            strongNameKeyInfo.Uri = "#StrongNameKeyInfo";
            strongNameKeyInfo.AddTransform(new XmlDsigExcC14NTransform());
            signedXml.AddReference(strongNameKeyInfo);
#endif
            // Compute the signature.
            signedXml.ComputeSignature();

            // Get the XML representation
            XmlElement xmlDigitalSignature = signedXml.GetXml();
            xmlDigitalSignature.SetAttribute("Id", "StrongNameSignature");

            // Insert the signature now.
            signatureParent.AppendChild(xmlDigitalSignature);
        }
        private static readonly char[] s_hexValues = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

        private static string BytesToHexString(byte[] array, int start, int end)
        {
            string result = null;
            if (array != null)
            {
                char[] hexOrder = new char[(end - start) * 2];
                int i = end;
                int digit, j = 0;
                while (i-- > start)
                {
                    digit = (array[i] & 0xf0) >> 4;
                    hexOrder[j++] = s_hexValues[digit];
                    digit = (array[i] & 0x0f);
                    hexOrder[j++] = s_hexValues[digit];
                }
                result = new String(hexOrder);
            }
            return result;
        }

        private static byte[] HexStringToBytes(string hexString)
        {
            uint cbHex = (uint)hexString.Length / 2;
            byte[] hex = new byte[cbHex];
            int i = hexString.Length - 2;
            for (int index = 0; index < cbHex; index++)
            {
                hex[index] = (byte)((HexToByte(hexString[i]) << 4) | HexToByte(hexString[i + 1]));
                i -= 2;
            }
            return hex;
        }

        private static byte HexToByte(char val)
        {
            if (val <= '9' && val >= '0')
                return (byte)(val - '0');
            else if (val >= 'a' && val <= 'f')
                return (byte)((val - 'a') + 10);
            else if (val >= 'A' && val <= 'F')
                return (byte)((val - 'A') + 10);
            else
                return 0xFF;
        }
    }

    [Flags]
    internal enum CmiManifestSignerFlag
    {
        None = 0x00000000,
        DontReplacePublicKeyToken = 0x00000001
    }

    [Flags]
    internal enum CmiManifestVerifyFlags
    {
        None = 0x00000000,
        RevocationNoCheck = 0x00000001,
        RevocationCheckEndCertOnly = 0x00000002,
        RevocationCheckEntireChain = 0x00000004,
        UrlCacheOnlyRetrieval = 0x00000008,
        LifetimeSigning = 0x00000010,
        TrustMicrosoftRootOnly = 0x00000020,
        StrongNameOnly = 0x00010000
    }

    internal class CmiManifestSigner
    {
        private AsymmetricAlgorithm _strongNameKey;
        private X509Certificate2 _certificate;
        private string _description;
        private string _url;
        private X509Certificate2Collection _certificates;
        private X509IncludeOption _includeOption;
        private CmiManifestSignerFlag _signerFlag;

        private CmiManifestSigner() { }

        internal CmiManifestSigner(AsymmetricAlgorithm strongNameKey) :
            this(strongNameKey, null)
        { }

        internal CmiManifestSigner(AsymmetricAlgorithm strongNameKey, X509Certificate2 certificate)
        {
            if (strongNameKey == null)
                throw new ArgumentNullException("strongNameKey");

            RSA rsa = strongNameKey as RSA;
            if (rsa == null)
                throw new ArgumentNullException("strongNameKey");
            _strongNameKey = strongNameKey;
            _certificate = certificate;
            _certificates = new X509Certificate2Collection();
            _includeOption = X509IncludeOption.ExcludeRoot;
            _signerFlag = CmiManifestSignerFlag.None;
        }

        internal AsymmetricAlgorithm StrongNameKey
        {
            get
            {
                return _strongNameKey;
            }
        }

        internal X509Certificate2 Certificate
        {
            get
            {
                return _certificate;
            }
        }

        internal string Description
        {
            get
            {
                return _description;
            }
            set
            {
                _description = value;
            }
        }

        internal string DescriptionUrl
        {
            get
            {
                return _url;
            }
            set
            {
                _url = value;
            }
        }

        internal X509Certificate2Collection ExtraStore
        {
            get
            {
                return _certificates;
            }
        }

        internal X509IncludeOption IncludeOption
        {
            get
            {
                return _includeOption;
            }
            set
            {
                if (value < X509IncludeOption.None || value > X509IncludeOption.WholeChain)
                    throw new ArgumentException("value");
                if (_includeOption == X509IncludeOption.None)
                    throw new NotSupportedException();
                _includeOption = value;
            }
        }

        internal CmiManifestSignerFlag Flag
        {
            get
            {
                return _signerFlag;
            }
            set
            {
                unchecked
                {
                    if ((value & ((CmiManifestSignerFlag)~CimManifestSignerFlagMask)) != 0)
                        throw new ArgumentException("value");
                }
                _signerFlag = value;
            }
        }

        internal const uint CimManifestSignerFlagMask = (uint)0x00000001;
    }

    internal class CmiStrongNameSignerInfo
    {
        private int _error = 0;
        private string _publicKeyToken = null;
        private AsymmetricAlgorithm _snKey = null;

        internal CmiStrongNameSignerInfo() { }

        internal CmiStrongNameSignerInfo(int errorCode, string publicKeyToken)
        {
            _error = errorCode;
            _publicKeyToken = publicKeyToken;
        }

        internal int ErrorCode
        {
            get
            {
                return _error;
            }

            set
            {
                _error = value;
            }
        }

        internal string PublicKeyToken
        {
            get
            {
                return _publicKeyToken;
            }

            set
            {
                _publicKeyToken = value;
            }
        }

        internal AsymmetricAlgorithm PublicKey
        {
            get
            {
                return _snKey;
            }

            set
            {
                _snKey = value;
            }
        }
    }

    internal class CmiAuthenticodeSignerInfo
    {
        private int _error = 0;
        private X509Chain _signerChain = null;
        private uint _algHash = 0;
        private string _hash = null;
        private string _description = null;
        private string _descriptionUrl = null;
        private CmiAuthenticodeTimestamperInfo _timestamperInfo = null;

        internal CmiAuthenticodeSignerInfo() { }

        internal CmiAuthenticodeSignerInfo(int errorCode)
        {
            _error = errorCode;
        }

        internal CmiAuthenticodeSignerInfo(Win32.AXL_SIGNER_INFO signerInfo,
                                            Win32.AXL_TIMESTAMPER_INFO timestamperInfo)
        {
            _error = (int)signerInfo.dwError;
            if (signerInfo.pChainContext != IntPtr.Zero)
            {
                _signerChain = new X509Chain(signerInfo.pChainContext);
            }

            _algHash = signerInfo.algHash;
            if (signerInfo.pwszHash != IntPtr.Zero)
            {
                _hash = Marshal.PtrToStringUni(signerInfo.pwszHash);
            }
            if (signerInfo.pwszDescription != IntPtr.Zero)
            {
                _description = Marshal.PtrToStringUni(signerInfo.pwszDescription);
            }
            if (signerInfo.pwszDescriptionUrl != IntPtr.Zero)
            {
                _descriptionUrl = Marshal.PtrToStringUni(signerInfo.pwszDescriptionUrl);
            }
            if ((int)timestamperInfo.dwError != Win32.TRUST_E_NOSIGNATURE)
            {
                _timestamperInfo = new CmiAuthenticodeTimestamperInfo(timestamperInfo);
            }
        }

        internal int ErrorCode
        {
            get
            {
                return _error;
            }
            set
            {
                _error = value;
            }
        }

        internal uint HashAlgId
        {
            get
            {
                return _algHash;
            }
            set
            {
                _algHash = value;
            }
        }

        internal string Hash
        {
            get
            {
                return _hash;
            }
            set
            {
                _hash = value;
            }
        }

        internal string Description
        {
            get
            {
                return _description;
            }
            set
            {
                _description = value;
            }
        }

        internal string DescriptionUrl
        {
            get
            {
                return _descriptionUrl;
            }
            set
            {
                _descriptionUrl = value;
            }
        }

        internal CmiAuthenticodeTimestamperInfo TimestamperInfo
        {
            get
            {
                return _timestamperInfo;
            }
        }

        internal X509Chain SignerChain
        {
            get
            {
                return _signerChain;
            }
            set
            {
                _signerChain = value;
            }
        }
    }

    internal class CmiAuthenticodeTimestamperInfo
    {
        private int _error = 0;
        private X509Chain _timestamperChain = null;
        private DateTime _timestampTime;
        private uint _algHash = 0;

        private CmiAuthenticodeTimestamperInfo() { }

        internal CmiAuthenticodeTimestamperInfo(Win32.AXL_TIMESTAMPER_INFO timestamperInfo)
        {
            _error = (int)timestamperInfo.dwError;
            _algHash = timestamperInfo.algHash;
            long dt = (((long)(uint)timestamperInfo.ftTimestamp.dwHighDateTime) << 32) | ((long)(uint)timestamperInfo.ftTimestamp.dwLowDateTime);
            _timestampTime = DateTime.FromFileTime(dt);
            if (timestamperInfo.pChainContext != IntPtr.Zero)
            {
                _timestamperChain = new X509Chain(timestamperInfo.pChainContext);
            }
        }

        internal int ErrorCode
        {
            get
            {
                return _error;
            }
        }

        internal uint HashAlgId
        {
            get
            {
                return _algHash;
            }
        }

        internal DateTime TimestampTime
        {
            get
            {
                return _timestampTime;
            }
        }

        internal X509Chain TimestamperChain
        {
            get
            {
                return _timestamperChain;
            }
        }
    }
}

