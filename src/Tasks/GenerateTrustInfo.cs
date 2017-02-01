// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This task generates the application trust from the base manifest
    /// and the TargetZone and ExcludedPermissions properties.
    /// </summary>
    public sealed class GenerateTrustInfo : TaskExtension
    {
        private ITaskItem _baseManifest = null;
        private string _excludedPermissions = null;
        private string _targetFrameworkMoniker;
        private string _targetZone;
        private ITaskItem _trustInfoFile;
        private ITaskItem[] _applicationDependencies;
        private const string Custom = "Custom";

        public ITaskItem BaseManifest
        {
            get { return _baseManifest; }
            set { _baseManifest = value; }
        }

        public string ExcludedPermissions
        {
            get { return _excludedPermissions; }
            set { _excludedPermissions = value; }
        }

        public string TargetFrameworkMoniker
        {
            get { return _targetFrameworkMoniker; }
            set { _targetFrameworkMoniker = value; }
        }

        public string TargetZone
        {
            get { return _targetZone; }
            set { _targetZone = value; }
        }

        public ITaskItem[] ApplicationDependencies
        {
            get { return _applicationDependencies; }
            set { _applicationDependencies = value; }
        }

        [Output]
        [Required]
        public ITaskItem TrustInfoFile
        {
            get { return _trustInfoFile; }
            set { _trustInfoFile = value; }
        }

        public GenerateTrustInfo()
        {
        }

        public override bool Execute()
        {
            TrustInfo trustInfo = new TrustInfo();
            trustInfo.IsFullTrust = false;
            FrameworkNameVersioning fn = null;
            string dotNetVersion = string.Empty;
            if (!string.IsNullOrEmpty(TargetFrameworkMoniker))
            {
                fn = new FrameworkNameVersioning(TargetFrameworkMoniker);
                dotNetVersion = fn.Version.ToString();
            }

            // Read trust-info from app.manifest
            if (BaseManifest != null && File.Exists(BaseManifest.ItemSpec))
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
                Log.LogWarningFromResources("GenerateManifest.ExcludedPermissionsNotSupported");

            try
            {
                // If it's a known zone and the user add additional permission to it.
                if (!String.IsNullOrEmpty(_targetZone)
                    && trustInfo.PermissionSet != null && trustInfo.PermissionSet.Count > 0
                    && !String.Equals(_targetZone, Custom, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogErrorFromResources("GenerateManifest.KnownTargetZoneCannotHaveAdditionalPermissionType");
                    return false;
                }
                else
                {
                    trustInfo.PermissionSet = SecurityUtilities.ComputeZonePermissionSetHelper(TargetZone, trustInfo.PermissionSet, _applicationDependencies, TargetFrameworkMoniker);
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
            catch (ArgumentException ex)
            {
                if (String.Equals(ex.ParamName, "TargetZone", StringComparison.OrdinalIgnoreCase))
                    Log.LogWarningWithCodeFromResources("GenerateManifest.InvalidItemValue", "TargetZone", TargetZone);
                else
                    throw;
            }

            // Write trust-info back to a stand-alone trust file
            trustInfo.Write(TrustInfoFile.ItemSpec);

            return true;
        }

        private static string[] StringToIdentityList(string s)
        {
            string[] a = s.Split(';');
            for (int i = 0; i < a.Length; ++i)
                a[i] = a[i].Trim();
            return a;
        }
    }
}
