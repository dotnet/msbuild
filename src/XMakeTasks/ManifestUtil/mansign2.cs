// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


//
// mansign.cs
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
using System.Security.Cryptography.Pkcs;
using Microsoft.Win32;

using _FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace System.Deployment.Internal.CodeSigning
{
    internal class ManifestSignedXml2 : SignedXml
    {
        private bool _verify = false;
        private const string Sha256SignatureMethodUri = @"http://www.w3.org/2000/09/xmldsig#rsa-sha256";
        private const string Sha256DigestMethod = @"http://www.w3.org/2000/09/xmldsig#sha256";

        internal ManifestSignedXml2()
            : base()
        {
            init();
        }
        internal ManifestSignedXml2(XmlElement elem)
            : base(elem)
        {
            init();
        }
        internal ManifestSignedXml2(XmlDocument document)
            : base(document)
        {
            init();
        }

        internal ManifestSignedXml2(XmlDocument document, bool verify)
            : base(document)
        {
            _verify = verify;
            init();
        }

        private void init()
        {
            CryptoConfig.AddAlgorithm(typeof(RSAPKCS1SHA256SignatureDescription),
                               Sha256SignatureMethodUri);

            CryptoConfig.AddAlgorithm(typeof(System.Security.Cryptography.SHA256Cng),
                               Sha256DigestMethod);
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

    internal class SignedCmiManifest2
    {
        private XmlDocument _manifestDom = null;
        private CmiStrongNameSignerInfo _strongNameSignerInfo = null;
        private CmiAuthenticodeSignerInfo _authenticodeSignerInfo = null;
        private bool _useSha256;

        private const string Sha256SignatureMethodUri = @"http://www.w3.org/2000/09/xmldsig#rsa-sha256";
        private const string Sha256DigestMethod = @"http://www.w3.org/2000/09/xmldsig#sha256";

        private const string wintrustPolicyFlagsRegPath = "Software\\Microsoft\\Windows\\CurrentVersion\\WinTrust\\Trust Providers\\Software Publishing";
        private const string wintrustPolicyFlagsRegName = "State";

        private SignedCmiManifest2() { }

        internal SignedCmiManifest2(XmlDocument manifestDom, bool useSha256)
        {
            if (manifestDom == null)
                throw new ArgumentNullException("manifestDom");
            _manifestDom = manifestDom;
            _useSha256 = useSha256;
        }

        internal void Sign(CmiManifestSigner2 signer)
        {
            Sign(signer, null);
        }

        internal void Sign(CmiManifestSigner2 signer, string timeStampUrl)
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
                ReplacePublicKeyToken(_manifestDom, signer.StrongNameKey, _useSha256);
            }

            // No cert means don't Authenticode sign and timestamp.
            XmlDocument licenseDom = null;
            if (signer.Certificate != null)
            {
                // Yes. We will Authenticode sign, so first insert <publisherIdentity />
                // element, if necessary.
                InsertPublisherIdentity(_manifestDom, signer.Certificate);

                // Now create the license DOM, and then sign it.
                licenseDom = CreateLicenseDom(signer, ExtractPrincipalFromManifest(), ComputeHashFromManifest(_manifestDom, _useSha256));
                AuthenticodeSignLicenseDom(licenseDom, signer, timeStampUrl, _useSha256);
            }
            StrongNameSignManifestDom(_manifestDom, licenseDom, signer, _useSha256);
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
            bool oldFormat = VerifySignatureForm(signatureNode, "StrongNameSignature", nsm);

            // It is the DSig we want, now make sure the public key matches the token.
            string publicKeyToken = VerifyPublicKeyToken();

            // OK. We found the SN signature with matching public key token, so
            // instantiate the SN signer info property.
            _strongNameSignerInfo = new CmiStrongNameSignerInfo(Win32.TRUST_E_FAIL, publicKeyToken);

            // Now verify the SN signature, and Authenticode license if available.
            ManifestSignedXml2 signedXml = new ManifestSignedXml2(_manifestDom, true);
            signedXml.LoadXml(signatureNode);
            if (_useSha256)
            {
                signedXml.SignedInfo.SignatureMethod = Sha256SignatureMethodUri;
            }

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
                if (_useSha256)
                {
                    VerifyLicenseNew(verifyFlags, oldFormat);
                }
                else
                {
                    VerifyLicense(verifyFlags, oldFormat);
                }
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

        // can be used with sha1 or sha2 
        // logic is copied from the "isolation library" in NDP\iso_whid\ds\security\cryptoapi\pkisign\msaxlapi\mansign.cpp
        private void VerifyLicenseNew(CmiManifestVerifyFlags verifyFlags, bool oldFormat)
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

            // Find the license's signature
            XmlElement signatureNode = licenseNode.SelectSingleNode("//r:issuer/ds:Signature", nsm) as XmlElement;
            if (signatureNode == null)
            {
                throw new CryptographicException(Win32.TRUST_E_NOSIGNATURE);
            }

            // Make sure it is indeed an Authenticode signature, and it is an enveloped signature.
            // Then make sure the transforms are valid.
            VerifySignatureForm(signatureNode, "AuthenticodeSignature", nsm);

            // Now read the enveloped license signature.
            XmlDocument licenseDom = new XmlDocument();
            licenseDom.LoadXml(licenseNode.OuterXml);
            signatureNode = licenseDom.SelectSingleNode("//r:issuer/ds:Signature", nsm) as XmlElement;

            ManifestSignedXml2 signedXml = new ManifestSignedXml2(licenseDom);
            signedXml.LoadXml(signatureNode);
            if (_useSha256)
            {
                signedXml.SignedInfo.SignatureMethod = Sha256SignatureMethodUri;
            }

            // Check the signature
            if (!signedXml.CheckSignature())
            {
                _authenticodeSignerInfo = null;
                throw new CryptographicException(Win32.TRUST_E_CERT_SIGNATURE);
            }

            X509Certificate2 signingCertificate = GetSigningCertificate(signedXml, nsm);

            // First make sure certificate is not explicitly disallowed.
            X509Store store = new X509Store(StoreName.Disallowed, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            X509Certificate2Collection storedCertificates = null;
            try
            {
                storedCertificates = (X509Certificate2Collection)store.Certificates;
                if (storedCertificates == null)
                {
                    _authenticodeSignerInfo.ErrorCode = Win32.TRUST_E_FAIL;
                    throw new CryptographicException(Win32.TRUST_E_FAIL);
                }
                if (storedCertificates.Contains(signingCertificate))
                {
                    _authenticodeSignerInfo.ErrorCode = Win32.TRUST_E_EXPLICIT_DISTRUST;
                    throw new CryptographicException(Win32.TRUST_E_EXPLICIT_DISTRUST);
                }
            }
            finally
            {
                store.Close();
            }

            // prepare information for the TrustManager to display
            string hash;
            string description;
            string url;
            if (!GetManifestInformation(licenseNode, nsm, out hash, out description, out url))
            {
                _authenticodeSignerInfo.ErrorCode = Win32.TRUST_E_SUBJECT_FORM_UNKNOWN;
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }
            _authenticodeSignerInfo.Hash = hash;
            _authenticodeSignerInfo.Description = description;
            _authenticodeSignerInfo.DescriptionUrl = url;

            // read the timestamp from the manifest
            DateTime verificationTime;
            bool isTimestamped = VerifySignatureTimestamp(signatureNode, nsm, out verificationTime);
            bool isLifetimeSigning = false;
            if (isTimestamped)
            {
                isLifetimeSigning = ((verifyFlags & CmiManifestVerifyFlags.LifetimeSigning) == CmiManifestVerifyFlags.LifetimeSigning);
                if (!isLifetimeSigning)
                {
                    isLifetimeSigning = GetLifetimeSigning(signingCertificate);
                }
            }

            // Retrieve the Authenticode policy settings from registry.
            uint policies = GetAuthenticodePolicies();

            X509Chain chain = new X509Chain(); // use the current user profile
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            if ((CmiManifestVerifyFlags.RevocationCheckEndCertOnly & verifyFlags) == CmiManifestVerifyFlags.RevocationCheckEndCertOnly)
            {
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EndCertificateOnly;
            }
            else if ((CmiManifestVerifyFlags.RevocationCheckEntireChain & verifyFlags) == CmiManifestVerifyFlags.RevocationCheckEntireChain)
            {
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            }
            else if (((CmiManifestVerifyFlags.RevocationNoCheck & verifyFlags) == CmiManifestVerifyFlags.RevocationNoCheck) ||
                ((Win32.WTPF_IGNOREREVOKATION & policies) == Win32.WTPF_IGNOREREVOKATION))
            {
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            }

            chain.ChainPolicy.VerificationTime = verificationTime; // local time
            if (isTimestamped && isLifetimeSigning)
            {
                chain.ChainPolicy.ApplicationPolicy.Add(new Oid(Win32.szOID_KP_LIFETIME_SIGNING));
            }

            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag; // don't ignore anything

            bool chainIsValid = chain.Build(signingCertificate);

            if (!chainIsValid)
            {
#if DEBUG
                X509ChainStatus[] statuses = chain.ChainStatus;
                foreach (X509ChainStatus status in statuses)
                {
                    System.Diagnostics.Debug.WriteLine("flag = " + status.Status + " " + status.StatusInformation);
                }
#endif
                AuthenticodeSignerInfo.ErrorCode = Win32.TRUST_E_SUBJECT_NOT_TRUSTED;
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_NOT_TRUSTED);
            }

            // package information for the trust manager
            _authenticodeSignerInfo.SignerChain = chain;

            store = new X509Store(StoreName.TrustedPublisher, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            try
            {
                storedCertificates = (X509Certificate2Collection)store.Certificates;
                if (storedCertificates == null)
                {
                    _authenticodeSignerInfo.ErrorCode = Win32.TRUST_E_FAIL;
                    throw new CryptographicException(Win32.TRUST_E_FAIL);
                }
                if (!storedCertificates.Contains(signingCertificate))
                {
                    AuthenticodeSignerInfo.ErrorCode = Win32.TRUST_E_SUBJECT_NOT_TRUSTED;
                    throw new CryptographicException(Win32.TRUST_E_SUBJECT_NOT_TRUSTED);
                }
            }
            finally
            {
                store.Close();
            }

            // Verify Certificate publisher name
            XmlElement subjectNode = licenseNode.SelectSingleNode("r:grant/as:AuthenticodePublisher/as:X509SubjectName", nsm) as XmlElement;
            if (subjectNode == null || String.Compare(signingCertificate.Subject, subjectNode.InnerText, StringComparison.Ordinal) != 0)
            {
                AuthenticodeSignerInfo.ErrorCode = Win32.TRUST_E_CERT_SIGNATURE;
                throw new CryptographicException(Win32.TRUST_E_CERT_SIGNATURE);
            }

            if (!oldFormat)
                // Make sure we have the intended Authenticode signer.
                VerifyPublisherIdentity(nsm);
        }

        private X509Certificate2 GetSigningCertificate(ManifestSignedXml2 signedXml, XmlNamespaceManager nsm)
        {
            X509Certificate2 signingCertificate = null;

            KeyInfo keyInfo = signedXml.KeyInfo;
            KeyInfoX509Data kiX509 = null;
            RSAKeyValue keyValue = null;
            foreach (KeyInfoClause kic in keyInfo)
            {
                if (keyValue == null)
                {
                    keyValue = kic as RSAKeyValue;
                    if (keyValue == null)
                    {
                        break;
                    }
                }

                if (kiX509 == null)
                {
                    kiX509 = kic as KeyInfoX509Data;
                }

                if (keyValue != null && kiX509 != null)
                {
                    break;
                }
            }

            if (keyValue == null || kiX509 == null)
            {
                // no X509Certificate KeyInfoClause
                _authenticodeSignerInfo.ErrorCode = Win32.TRUST_E_SUBJECT_FORM_UNKNOWN;
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            // get public key from signing keyInfo
            byte[] signingPublicKey = null;
            RSACryptoServiceProvider rsaProvider = keyValue.Key as RSACryptoServiceProvider;
            if (rsaProvider != null)
            {
                signingPublicKey = rsaProvider.ExportCspBlob(false);
            }
            if (signingPublicKey == null)
            {
                _authenticodeSignerInfo.ErrorCode = Win32.TRUST_E_CERT_SIGNATURE;
                throw new CryptographicException(Win32.TRUST_E_CERT_SIGNATURE);
            }

            // enumerate all certificates in x509Data searching for the one whose public key is used in <RSAKeyValue>
            foreach (X509Certificate2 certificate in kiX509.Certificates)
            {
                if (certificate == null)
                {
                    continue;
                }

                bool certificateAuthority = false;
                foreach (X509Extension extention in certificate.Extensions)
                {
                    X509BasicConstraintsExtension basicExtention = extention as X509BasicConstraintsExtension;
                    if (basicExtention != null)
                    {
                        certificateAuthority = basicExtention.CertificateAuthority;
                        if (certificateAuthority)
                        {
                            break;
                        }
                    }
                }

                if (certificateAuthority)
                {
                    // Ignore certs that have "Subject Type=CA" in basic contraints
                    continue;
                }

                RSACryptoServiceProvider p = (RSACryptoServiceProvider)certificate.PublicKey.Key;
                byte[] certificatePublicKey = p.ExportCspBlob(false);
                bool noMatch = false;
                if (signingPublicKey.Length == certificatePublicKey.Length)
                {
                    for (int i = 0; i < signingPublicKey.Length; i++)
                    {
                        if (signingPublicKey[i] != certificatePublicKey[i])
                        {
                            noMatch = true;
                            break;
                        }
                    }
                    if (!noMatch)
                    {
                        signingCertificate = certificate;
                        break;
                    }
                }
            }

            if (signingCertificate == null)
            {
                _authenticodeSignerInfo.ErrorCode = Win32.TRUST_E_CERT_SIGNATURE;
                throw new CryptographicException(Win32.TRUST_E_CERT_SIGNATURE);
            }
            return signingCertificate;
        }

        private bool VerifySignatureForm(XmlElement signatureNode, string signatureKind, XmlNamespaceManager nsm)
        {
            bool oldFormat = false;
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
                String.Compare(snIdValue, signatureKind, StringComparison.Ordinal) != 0)
            {
                throw new CryptographicException(Win32.TRUST_E_SUBJECT_FORM_UNKNOWN);
            }

            // Make sure it is indeed an enveloped signature.
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

            return oldFormat;
        }

        private bool GetManifestInformation(XmlElement licenseNode, XmlNamespaceManager nsm, out string hash, out string description, out string url)
        {
            hash = "";
            description = "";
            url = "";

            XmlElement manifestInformation = licenseNode.SelectSingleNode("r:grant/as:ManifestInformation", nsm) as XmlElement;
            if (manifestInformation == null)
            {
                return false;
            }
            if (!manifestInformation.HasAttribute("Hash"))
            {
                return false;
            }

            hash = manifestInformation.GetAttribute("Hash");
            if (string.IsNullOrEmpty(hash))
            {
                return false;
            }

            foreach (char c in hash)
            {
                if (0xFF == HexToByte(c))
                {
                    return false;
                }
            }

            if (manifestInformation.HasAttribute("Description"))
            {
                description = manifestInformation.GetAttribute("Description");
            }

            if (manifestInformation.HasAttribute("Url"))
            {
                url = manifestInformation.GetAttribute("Url");
            }

            return true;
        }

        private bool VerifySignatureTimestamp(XmlElement signatureNode, XmlNamespaceManager nsm, out DateTime verificationTime)
        {
            verificationTime = DateTime.Now;

            XmlElement node = signatureNode.SelectSingleNode("ds:Object/as:Timestamp", nsm) as XmlElement;
            if (node != null)
            {
                string encodedMessage = node.InnerText;

                if (!string.IsNullOrEmpty(encodedMessage))
                {
                    byte[] base64DecodedMessage = null;
                    try
                    {
                        base64DecodedMessage = Convert.FromBase64String(encodedMessage);
                    }
                    catch (FormatException)
                    {
                        _authenticodeSignerInfo.ErrorCode = Win32.TRUST_E_TIME_STAMP;
                        throw new CryptographicException(Win32.TRUST_E_TIME_STAMP);
                    }
                    if (base64DecodedMessage != null)
                    {
                        // Create a new, nondetached SignedCms message.
                        SignedCms signedCms = new SignedCms();
                        signedCms.Decode(base64DecodedMessage);

                        // Verify the signature without validating the 
                        // certificate.
                        signedCms.CheckSignature(true);

                        byte[] signingTime = null;
                        CryptographicAttributeObjectCollection caos = signedCms.SignerInfos[0].SignedAttributes;
                        foreach (CryptographicAttributeObject cao in caos)
                        {
                            if (0 == string.Compare(cao.Oid.Value, Win32.szOID_RSA_signingTime, StringComparison.Ordinal))
                            {
                                foreach (AsnEncodedData d in cao.Values)
                                {
                                    if (0 == string.Compare(d.Oid.Value, Win32.szOID_RSA_signingTime, StringComparison.Ordinal))
                                    {
                                        signingTime = d.RawData;
                                        Pkcs9SigningTime time = new Pkcs9SigningTime(signingTime);
                                        verificationTime = time.SigningTime;
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool GetLifetimeSigning(X509Certificate2 signingCertificate)
        {
            foreach (X509Extension extension in signingCertificate.Extensions)
            {
                X509EnhancedKeyUsageExtension ekuExtention = extension as X509EnhancedKeyUsageExtension;
                if (ekuExtention != null)
                {
                    OidCollection oids = ekuExtention.EnhancedKeyUsages;
                    foreach (Oid oid in oids)
                    {
                        if (0 == string.Compare(Win32.szOID_KP_LIFETIME_SIGNING, oid.Value, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // Retrieve the Authenticode policy settings from registry. 
        // Isolation library was ignoring missing or inaccessible key/value errors
        private uint GetAuthenticodePolicies()
        {
            uint policies = 0;

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(wintrustPolicyFlagsRegPath);
                if (key != null)
                {
                    RegistryValueKind kind = key.GetValueKind(wintrustPolicyFlagsRegName);
                    if (kind == RegistryValueKind.DWord || kind == RegistryValueKind.Binary)
                    {
                        object value = key.GetValue(wintrustPolicyFlagsRegName);
                        if (value != null)
                        {
                            policies = Convert.ToUInt32(value);
                        }
                    }
                    key.Close();
                }
            }
            catch (System.Security.SecurityException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
            return policies;
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
            byte[] computedHashBytes = ComputeHashFromManifest(manifestDom, _useSha256);

            // Do they match?
            if (hashBytes.Length == 0 || hashBytes.Length != computedHashBytes.Length)
            {
                byte[] computedOldHashBytes = ComputeHashFromManifest(manifestDom, true, _useSha256);

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
                    byte[] computedOldHashBytes = ComputeHashFromManifest(manifestDom, true, _useSha256);

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

        /// <summary>
        /// The reason you need provider type 24, is because that’s the only RSA provider type that supports SHA-2 operations.   (For instance, PROV_RSA_FULL does not support SHA-2).
        /// As for official guidance – I’m not sure of any.    For workarounds though, if you’re using the Microsoft software CSPs, they share the underlying key store.  You can get the key container name from your RSA object, then open up a new RSA object with the same key container name but with PROV_RSA_AES.   At that point, you should be able to use SHA-2 algorithms.
        /// </summary>
        /// <param name="oldCsp"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Cryptographic.Standard", "CA5358:RSAProviderNeeds2048bitKey", Justification = "SHA1 is retained for compatibility reasons as an option in VisualStudio signing page and consequently in the trust manager, default is SHA2.")]
        internal static RSACryptoServiceProvider GetFixedRSACryptoServiceProvider(RSACryptoServiceProvider oldCsp, bool useSha256)
        {
            if (!useSha256)
            {
                return oldCsp;
            }

            const int PROV_RSA_AES = 24;    // CryptoApi provider type for an RSA provider supporting sha-256 digital signatures
            CspParameters csp = new CspParameters();
            csp.ProviderType = PROV_RSA_AES;
            csp.KeyContainerName = oldCsp.CspKeyContainerInfo.KeyContainerName;
            csp.KeyNumber = (int)oldCsp.CspKeyContainerInfo.KeyNumber;
            if (oldCsp.CspKeyContainerInfo.MachineKeyStore)
            {
                csp.Flags = CspProviderFlags.UseMachineKeyStore;
            }
            RSACryptoServiceProvider fixedRsa = new RSACryptoServiceProvider(csp);

            return fixedRsa;
        }

        private static void ReplacePublicKeyToken(XmlDocument manifestDom, AsymmetricAlgorithm snKey, bool useSha256)
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

            byte[] cspPublicKeyBlob = (GetFixedRSACryptoServiceProvider((RSACryptoServiceProvider)snKey, useSha256)).ExportCspBlob(false);
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

        private static byte[] ComputeHashFromManifest(XmlDocument manifestDom, bool useSha256)
        {
            return ComputeHashFromManifest(manifestDom, false, useSha256);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Cryptographic.Standard", "CA5354:SHA1CannotBeUsed", Justification = "SHA1 is retained for compatibility reasons as an option in VisualStudio signing page and consequently in the trust manager, default is SHA2.")]
        private static byte[] ComputeHashFromManifest(XmlDocument manifestDom, bool oldFormat, bool useSha256)
        {
            if (oldFormat)
            {
                XmlDsigExcC14NTransform exc = new XmlDsigExcC14NTransform();
                exc.LoadInput(manifestDom);

                if (useSha256)
                {
                    using (SHA256CryptoServiceProvider sha2 = new SHA256CryptoServiceProvider())
                    {
                        byte[] hash = sha2.ComputeHash(exc.GetOutput() as MemoryStream);
                        if (hash == null)
                        {
                            throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
                        }

                        return hash;
                    }
                }
                else
                {
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

                if (useSha256)
                {
                    using (SHA256CryptoServiceProvider sha2 = new SHA256CryptoServiceProvider())
                    {
                        byte[] hash = sha2.ComputeHash(exc.GetOutput() as MemoryStream);
                        if (hash == null)
                        {
                            throw new CryptographicException(Win32.TRUST_E_BAD_DIGEST);
                        }

                        return hash;
                    }
                }
                else
                {
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

        private static XmlDocument CreateLicenseDom(CmiManifestSigner2 signer, XmlElement principal, byte[] hash)
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

        private static void AuthenticodeSignLicenseDom(XmlDocument licenseDom, CmiManifestSigner2 signer, string timeStampUrl, bool useSha256)
        {
            // Make sure it is RSA, as this is the only one Fusion will support.
            if (signer.Certificate.PublicKey.Key.GetType() != typeof(RSACryptoServiceProvider))
            {
                throw new NotSupportedException();
            }

            if (signer.Certificate.PrivateKey.GetType() != typeof(RSACryptoServiceProvider))
            {
                throw new NotSupportedException();
            }

            // Setup up XMLDSIG engine.
            ManifestSignedXml2 signedXml = new ManifestSignedXml2(licenseDom);
            signedXml.SigningKey = GetFixedRSACryptoServiceProvider(signer.Certificate.PrivateKey as RSACryptoServiceProvider, useSha256);
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
            if (signer.UseSha256)
                signedXml.SignedInfo.SignatureMethod = Sha256SignatureMethodUri;

            // Add the key information.
            signedXml.KeyInfo.AddClause(new RSAKeyValue(GetFixedRSACryptoServiceProvider(signer.Certificate.PrivateKey as RSACryptoServiceProvider, useSha256) as RSA));
            signedXml.KeyInfo.AddClause(new KeyInfoX509Data(signer.Certificate, signer.IncludeOption));

            // Add the enveloped reference.
            Reference reference = new Reference();
            reference.Uri = "";
            if (signer.UseSha256)
                reference.DigestMethod = Sha256DigestMethod;

            // Add an enveloped and an Exc-C14N transform.
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
#if (false) // BUGBUG: LTA transform complaining about issuer node not found.
            reference.AddTransform(new XmlLicenseTransform()); 
#endif
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

        private static void StrongNameSignManifestDom(XmlDocument manifestDom, XmlDocument licenseDom, CmiManifestSigner2 signer, bool useSha256)
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

            if (signer.StrongNameKey.GetType() != typeof(RSACryptoServiceProvider))
            {
                throw new NotSupportedException();
            }

            // Setup up XMLDSIG engine.
            ManifestSignedXml2 signedXml = new ManifestSignedXml2(signatureParent);
            signedXml.SigningKey = GetFixedRSACryptoServiceProvider(signer.StrongNameKey as RSACryptoServiceProvider, useSha256);
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
            if (signer.UseSha256)
                signedXml.SignedInfo.SignatureMethod = Sha256SignatureMethodUri;

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
            if (signer.UseSha256)
                enveloped.DigestMethod = Sha256DigestMethod;

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

    internal class CmiManifestSigner2
    {
        private AsymmetricAlgorithm _strongNameKey;
        private X509Certificate2 _certificate;
        private string _description;
        private string _url;
        private X509Certificate2Collection _certificates;
        private X509IncludeOption _includeOption;
        private CmiManifestSignerFlag _signerFlag;
        private bool _useSha256;

        private CmiManifestSigner2() { }

        internal CmiManifestSigner2(AsymmetricAlgorithm strongNameKey) :
            this(strongNameKey, null, false)
        { }

        internal CmiManifestSigner2(AsymmetricAlgorithm strongNameKey, X509Certificate2 certificate, bool useSha256)
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
            _useSha256 = useSha256;
        }

        internal bool UseSha256
        {
            get
            {
                return _useSha256;
            }
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
}

