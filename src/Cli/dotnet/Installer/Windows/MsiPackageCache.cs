// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
        /// Creates the root package cache directory if it does not exist and configures the directory's ACLs. ACLs are still configured
        /// if the directory exists.
        /// </summary>
        private void CreateRootDirectory()
        {
            CreateSecureDirectory(PackageCacheRoot);
        }

        /// <summary>
        /// Creates the specified directory and secures it by configuring access rules (ACLs). If the parent
        /// of the directory does not exist, it recursively walks the path back to ensure each parent directory
        /// is created with the proper ACLs and inheritance settings.
        /// </summary>
        /// <param name="path">The path of the directory to create.</param>
        private void CreateSecureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                CreateSecureDirectory(Directory.GetParent(path).FullName);

                DirectorySecurity directorySecurity = new();
                directorySecurity.SetAccessRule(s_AdministratorRule);
                directorySecurity.SetGroup(s_LocalSystemSid);
                directorySecurity.CreateDirectory(path);
                SecureDirectory(path);
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
                CreateRootDirectory();

                string packageDirectory = GetPackageDirectory(packageId, packageVersion);

                // Delete the directory and create a new one that's secure. If the files were properly
                // cached, the client won't request this action.
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

                MoveFile(manifestPath, cachedManifestPath);
                MoveFile(msiPath, cachedMsiPath);
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
        /// Moves a file from one location to another if the destination file does not already exist.
        /// </summary>
        /// <param name="sourceFile">The source file to move.</param>
        /// <param name="destinationFile">The destination where the source file will be moved.</param>
        protected void MoveFile(string sourceFile, string destinationFile)
        {
            if (!File.Exists(destinationFile))
            {
                FileAccessRetrier.RetryOnMoveAccessFailure(() => File.Move(sourceFile, destinationFile));
                Log?.LogMessage($"Moved '{sourceFile}' to '{destinationFile}'");
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

            string msiPath;
            if (!TryGetMsiPathFromPackageData(packageCacheDirectory, out msiPath, out string manifestPath))
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

        /// <summary>
        /// Secures the target directory by applying multiple ACLs. Administrators and local SYSTEM
        /// receive full control. Users and Everyone receive read and execute permissions.
        /// </summary>
        /// <param name="path">The directory to secure.</param>
        private void SecureDirectory(string path)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            DirectorySecurity directorySecurity = directoryInfo.GetAccessControl();
            directorySecurity.SetAccessRule(s_AdministratorRule);
            directorySecurity.SetAccessRule(s_EveryoneRule);
            directorySecurity.SetAccessRule(s_LocalSystemRule);
            directorySecurity.SetAccessRule(s_UsersRule);
            directoryInfo.SetAccessControl(directorySecurity);
        }
    }
}
