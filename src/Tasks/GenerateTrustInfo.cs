﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This task generates the application trust from the base manifest
    /// and the TargetZone and ExcludedPermissions properties.
    /// </summary>
    public sealed class GenerateTrustInfo : TaskExtension
    {
        private const string Custom = "Custom";

        public ITaskItem BaseManifest { get; set; }

        public string ExcludedPermissions { get; set; }

        public string TargetFrameworkMoniker { get; set; }

        public string TargetZone { get; set; }

        public ITaskItem[] ApplicationDependencies { get; set; }

        [Output]
        [Required]
        public ITaskItem TrustInfoFile { get; set; }

        public override bool Execute()
        {
            var trustInfo = new TrustInfo { IsFullTrust = false };
            string dotNetVersion = string.Empty;
            if (!string.IsNullOrEmpty(TargetFrameworkMoniker))
            {
                var fn = new FrameworkNameVersioning(TargetFrameworkMoniker);
                dotNetVersion = fn.Version.ToString();
            }

            // Read trust-info from app.manifest
            if (BaseManifest != null && FileSystems.Default.FileExists(BaseManifest.ItemSpec))
            {
                try
                {
                    trustInfo.ReadManifest(BaseManifest.ItemSpec);
                }
                catch (Exception ex)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.ReadInputManifestFailed", BaseManifest.ItemSpec, ex.Message);
                    return false;
                }
            }

            if (!String.IsNullOrEmpty(ExcludedPermissions))
            {
                Log.LogWarningFromResources("GenerateManifest.ExcludedPermissionsNotSupported");
            }

            try
            {
                // If it's a known zone and the user add additional permission to it.
                if (!String.IsNullOrEmpty(TargetZone)
                    && trustInfo.PermissionSet?.Count > 0
                    && !String.Equals(TargetZone, Custom, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogErrorFromResources("GenerateManifest.KnownTargetZoneCannotHaveAdditionalPermissionType");
                    return false;
                }
                else
                {
                    trustInfo.PermissionSet = SecurityUtilities.ComputeZonePermissionSetHelper(TargetZone, trustInfo.PermissionSet, ApplicationDependencies, TargetFrameworkMoniker);
                    if (trustInfo.PermissionSet == null)
                    {
                        Log.LogErrorWithCodeFromResources("GenerateManifest.NoPermissionSetForTargetZone", dotNetVersion);
                        return false;
                    }
                }
            }
            catch (ArgumentNullException)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.NoPermissionSetForTargetZone", dotNetVersion);
                return false;
            }
            catch (ArgumentException ex) when (String.Equals(ex.ParamName, "TargetZone", StringComparison.OrdinalIgnoreCase))
            {
                Log.LogWarningWithCodeFromResources("GenerateManifest.InvalidItemValue", "TargetZone", TargetZone);
            }

            // Write trust-info back to a stand-alone trust file
            trustInfo.Write(TrustInfoFile.ItemSpec);

            return true;
        }
    }
}
