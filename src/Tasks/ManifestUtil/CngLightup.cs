// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==

// Supported preprocessor declarations:
//
// CNG_LIGHTUP_NO_SYSTEM_CORE
//   Indicates that this file is being processed from a context that cannot access System.Core.
//     * The assembly must be built with <GenerateAssemblyRefs>true</GenerateAssemblyRefs>
//     * The ECDSA methods are not available

// There are cases where we have multiple assemblies that are going to import this file and 
// if they are going to also have InternalsVisibleTo between them, there will be a compiler warning
// that the type is found both in the source and in a referenced assembly. The compiler will prefer 
// the version of the type defined in the source
//
// In order to disable the warning for this type we are disabling this warning for this entire file.
#pragma warning disable 436

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace System.Security.Cryptography
{
    internal static partial class CngLightup
    {
        private const string DsaOid = "1.2.840.10040.4.1";
        private const string RsaOid = "1.2.840.113549.1.1.1";

        private const string HashAlgorithmNameTypeName = "System.Security.Cryptography.HashAlgorithmName";
        private const string RSASignaturePaddingTypeName = "System.Security.Cryptography.RSASignaturePadding";
        private const string RSAEncryptionPaddingTypeName = "System.Security.Cryptography.RSAEncryptionPadding";
        private const string RSACngTypeName = "System.Security.Cryptography.RSACng";
        private const string DSACngTypeName = "System.Security.Cryptography.DSACng";

        // If 4.6 is guaranteed as a baseline, then these field references should become typeof()
        // expressions, and the methods that consume them can get significantly easier.
        private static readonly Type s_hashAlgorithmNameType =
            typeof(object).Assembly.GetType(HashAlgorithmNameTypeName, false);

        private static readonly Type s_rsaSignaturePaddingType =
            typeof(object).Assembly.GetType(RSASignaturePaddingTypeName, false);

        private static readonly Type s_rsaEncryptionPaddingType =
            typeof(object).Assembly.GetType(RSAEncryptionPaddingTypeName, false);

        private static readonly object s_pkcs1SignaturePadding =
            s_rsaSignaturePaddingType == null ? null :
            s_rsaSignaturePaddingType.GetProperty("Pkcs1", BindingFlags.Static | BindingFlags.Public).GetValue(null);

        private static readonly object s_pkcs1EncryptionPadding =
            s_rsaEncryptionPaddingType == null ? null :
            s_rsaEncryptionPaddingType.GetProperty("Pkcs1", BindingFlags.Static | BindingFlags.Public).GetValue(null);

        private static readonly object s_oaepSha1EncryptionPadding =
            s_rsaEncryptionPaddingType == null ? null :
            s_rsaEncryptionPaddingType.GetProperty("OaepSHA1", BindingFlags.Static | BindingFlags.Public).GetValue(null);

        private static readonly Lazy<bool> s_preferRsaCng = new Lazy<bool>(DetectRsaCngSupport);

        private static volatile Func<X509Certificate2, DSA> s_getDsaPublicKey;
        private static volatile Func<X509Certificate2, DSA> s_getDsaPrivateKey;

        private static volatile Func<X509Certificate2, RSA> s_getRsaPublicKey;
        private static volatile Func<X509Certificate2, RSA> s_getRsaPrivateKey;
        private static volatile Func<RSA, byte[], string, byte[]> s_rsaPkcs1SignMethod;
        private static volatile Func<RSA, byte[], byte[], string, bool> s_rsaPkcs1VerifyMethod;
        private static volatile Func<RSA, byte[], byte[]> s_rsaPkcs1EncryptMethod;
        private static volatile Func<RSA, byte[], byte[]> s_rsaPkcs1DecryptMethod;
        private static volatile Func<RSA, byte[], byte[]> s_rsaOaepSha1EncryptMethod;
        private static volatile Func<RSA, byte[], byte[]> s_rsaOaepSha1DecryptMethod;

#if !CNG_LIGHTUP_NO_SYSTEM_CORE
        private static volatile Func<X509Certificate2, ECDsa> s_getECDsaPublicKey;
        private static volatile Func<X509Certificate2, ECDsa> s_getECDsaPrivateKey;
#endif

        internal static RSA GetRSAPublicKey(X509Certificate2 cert)
        {
            if (s_getRsaPublicKey == null)
            {
                if (s_preferRsaCng.Value)
                {
                    s_getRsaPublicKey =
                        BindCoreDelegate<RSA>("RSA", isPublic: true) ??
                        BindGetCapiPublicKey<RSA, RSACryptoServiceProvider>(RsaOid);
                }
                else
                {
                    s_getRsaPublicKey = BindGetCapiPublicKey<RSA, RSACryptoServiceProvider>(RsaOid);
                }
            }

            return s_getRsaPublicKey(cert);
        }

        internal static RSA GetRSAPrivateKey(X509Certificate2 cert)
        {
            if (s_getRsaPrivateKey == null)
            {
                if (s_preferRsaCng.Value)
                {
                    s_getRsaPrivateKey =
                        BindCoreDelegate<RSA>("RSA", isPublic: false) ??
                        BindGetCapiPrivateKey<RSA>(RsaOid, csp => new RSACryptoServiceProvider(csp));
                }
                else
                {
                    s_getRsaPrivateKey = BindGetCapiPrivateKey<RSA>(RsaOid, csp => new RSACryptoServiceProvider(csp));
                }
            }

            return s_getRsaPrivateKey(cert);
        }

        internal static DSA GetDSAPublicKey(X509Certificate2 cert)
        {
            if (s_getDsaPublicKey == null)
            {
                s_getDsaPublicKey =
                    BindCoreDelegate<DSA>("DSA", isPublic: true) ??
                    BindGetCapiPublicKey<DSA, DSACryptoServiceProvider>(DsaOid);
            }

            return s_getDsaPublicKey(cert);
        }

        internal static DSA GetDSAPrivateKey(X509Certificate2 cert)
        {
            if (s_getDsaPrivateKey == null)
            {
                s_getDsaPrivateKey =
                    BindCoreDelegate<DSA>("DSA", isPublic: false) ??
                    BindGetCapiPrivateKey<DSA>(DsaOid, csp => new DSACryptoServiceProvider(csp));
            }

            return s_getDsaPrivateKey(cert);
        }

#if !CNG_LIGHTUP_NO_SYSTEM_CORE
        internal static ECDsa GetECDsaPublicKey(X509Certificate2 cert)
        {
            // ECDsa has no fallback mode, it was only available via the System.Core extension method.
            // With this lightup path, GetECDsaPublicKey can return null for an ECDSA certificate, just because
            // System.Core is too old.
            if (s_getECDsaPublicKey == null)
            {
                s_getECDsaPublicKey = BindCoreDelegate<ECDsa>("ECDsa", isPublic: true) ?? (c => null);
            }

            return s_getECDsaPublicKey(cert);
        }

        internal static ECDsa GetECDsaPrivateKey(X509Certificate2 cert)
        {
            // ECDsa has no fallback mode, it was only available via the System.Core extension method.
            // With this lightup path, GetECDsaPrivateKey can return null for an ECDSA certificate, just because
            // System.Core is too old.
            if (s_getECDsaPrivateKey == null)
            {
                s_getECDsaPrivateKey = BindCoreDelegate<ECDsa>("ECDsa", isPublic: false) ?? (c => null);
            }

            return s_getECDsaPrivateKey(cert);
        }
#endif

        // When 4.6 is guaranteed as the minimum patch baseline, this method should be moved to a direct
        // call to RSA.SignData.  Or, even better, removed and just used inline.
        internal static byte[] Pkcs1SignData(RSA rsa, byte[] data, string hashAlgorithmName)
        {
            // Because RSACryptoServiceProvider existed in 4.5, but RSACng didn't, and RSA's SignData
            // method requires types that aren't in 4.5, try RSACryptoServiceProvider's way first.
            if (rsa is RSACryptoServiceProvider rsaCsp)
            {
                return rsaCsp.SignData(data, hashAlgorithmName);
            }

            if (s_rsaPkcs1SignMethod == null)
            {
                // [X] SignData(byte[] data, HashAlgorithmName hashAlgorithmName, RSASignaturePadding padding)
                // [ ] SignData(byte[] data, int offset, int count, HashAlgorithmName hashAlgorithmName, RSASignaturePadding padding)
                // [ ] SignData(Stream data, HashAlgorithmName hashAlgorithmName, RSASignaturePadding padding)

                Debug.Assert(s_hashAlgorithmNameType != null);
                Debug.Assert(s_rsaSignaturePaddingType != null);
                Debug.Assert(s_pkcs1SignaturePadding != null);
                Type[] signatureTypes = { typeof(byte[]), s_hashAlgorithmNameType, s_rsaSignaturePaddingType };

                MethodInfo signDataMethod = typeof(RSA).GetMethod(
                    "SignData",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    signatureTypes,
                    null);

                Debug.Assert(signDataMethod != null, "Cannot find RSA.SignData(byte[], HashAlgorithmName, RSASignaturePadding)");

                // Because the HashAlgorithmName and RSASignaturePadding types aren't guaranteed available at
                // assembly time (though they really need to be if the runtime makes it here...) the delegate binding
                // is a bit harder than normal.
                Type delegateType = typeof(Func<,,,,>).MakeGenericType(
                    typeof(RSA),
                    typeof(byte[]),
                    s_hashAlgorithmNameType,
                    s_rsaSignaturePaddingType,
                    typeof(byte[]));

                Delegate openDelegate = Delegate.CreateDelegate(delegateType, signDataMethod);

                s_rsaPkcs1SignMethod =
                    (delegateRsa, delegateData, delegateAlgorithm) =>
                    {
                        object hashAlgorithmNameObject = Activator.CreateInstance(s_hashAlgorithmNameType, delegateAlgorithm);

                        object[] args = { delegateRsa, delegateData, hashAlgorithmNameObject, s_pkcs1SignaturePadding };
                        return (byte[])openDelegate.DynamicInvoke(args);
                    };
            }

            Debug.Assert(s_rsaPkcs1SignMethod != null);
            return s_rsaPkcs1SignMethod(rsa, data, hashAlgorithmName);
        }

        internal static bool Pkcs1VerifyData(RSA rsa, byte[] data, byte[] signature, string hashAlgorithmName)
        {
            // Because RSACryptoServiceProvider existed in 4.5, but RSACng didn't, and RSA's SignData
            // method requires types that aren't in 4.5, try RSACryptoServiceProvider's way first.
            if (rsa is RSACryptoServiceProvider rsaCsp)
            {
                return rsaCsp.VerifyData(data, hashAlgorithmName, signature);
            }

            if (s_rsaPkcs1VerifyMethod == null)
            {
                // [X] VerifyData(byte[] data, byte[] signature, HashAlgorithmName hashAlgorithmName, RSASignaturePadding padding)
                // [ ] VerifyData(byte[] data, int offset, int count, byte[] signature, HashAlgorithmName hashAlgorithmName, RSASignaturePadding padding)
                // [ ] VerifyData(Stream data, byte[] signature, HashAlgorithmName hashAlgorithmName, RSASignaturePadding padding)

                Debug.Assert(s_hashAlgorithmNameType != null);
                Debug.Assert(s_rsaSignaturePaddingType != null);
                Debug.Assert(s_pkcs1SignaturePadding != null);
                Type[] signatureTypes = { typeof(byte[]), typeof(byte[]), s_hashAlgorithmNameType, s_rsaSignaturePaddingType };

                MethodInfo verifyDataMethod = typeof(RSA).GetMethod(
                    "VerifyData",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    signatureTypes,
                    null);

                Debug.Assert(
                    verifyDataMethod != null,
                    "Cannot find RSA.VerifyData(byte[], byte[], HashAlgorithmName, RSASignaturePadding)");

                // Because the HashAlgorithmName and RSASignaturePadding types aren't guaranteed available at
                // assembly time (though they really need to be if the runtime makes it here...) the delegate binding
                // is a bit harder than normal.
                Type delegateType = typeof(Func<,,,,,>).MakeGenericType(
                    typeof(RSA),
                    typeof(byte[]),
                    typeof(byte[]),
                    s_hashAlgorithmNameType,
                    s_rsaSignaturePaddingType,
                    typeof(bool));

                Delegate openDelegate = Delegate.CreateDelegate(delegateType, verifyDataMethod);

                s_rsaPkcs1VerifyMethod =
                    (delegateRsa, delegateData, delegateSignature, delegateAlgorithm) =>
                    {
                        object hashAlgorithmNameObject = Activator.CreateInstance(s_hashAlgorithmNameType, delegateAlgorithm);

                        object[] args =
                        {
                            delegateRsa,
                            delegateData,
                            delegateSignature,
                            hashAlgorithmNameObject,
                            s_pkcs1SignaturePadding
                        };

                        return (bool)openDelegate.DynamicInvoke(args);
                    };
            }

            Debug.Assert(s_rsaPkcs1VerifyMethod != null);
            return s_rsaPkcs1VerifyMethod(rsa, data, signature, hashAlgorithmName);
        }

        internal static byte[] Pkcs1Encrypt(RSA rsa, byte[] data)
        {
            // Because RSACryptoServiceProvider existed in 4.5, but RSACng didn't, and RSA's Encrypt
            // method requires types that aren't in 4.5, try RSACryptoServiceProvider's way first.
            if (rsa is RSACryptoServiceProvider rsaCsp)
            {
                return rsaCsp.Encrypt(data, false);
            }

            if (s_rsaPkcs1EncryptMethod == null)
            {
                Debug.Assert(s_pkcs1EncryptionPadding != null);

                Delegate openDelegate = BindRsaCryptMethod("Encrypt");

                s_rsaPkcs1EncryptMethod =
                    (delegateRsa, delegateData) => (byte[])openDelegate.DynamicInvoke(delegateRsa, delegateData, s_pkcs1EncryptionPadding);
            }

            Debug.Assert(s_rsaPkcs1EncryptMethod != null);
            return s_rsaPkcs1EncryptMethod(rsa, data);
        }

        internal static byte[] Pkcs1Decrypt(RSA rsa, byte[] data)
        {
            // Because RSACryptoServiceProvider existed in 4.5, but RSACng didn't, and RSA's Decrypt
            // method requires types that aren't in 4.5, try RSACryptoServiceProvider's way first.
            if (rsa is RSACryptoServiceProvider rsaCsp)
            {
                return rsaCsp.Decrypt(data, false);
            }

            if (s_rsaPkcs1DecryptMethod == null)
            {
                Debug.Assert(s_pkcs1EncryptionPadding != null);

                Delegate openDelegate = BindRsaCryptMethod("Decrypt");

                s_rsaPkcs1DecryptMethod =
                    (delegateRsa, delegateData) => (byte[])openDelegate.DynamicInvoke(delegateRsa, delegateData, s_pkcs1EncryptionPadding);
            }

            Debug.Assert(s_rsaPkcs1DecryptMethod != null);
            return s_rsaPkcs1DecryptMethod(rsa, data);
        }

        internal static byte[] OaepSha1Encrypt(RSA rsa, byte[] data)
        {
            // Because RSACryptoServiceProvider existed in 4.5, but RSACng didn't, and RSA's Encrypt
            // method requires types that aren't in 4.5, try RSACryptoServiceProvider's way first.

            if (rsa is RSACryptoServiceProvider rsaCsp)
            {
                return rsaCsp.Encrypt(data, true);
            }

            if (s_rsaOaepSha1EncryptMethod == null)
            {
                Debug.Assert(s_oaepSha1EncryptionPadding != null);

                Delegate openDelegate = BindRsaCryptMethod("Encrypt");

                s_rsaOaepSha1EncryptMethod =
                    (delegateRsa, delegateData) => (byte[])openDelegate.DynamicInvoke(delegateRsa, delegateData, s_oaepSha1EncryptionPadding);
            }

            Debug.Assert(s_rsaOaepSha1EncryptMethod != null);
            return s_rsaOaepSha1EncryptMethod(rsa, data);
        }

        internal static byte[] OaepSha1Decrypt(RSA rsa, byte[] data)
        {
            // Because RSACryptoServiceProvider existed in 4.5, but RSACng didn't, and RSA's Decrypt
            // method requires types that aren't in 4.5, try RSACryptoServiceProvider's way first.
            if (rsa is RSACryptoServiceProvider rsaCsp)
            {
                return rsaCsp.Decrypt(data, true);
            }

            if (s_rsaOaepSha1DecryptMethod == null)
            {
                Debug.Assert(s_oaepSha1EncryptionPadding != null);

                Delegate openDelegate = BindRsaCryptMethod("Decrypt");

                s_rsaOaepSha1DecryptMethod =
                    (delegateRsa, delegateData) => (byte[])openDelegate.DynamicInvoke(delegateRsa, delegateData, s_oaepSha1EncryptionPadding);
            }

            Debug.Assert(s_rsaOaepSha1DecryptMethod != null);
            return s_rsaOaepSha1DecryptMethod(rsa, data);
        }

        private static Delegate BindRsaCryptMethod(string methodName)
        {
            Debug.Assert(s_rsaEncryptionPaddingType != null);
            Type[] signatureTypes = { typeof(byte[]), s_rsaEncryptionPaddingType };

            MethodInfo cryptMethod = typeof(RSA).GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                signatureTypes,
                null);

            Debug.Assert(cryptMethod != null, "Cannot find RSA." + methodName + "(byte[], RSAEncryptionPadding)");

            // Because the RSAEncryptionPadding type isn't guaranteed available at assembly time
            // (though they really need to be if the runtime makes it here...) the delegate binding
            // is a bit harder than normal.
            Type delegateType = typeof(Func<,,,>).MakeGenericType(
                typeof(RSA),
                typeof(byte[]),
                s_rsaEncryptionPaddingType,
                typeof(byte[]));

            return Delegate.CreateDelegate(delegateType, cryptMethod);
        }

        private static bool DetectRsaCngSupport()
        {
            Type rsaCng = GetSystemCoreType(RSACngTypeName, throwOnError: false);

            // If the type doesn't exist, there can't be good support for it.
            // (System.Core < 4.6)
            if (rsaCng == null)
            {
                return false;
            }

            Type dsaCng = GetSystemCoreType(DSACngTypeName, throwOnError: false);

            // The original implementation of RSACng returned shared objects in the CAPI fallback
            // pathway. That behavior is hard to test for, since CNG can load all CAPI software keys.
            // But, since DSACng was added in 4.6.2, and RSACng better guarantees uniqueness in 4.6.2
            // use that coincidence as a compatibility test.
            //
            // If DSACng is missing, RSACng usage might lead to attempting to use Disposed objects
            // (System.Core < 4.6.2)
            if (dsaCng == null)
            {
                return false;
            }

            // RSAPKCS1KeyExchangeFormatter, and other BCL types, were better compatible with RSACng
            // in .NET 4.6.2 and beyond.
            //
            // DSACng has a 4.6.2 patch-family dependency on mscorlib 4.6.2, so the existence of
            // DSACng should mean that we can expect the BCL to use the new (4.6) RSA base class
            // overloads, instead of just casting as RSACryptoServiceProvider.
            //
            // But, do one last check: DSA.SignData(byte[], HashAlgorithmName) was added to mscorlib in
            // .NET 4.6.2.  If that's present, RSACng should have sufficient support.
            //
            // A live functionality test would be best, but instantiating an RSACng to generate a new
            // key may fail in no-profile execution contexts; and custom extending RSA in the lightup
            // path would cause the lightup path to fail to load if a library with this change got
            // patched next to mscorlib 4.5.x.
            Type[] signatureTypes = { typeof(byte[]), s_hashAlgorithmNameType };
            MethodInfo signDataMethod = typeof(DSA).GetMethod(
                "SignData",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                signatureTypes,
                null);

            if (signDataMethod == null)
            {
                return false;
            }

            return true;
        }

        private static Func<X509Certificate2, T> BindGetCapiPublicKey<T, TCryptoServiceProvider>(string algorithmOid)
            where T : AsymmetricAlgorithm
            where TCryptoServiceProvider : T, ICspAsymmetricAlgorithm, new()
        {
            return cert =>
            {
                PublicKey publicKeyInfo = cert.PublicKey;

                if (publicKeyInfo.Oid.Value != algorithmOid)
                {
                    return null;
                }

                AsymmetricAlgorithm publicKey = publicKeyInfo.Key;
                Debug.Assert(publicKey != null);
                Debug.Assert(publicKey is T);
                Debug.Assert(publicKey is ICspAsymmetricAlgorithm);

                ICspAsymmetricAlgorithm sharedCspKey = (ICspAsymmetricAlgorithm)publicKey;
                byte[] publicKeyBlob = sharedCspKey.ExportCspBlob(false);

                TCryptoServiceProvider uniqueCspKey = new TCryptoServiceProvider();
                uniqueCspKey.ImportCspBlob(publicKeyBlob);

                return uniqueCspKey;
            };
        }

        private static Func<X509Certificate2, T> BindGetCapiPrivateKey<T>(
            string algorithmOid,
            Func<CspParameters, T> instanceFactory)
            where T : AsymmetricAlgorithm
        {
            return cert =>
            {
                if (!cert.HasPrivateKey)
                {
                    return null;
                }

                PublicKey publicKeyInfo = cert.PublicKey;

                if (publicKeyInfo.Oid.Value != algorithmOid)
                {
                    return null;
                }

                AsymmetricAlgorithm privateKey = cert.PrivateKey;
                Debug.Assert(privateKey != null);
                Debug.Assert(privateKey is T);
                Debug.Assert(privateKey is ICspAsymmetricAlgorithm);

                ICspAsymmetricAlgorithm cspKey = (ICspAsymmetricAlgorithm)privateKey;
                CspParameters cspParameters = CopyCspParameters(cspKey);
                return instanceFactory(cspParameters);
            };
        }

        private static Func<X509Certificate2, T> BindCoreDelegate<T>(string algorithmName, bool isPublic)
        {
            Debug.Assert(typeof(T).Name == algorithmName);

            // Load System.Core.dll and load the appropriate extension class 
            // (one of
            //    System.Security.Cryptography.X509Certificates.RSACertificateExtensions
            //    System.Security.Cryptography.X509Certificates.DSACertificateExtensions
            //    System.Security.Cryptography.X509Certificates.ECDsaCertificateExtensions
            // )
            string typeName = "System.Security.Cryptography.X509Certificates." + algorithmName + "CertificateExtensions";

            Type type = GetSystemCoreType(typeName, throwOnError: false);

            // If the type exists, it will have the GetPublic/GetPrivate methods available.
            // But it can end up not existing in scenarios where this assembly has been patched onto
            // a machine which doesn't have the equivalent baseline.

            if (type == null)
            {
                return null;
            }

            // Now, find the api we want to call:
            //   
            // (one of
            //     GetRSAPublicKey(this X509Certificate2 c)
            //     GetRSAPrivateKey(this X509Certificate2 c)
            //     GetDSAPublicKey(this X509Certificate2 c)
            //     GetDSAPrivateKey(this X509Certificate2 c)
            //     GetECDsaPublicKey(this X509Certificate2 c)
            //     GetECDsaPrivateKey(this X509Certificate2 c)
            // )
            string methodName = "Get" + algorithmName + (isPublic ? "Public" : "Private") + "Key";

            MethodInfo api = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(X509Certificate2) },
                null);

            Debug.Assert(api != null, "Method '" + methodName + "(X509Certificate2 c)' not found on type '" + type + "'");
            return (Func<X509Certificate2, T>)api.CreateDelegate(typeof(Func<X509Certificate2, T>));
        }

        private static CspParameters CopyCspParameters(ICspAsymmetricAlgorithm cspAlgorithm)
        {
            CspKeyContainerInfo cspInfo = cspAlgorithm.CspKeyContainerInfo;

            CspParameters cspParameters = new CspParameters(cspInfo.ProviderType, cspInfo.ProviderName, cspInfo.KeyContainerName)
            {
                Flags = CspProviderFlags.UseExistingKey,
                KeyNumber = (int)cspInfo.KeyNumber,
            };

            if (cspInfo.MachineKeyStore)
            {
                cspParameters.Flags |= CspProviderFlags.UseMachineKeyStore;
            }

            return cspParameters;
        }

        private static Type GetSystemCoreType(string namespaceQualifiedTypeName, bool throwOnError=true)
        {
#if CNG_LIGHTUP_NO_SYSTEM_CORE
            string assemblyQualifiedTypeName = namespaceQualifiedTypeName + ", " + AssemblyRef.SystemCore;
            return Type.GetType(assemblyQualifiedTypeName, throwOnError: false, ignoreCase: false);
#else
            Assembly systemCore = typeof(CngKey).Assembly;
            Debug.Assert(systemCore.GetName().Name == "System.Core");
            return systemCore.GetType(namespaceQualifiedTypeName, throwOnError);
#endif
        }
    }
}

#pragma warning restore 436
