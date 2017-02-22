// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Collections;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
#if FEATURE_PFX_SIGNING
using Microsoft.Runtime.Hosting;
#endif

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Determine the strong name key source
    /// </summary>
    public class ResolveKeySource : TaskExtension
    {
        private string _keyFile;
        private string _certificateThumbprint;
        private string _certificateFile;
        private string _resolvedKeyContainer = String.Empty;
        private string _resolvedKeyFile = String.Empty;
        private string _resolvedThumbprint = String.Empty;
        private const string pfxFileExtension = ".pfx";
        private const string pfxFileContainerPrefix = "VS_KEY_";
        private bool _suppressAutoClosePasswordPrompt = false;
        private bool _showImportDialogDespitePreviousFailures = false;
        private int _autoClosePasswordPromptTimeout = 20;
        private int _autoClosePasswordPromptShow = 15;
        static private Hashtable s_pfxKeysToIgnore = new Hashtable(StringComparer.OrdinalIgnoreCase);


        #region Properties

        public string KeyFile
        {
            get { return _keyFile; }
            set { _keyFile = value; }
        }

        public string CertificateThumbprint
        {
            get { return _certificateThumbprint; }
            set { _certificateThumbprint = value; }
        }

        public string CertificateFile
        {
            get { return _certificateFile; }
            set { _certificateFile = value; }
        }

        public bool SuppressAutoClosePasswordPrompt
        {
            get { return _suppressAutoClosePasswordPrompt; }
            set { _suppressAutoClosePasswordPrompt = value; }
        }

        public bool ShowImportDialogDespitePreviousFailures
        {
            get { return _showImportDialogDespitePreviousFailures; }
            set { _showImportDialogDespitePreviousFailures = value; }
        }

        public int AutoClosePasswordPromptTimeout
        {
            get { return _autoClosePasswordPromptTimeout; }
            set { _autoClosePasswordPromptTimeout = value; }
        }

        public int AutoClosePasswordPromptShow
        {
            get { return _autoClosePasswordPromptShow; }
            set { _autoClosePasswordPromptShow = value; }
        }

        [Output]
        public string ResolvedThumbprint
        {
            get { return _resolvedThumbprint; }
            set { _resolvedThumbprint = value; }
        }

        [Output]
        public string ResolvedKeyContainer
        {
            get { return _resolvedKeyContainer; }
            set { _resolvedKeyContainer = value; }
        }

        [Output]
        public string ResolvedKeyFile
        {
            get { return _resolvedKeyFile; }
            set { _resolvedKeyFile = value; }
        }

        #endregion


        #region ITask Members

        public override bool Execute()
        {
            return ResolveAssemblyKey() && ResolveManifestKey();
        }

        // We we use hash the contens of .pfx file so we can establish relationship file <-> container name, whithout
        // need to prompt for password. Note this is not used for any security reasons. With the departure from standard MD5 algoritm
        // we need as simple hash function for replacement. The data blobs we use (.pfx files)  are
        // encrypted meaning they have high entropy, so in all practical pupose even a simpliest
        // hash would give good enough results. This code needs to be kept in sync with the code  in compsvcpkgs
        // to prevent double prompt for newly created keys. The magic numbers here are just random primes
        // in the range 10m/20m.
        static private UInt64 HashFromBlob(byte[] data)
        {
            UInt32 dw1 = 17339221;
            UInt32 dw2 = 19619429;
            UInt32 pos = 10803503;

            foreach (byte b in data)
            {
                UInt32 value = b ^ pos;
                pos *= 10803503;
                dw1 += ((value ^ dw2) * 15816943) + 17368321;
                dw2 ^= ((value + dw1) * 14984549) ^ 11746499;
            }
            UInt64 result = dw1;
            result <<= 32;
            result |= dw2;
            return result;
        }


        private bool ResolveAssemblyKey()
        {
            bool pfxSuccess = true;
            if (KeyFile != null && KeyFile.Length > 0)
            {
                string keyFileExtension = String.Empty;
                try
                {
                    keyFileExtension = Path.GetExtension(KeyFile);
                }
                catch (ArgumentException ex)
                {
                    Log.LogErrorWithCodeFromResources("ResolveKeySource.InvalidKeyName", KeyFile, ex.Message);
                    pfxSuccess = false;
                }
                if (pfxSuccess)
                {
                    if (0 != String.Compare(keyFileExtension, pfxFileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        ResolvedKeyFile = KeyFile;
                    }
                    else
                    {
#if FEATURE_PFX_SIGNING
                        pfxSuccess = false;
                        // it is .pfx file. It is being imported into key container with name = "VS_KEY_<MD5 check sum of the encrypted file>"
                        System.IO.FileStream fs = null;
                        try
                        {
                            string hashedContainerName = String.Empty;
                            string currentUserName = Environment.UserDomainName + "\\" + Environment.UserName;
                            // we use the curent user name to randomize the associated container name, i.e different user on the same machine will export to different keys
                            // this is because SNAPI by default will create keys in "per-machine" crypto store (visible for all the user) but will set the permission such only
                            // creator will be able to use it. This will make imposible for other user both to sign or export the key again (since they also can not delete that key).
                            // Now different users will use different container name. We use ToLower(invariant) because this is what the native equivalent of this function (Create new key, or VC++ import-er).
                            // use as well and we want to keep the hash (and key container name the same) otherwise user could be prompt for a password twice.
                            byte[] userNameBytes = System.Text.Encoding.Unicode.GetBytes(currentUserName.ToLower(CultureInfo.InvariantCulture));
                            fs = System.IO.File.OpenRead(KeyFile);
                            int fileLength = (int)fs.Length;
                            byte[] keyBytes = new byte[fileLength];
                            fs.Read(keyBytes, 0, fileLength);

                            UInt64 hash = HashFromBlob(keyBytes);
                            hash ^= HashFromBlob(userNameBytes); // modify it with the username hash, so each user would get different hash for the same key

                            hashedContainerName = pfxFileContainerPrefix + hash.ToString("X016", CultureInfo.InvariantCulture);

                            IntPtr publicKeyBlob = IntPtr.Zero;
                            int publicKeyBlobSize = 0;
                            if (StrongNameHelpers.StrongNameGetPublicKey(hashedContainerName, IntPtr.Zero, 0, out publicKeyBlob, out publicKeyBlobSize) && publicKeyBlob != IntPtr.Zero)
                            {
                                StrongNameHelpers.StrongNameFreeBuffer(publicKeyBlob);
                                pfxSuccess = true;
                            }
                            else
                            {
                                if (ShowImportDialogDespitePreviousFailures || !s_pfxKeysToIgnore.Contains(hashedContainerName))
                                {
                                    Log.LogErrorWithCodeFromResources("ResolveKeySource.KeyFileForSignAssemblyNotImported", KeyFile, hashedContainerName);
                                }

                                Log.LogErrorWithCodeFromResources("ResolveKeySource.KeyImportError", KeyFile);
                            }
                            if (pfxSuccess)
                            {
                                ResolvedKeyContainer = hashedContainerName;
                            }
                        }
                        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                        {
                            Log.LogErrorWithCodeFromResources("ResolveKeySource.KeyMD5SumError", KeyFile, e.Message);
                        }
                        finally
                        {
                            if (fs != null)
                            {
                                fs.Close();
                            }
                        }
#else
                        Log.LogError("PFX signing not supported on .NET Core");
                        pfxSuccess = false;
#endif
                    }
                }
            }

            return pfxSuccess;
        }

        private bool ResolveManifestKey()
        {
            bool certSuccess = false;
            bool certInStore = false;
            if (!string.IsNullOrEmpty(CertificateThumbprint))
            {
                // look for cert in the cert store
                X509Store personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                try
                {
                    personalStore.Open(OpenFlags.ReadWrite);
                    X509Certificate2Collection foundCerts = personalStore.Certificates.Find(X509FindType.FindByThumbprint, CertificateThumbprint, false);
                    if (foundCerts.Count == 1)
                    {
                        certInStore = true;
                        ResolvedThumbprint = CertificateThumbprint;
                        certSuccess = true;
                    }
                }
                finally
                {
#if FEATURE_PFX_SIGNING
                    personalStore.Close();
#else
                    personalStore.Dispose();
#endif
                }
                if (!certSuccess)
                {
                    Log.LogWarningWithCodeFromResources("ResolveKeySource.ResolvedThumbprintEmpty");
                }
            }

            if (!string.IsNullOrEmpty(CertificateFile) && !certInStore)
            {
#if FEATURE_PFX_SIGNING
                // if the cert isn't on disk, we can't import it
                if (!File.Exists(CertificateFile))
                {
                    Log.LogErrorWithCodeFromResources("ResolveKeySource.CertificateNotInStore");
                }
                else
                {
                    // add the cert to the store optionally prompting for the password
                    if (X509Certificate2.GetCertContentType(CertificateFile) == X509ContentType.Pfx)
                    {
                        bool imported = false;
                        // first try it with no password
                        X509Certificate2 cert = new X509Certificate2();
                        X509Store personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                        try
                        {
                            personalStore.Open(OpenFlags.ReadWrite);
                            cert.Import(CertificateFile, (string)null, X509KeyStorageFlags.PersistKeySet);
                            personalStore.Add(cert);
                            ResolvedThumbprint = cert.Thumbprint;
                            imported = true;
                            certSuccess = true;
                        }
                        catch (CryptographicException)
                        {
                            // cert has a password, move on and prompt for it
                        }
                        finally
                        {
                            personalStore.Close();
                        }
                        if (!imported && ShowImportDialogDespitePreviousFailures)
                        {
                            Log.LogErrorWithCodeFromResources("ResolveKeySource.KeyFileForManifestNotImported", KeyFile);
                        }
                        if (!certSuccess)
                        {
                            Log.LogErrorWithCodeFromResources("ResolveKeySource.KeyImportError", CertificateFile);
                        }
                    }
                    else
                    {
                        X509Store personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                        try
                        {
                            X509Certificate2 cert = new X509Certificate2(CertificateFile);
                            personalStore.Open(OpenFlags.ReadWrite);
                            personalStore.Add(cert);
                            ResolvedThumbprint = cert.Thumbprint;
                            certSuccess = true;
                        }
                        catch (CryptographicException)
                        {
                            Log.LogErrorWithCodeFromResources("ResolveKeySource.KeyImportError", CertificateFile);
                        }
                        finally
                        {
                            personalStore.Close();
                        }
                    }
                }
#else
                Log.LogError("Certificate signing not supported on .NET Core");
#endif
            }
            else if (!certInStore && !string.IsNullOrEmpty(CertificateFile) && !string.IsNullOrEmpty(CertificateThumbprint))
            {
                // no file and not in store, error out
                Log.LogErrorWithCodeFromResources("ResolveKeySource.CertificateNotInStore");
                certSuccess = false;
            }
            else
                certSuccess = true;

            return certSuccess;
        }

        #endregion
    }
}
