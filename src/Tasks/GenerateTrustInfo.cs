// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;

using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
#endif

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks
{
#if NETFRAMEWORK

    /// <summary>
    /// This task generates the application trust from the base manifest
    /// and the TargetZone and ExcludedPermissions properties.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public sealed class GenerateTrustInfo : TaskExtension, IGenerateTrustInfoTaskContract, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

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
            if (BaseManifest != null)
            {
                AbsolutePath baseManifestPath = TaskEnvironment.GetAbsolutePath(BaseManifest.ItemSpec);
                if (FileSystems.Default.FileExists(baseManifestPath))
                {
                    try
                    {
                        trustInfo.ReadManifest(baseManifestPath);
                    }
                    catch (Exception ex)
                    {
                        Log.LogErrorWithCodeFromResources("GenerateManifest.ReadInputManifestFailed", BaseManifest.ItemSpec, ex.Message);
                        return false;
                    }
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
            AbsolutePath trustInfoFilePath = TaskEnvironment.GetAbsolutePath(TrustInfoFile.ItemSpec);
            trustInfo.Write(trustInfoFilePath);

            return true;
        }
    }

#else

    [MSBuildMultiThreadableTask]
    public sealed class GenerateTrustInfo : TaskRequiresFramework, IGenerateTrustInfoTaskContract
    {
        public GenerateTrustInfo()
            : base(nameof(GenerateTrustInfo))
        {
        }

        #region Properties

        public ITaskItem BaseManifest { get; set; }

        public string ExcludedPermissions { get; set; }

        public string TargetFrameworkMoniker { get; set; }

        public string TargetZone { get; set; }

        public ITaskItem[] ApplicationDependencies { get; set; }

        [Output]
        public ITaskItem TrustInfoFile { get; set; }

        #endregion
    }

#endif

    internal interface IGenerateTrustInfoTaskContract
    {
        #region Properties

        ITaskItem BaseManifest { get; set; }
        string ExcludedPermissions { get; set; }
        string TargetFrameworkMoniker { get; set; }
        string TargetZone { get; set; }
        ITaskItem[] ApplicationDependencies { get; set; }
        ITaskItem TrustInfoFile { get; set; }

        #endregion
    }
}
