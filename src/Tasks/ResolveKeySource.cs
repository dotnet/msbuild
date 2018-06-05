// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
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
        private const string pfxFileExtension = ".pfx";
        private const string pfxFileContainerPrefix = "VS_KEY_";
        
        #region Properties

        public string KeyFile { get; set; }

        public string CertificateThumbprint { get; set; }

        public string CertificateFile { get; set; }

        public bool SuppressAutoClosePasswordPrompt { get; set; } = false;

        public bool ShowImportDialogDespitePreviousFailures { get; set; } = false;

        public int AutoClosePasswordPromptTimeout { get; set; } = 20;

        public int AutoClosePasswordPromptShow { get; set; } = 15;

        [Output]
        public string ResolvedThumbprint { get; set; } = String.Empty;

        [Output]
        public string ResolvedKeyContainer { get; set; } = String.Empty;

        [Output]
        public string ResolvedKeyFile { get; set; } = String.Empty;

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
        private static UInt64 HashFromBlob(byte[] data)
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
            if (!string.IsNullOrEmpty(KeyFile))
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
                        FileStream fs = null;
                        try
                        {
                            string currentUserName = Environment.UserDomainName + "\\" + Environment.UserName;
                            // we use the curent user name to randomize the associated container name, i.e different user on the same machine will export to different keys
                            // this is because SNAPI by default will create keys in "per-machine" crypto store (visible for all the user) but will set the permission such only
                            // creator will be able to use it. This will make imposible for other user both to sign or export the key again (since they also can not delete that key).
                            // Now different users will use different container name. We use ToLower(invariant) because this is what the native equivalent of this function (Create new key, or VC++ import-er).
                            // use as well and we want to keep the hash (and key container name the same) otherwise user could be prompt for a password twice.
                            byte[] userNameBytes = System.Text.Encoding.Unicode.GetBytes(currentUserName.ToLower(CultureInfo.InvariantCulture));
                            fs = File.OpenRead(KeyFile);
                            int fileLength = (int)fs.Length;
                            var keyBytes = new byte[fileLength];
                            fs.Read(keyBytes, 0, fileLength);

                            UInt64 hash = HashFromBlob(keyBytes);
                            hash ^= HashFromBlob(userNameBytes); // modify it with the username hash, so each user would get different hash for the same key

                            string hashedContainerName = pfxFileContainerPrefix + hash.ToString("X016", CultureInfo.InvariantCulture);

                            if (StrongNameHelpers.StrongNameGetPublicKey(hashedContainerName, IntPtr.Zero, 0, out IntPtr publicKeyBlob, out _) && publicKeyBlob != IntPtr.Zero)
                            {
                                StrongNameHelpers.StrongNameFreeBuffer(publicKeyBlob);
                                pfxSuccess = true;
                            }
                            else
                            {
                                Log.LogErrorWithCodeFromResources("ResolveKeySource.KeyFileForSignAssemblyNotImported", KeyFile, hashedContainerName);
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
                            fs?.Close();
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
                var personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
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
                        var cert = new X509Certificate2();
                        var personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
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
                        var personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                        try
                        {
                            var cert = new X509Certificate2(CertificateFile);
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
            }
            else
            {
                certSuccess = true;
            }

            return certSuccess;
        }

        #endregion
    }
}
