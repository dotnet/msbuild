// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Installer.Windows.Security;
using Microsoft.Win32.Msi;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Manages caching workload pack MSI packages.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class MsiPackageCache : InstallerBase
    {
        /// <summary>
        /// Default inheritance to apply to directory ACLs.
        /// </summary>
        private static readonly InheritanceFlags s_DefaultInheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        /// <summary>
        /// SID that matches built-in administrators.
        /// </summary>
        private static readonly SecurityIdentifier s_AdministratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        /// <summary>
        /// SID that matches everyone.
        /// </summary>
        private static readonly SecurityIdentifier s_EveryoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        /// <summary>
        /// Local SYSTEM SID.
        /// </summary>
        private static readonly SecurityIdentifier s_LocalSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        /// <summary>
        /// SID matching built-in user accounts.
        /// </summary>
        private static readonly SecurityIdentifier s_UsersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        /// <summary>
        /// ACL rule associated with the Administrators SID.
        /// </summary>
        private static readonly FileSystemAccessRule s_AdministratorRule = new FileSystemAccessRule(s_AdministratorsSid, FileSystemRights.FullControl,
            s_DefaultInheritance, PropagationFlags.None, AccessControlType.Allow);

        /// <summary>
        /// ACL rule associated with the Everyone SID.
        /// </summary>
        private static readonly FileSystemAccessRule s_EveryoneRule = new FileSystemAccessRule(s_EveryoneSid, FileSystemRights.ReadAndExecute,
            s_DefaultInheritance, PropagationFlags.None, AccessControlType.Allow);

        /// <summary>
        /// ACL rule associated with the Local SYSTEM SID.
        /// </summary>
        private static readonly FileSystemAccessRule s_LocalSystemRule = new FileSystemAccessRule(s_LocalSystemSid, FileSystemRights.FullControl,
            s_DefaultInheritance, PropagationFlags.None, AccessControlType.Allow);

        /// <summary>
        /// ACL rule associated with the built-in users SID.
        /// </summary>
        private static readonly FileSystemAccessRule s_UsersRule = new FileSystemAccessRule(s_UsersSid, FileSystemRights.ReadAndExecute,
            s_DefaultInheritance, PropagationFlags.None, AccessControlType.Allow);

        /// <summary>
        /// The root directory of the package cache where MSI workload packs are stored.
        /// </summary>
        public readonly string PackageCacheRoot;

        public MsiPackageCache(InstallElevationContextBase elevationContext, ISetupLogger logger,
            bool verifySignatures, string packageCacheRoot = null) : base(elevationContext, logger, verifySignatures)
        {
            PackageCacheRoot = string.IsNullOrWhiteSpace(packageCacheRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "dotnet", "workloads")
                : packageCacheRoot;
        }

        /// <summary>
        /// Creates the specified directory and secures it by configuring access rules (ACLs) that allow sub-directories
        /// and files to inherit access control entries. 
        /// </summary>
        /// <param name="path">The path of the directory to create.</param>
        public static void CreateSecureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                DirectorySecurity ds = new();
                SetDirectoryAccessRules(ds);
                ds.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Moves the MSI payload described by the manifest file to the cache.
        /// </summary>
        /// <param name="packageId">The ID of the workload pack package containing an MSI.</param>
        /// <param name="packageVersion">The package version.</param>
        /// <param name="manifestPath">The JSON manifest associated with the workload pack MSI.</param>
        public void CachePayload(string packageId, string packageVersion, string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"CachePayload: Manifest file not found: {manifestPath}");
            }

            Elevate();

            if (IsElevated)
            {
                string packageDirectory = GetPackageDirectory(packageId, packageVersion);

                // Delete the package directory and create a new one that's secure. If all the files were properly
                // cached, the client would not request this action.
                if (Directory.Exists(packageDirectory))
                {
                    Directory.Delete(packageDirectory, recursive: true);
                }

                CreateSecureDirectory(packageDirectory);

                // We cannot assume that the MSI adjacent to the manifest is the one to cache. We'll trust
                // the manifest to provide the MSI filename.
                MsiManifest msiManifest = JsonConvert.DeserializeObject<MsiManifest>(File.ReadAllText(manifestPath));
                // Only use the filename+extension of the payload property in case the manifest has been altered.
                string msiPath = Path.Combine(Path.GetDirectoryName(manifestPath), Path.GetFileName(msiManifest.Payload));

                string cachedMsiPath = Path.Combine(packageDirectory, Path.GetFileName(msiPath));
                string cachedManifestPath = Path.Combine(packageDirectory, Path.GetFileName(manifestPath));

                MoveAndSecureFile(manifestPath, cachedManifestPath, Log);
                MoveAndSecureFile(msiPath, cachedMsiPath, Log);
            }
            else if (IsClient)
            {
                Dispatcher.SendCacheRequest(InstallRequestType.CachePayload, manifestPath, packageId, packageVersion);
            }
        }

        /// <summary>
        /// Gets the full path of the cache directory for the specified package ID and version.
        /// </summary>
        /// <param name="packageId">The ID of the MSI workload pack package.</param>
        /// <param name="packageVersion">The version of the MSI workload pack package.</param>
        /// <returns>The directory where the MSI package will be cached.</returns>
        public string GetPackageDirectory(string packageId, string packageVersion)
        {
            return Path.Combine(PackageCacheRoot, packageId, packageVersion);
        }

        /// <summary>
        /// Moves a file from one location to another if the destination file does not already exist and
        /// configure its permissions.
        /// </summary>
        /// <param name="sourceFile">The source file to move.</param>
        /// <param name="destinationFile">The destination where the source file will be moved.</param>
        /// <param name="log">The underlying setup log to use.</param>
        public static void MoveAndSecureFile(string sourceFile, string destinationFile, ISetupLogger log = null)
        {
            if (!File.Exists(destinationFile))
            {
                FileAccessRetrier.RetryOnMoveAccessFailure(() =>
                {
                    // Moving the file preserves the owner SID and fails to inherit the WD ACE.
                    File.Copy(sourceFile, destinationFile, overwrite: true);
                    File.Delete(sourceFile);
                });
                log?.LogMessage($"Moved '{sourceFile}' to '{destinationFile}'");

                FileInfo fi = new(destinationFile);
                FileSecurity fs = new();

                // Set the owner and group to built-in administrators (BA). All other ACE values are inherited from
                // the parent directory. See https://github.com/dotnet/sdk/issues/28450. If the directory's descriptor
                // is correctly configured, we should end up with an inherited ACE for Everyone: (A;ID;0x1200a9;;;WD)
                fs.SetOwner(s_AdministratorsSid);
                fs.SetGroup(s_AdministratorsSid);
                fi.SetAccessControl(fs);
            }
        }

        /// <summary>
        /// Determines if the workload pack MSI is cached and tries to retrieve its payload from the cache.
        /// </summary>
        /// <param name="packageId">The package ID of NuGet package carrying the MSI payload.</param>
        /// <param name="packageVersion">The version of the package.</param>
        /// <param name="payload">Contains the payload if the method returns <see langword="true"/>; otherwise the default value of <see cref="MsiPayload"/>.</param>
        /// <returns><see langwork="true"/> if the MSI is cached; <see langword="false"/> otherwise.</returns>
        public bool TryGetPayloadFromCache(string packageId, string packageVersion, out MsiPayload payload)
        {
            string packageCacheDirectory = GetPackageDirectory(packageId, packageVersion);
            payload = default;

            if (!TryGetMsiPathFromPackageData(packageCacheDirectory, out string msiPath, out string manifestPath))
            {
                return false;
            }

            VerifyPackageSignature(msiPath);

            payload = new MsiPayload(manifestPath, msiPath);

            return true;
        }

        public bool TryGetMsiPathFromPackageData(string packageDataPath, out string msiPath, out string manifestPath)
        {
            msiPath = default;
            manifestPath = Path.Combine(packageDataPath, "msi.json");

            // It's possible that the MSI is cached, but without the JSON manifest we cannot
            // trust that the MSI in the cache directory is the correct file.
            if (!File.Exists(manifestPath))
            {
                Log?.LogMessage($"MSI manifest file does not exist, '{manifestPath}'");
                return false;
            }

            // The msi.json manifest contains the name of the actual MSI. The filename does not necessarily match the package
            // ID as it may have been shortened to support VS caching.
            MsiManifest msiManifest = JsonConvert.DeserializeObject<MsiManifest>(File.ReadAllText(manifestPath));
            string possibleMsiPath = Path.Combine(Path.GetDirectoryName(manifestPath), msiManifest.Payload);

            if (!File.Exists(possibleMsiPath))
            {
                Log?.LogMessage($"MSI package not found, '{possibleMsiPath}'");
                return false;
            }

            msiPath = possibleMsiPath;
            return true;
        }

        /// <summary>
        /// Apply a standard set of access rules to the directory security descriptor. The owner and group will
        /// be set to built-in Administrators. Full access is granted to built-in administators and SYSTEM with
        /// read, execute, synchronize permssions for built-in users and Everyone.
        /// </summary>
        /// <param name="ds">The security descriptor to update.</param>
        private static void SetDirectoryAccessRules(DirectorySecurity ds)
        {
            ds.SetOwner(s_AdministratorsSid);
            ds.SetGroup(s_AdministratorsSid);
            ds.SetAccessRule(s_AdministratorRule);
            ds.SetAccessRule(s_LocalSystemRule);
            ds.SetAccessRule(s_UsersRule);
            ds.SetAccessRule(s_EveryoneRule);
        }

        /// <summary>
        /// Verifies the AuthentiCode signature of an MSI package if the executing command itself is running
        /// from a signed module.
        /// </summary>
        /// <param name="msiPath">The path of the MSI to verify.</param>
        private void VerifyPackageSignature(string msiPath)
        {
            if (VerifySignatures)
            {
                bool isAuthentiCodeSigned = AuthentiCode.IsSigned(msiPath);

                // Need to capture the error now as other OS calls might change the last error.
                uint lastError = !isAuthentiCodeSigned ? unchecked((uint)Marshal.GetLastWin32Error()) : Error.SUCCESS;

                bool isTrustedOrganization = AuthentiCode.IsSignedByTrustedOrganization(msiPath, AuthentiCode.TrustedOrganizations);

                if (isAuthentiCodeSigned && isTrustedOrganization)
                {
                    Log?.LogMessage($"Successfully verified AuthentiCode signature for {msiPath}.");
                }
                else
                {
                    // Summarize the failure and then report additional details.
                    Log?.LogMessage($"Failed to verify signature for {msiPath}. AuthentiCode signed: {isAuthentiCodeSigned}, Trusted organization: {isTrustedOrganization}.");
                    IEnumerable<X509Certificate2> certificates = AuthentiCode.GetCertificates(msiPath);

                    // Dump all the certificates if there are any.
                    if (certificates.Any())
                    {
                        Log?.LogMessage($"Certificate(s):");

                        foreach (X509Certificate2 certificate in certificates)
                        {
                            Log?.LogMessage($"       Subject={certificate.Subject}");
                            Log?.LogMessage($"        Issuer={certificate.Issuer}");
                            Log?.LogMessage($"    Not before={certificate.NotBefore}");
                            Log?.LogMessage($"     Not after={certificate.NotAfter}");
                            Log?.LogMessage($"    Thumbprint={certificate.Thumbprint}");
                            Log?.LogMessage($"     Algorithm={certificate.SignatureAlgorithm.FriendlyName}");
                        }
                    }

                    if (!isAuthentiCodeSigned)
                    {
                        // If it was a WinTrust failure, we can exit using that error code and include a proper message from the OS.
                        ExitOnError(lastError, $"Failed to verify authenticode signature for {msiPath}.");
                    }

                    if (!isTrustedOrganization)
                    {
                        throw new SecurityException(string.Format(LocalizableStrings.AuthentiCodeNoTrustedOrg, msiPath));
                    }
                }
            }
            else
            {
                Log?.LogMessage($"Skipping signature verification for {msiPath}.");
            }
        }
    }
}
