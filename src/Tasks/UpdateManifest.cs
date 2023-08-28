// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#if NETFRAMEWORK
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;
#endif

#nullable disable

namespace Microsoft.Build.Tasks
{
#if NETFRAMEWORK

    /// <summary>
    /// Updates selected properties in a manifest and resigns.
    /// </summary>
    public class UpdateManifest : Task, IUpdateManifestTaskContract
    {
        [Required]
        public string ApplicationPath { get; set; }

        public string TargetFrameworkVersion { get; set; }

        [Required]
        public ITaskItem ApplicationManifest { get; set; }

        [Required]
        public ITaskItem InputManifest { get; set; }

        [Output]
        public ITaskItem OutputManifest { get; set; }

        public override bool Execute()
        {
            Manifest.UpdateEntryPoint(InputManifest.ItemSpec, OutputManifest.ItemSpec, ApplicationPath, ApplicationManifest.ItemSpec, TargetFrameworkVersion);

            return true;
        }
    }

#else

    public sealed class UpdateManifest : TaskRequiresFramework, IUpdateManifestTaskContract
    {
        public UpdateManifest()
            : base(nameof(UpdateManifest))
        {
        }

        #region Properties

        [Required]
        public string ApplicationPath { get; set; }

        [Required]
        public string TargetFrameworkVersion { get; set; }

        [Required]
        public ITaskItem ApplicationManifest { get; set; }

        [Required]
        public ITaskItem InputManifest { get; set; }

        [Output]
        public ITaskItem OutputManifest { get; set; }

        #endregion
    }

#endif

    internal interface IUpdateManifestTaskContract
    {
        #region Properties

        string ApplicationPath { get; set; }
        string TargetFrameworkVersion { get; set; }
        ITaskItem ApplicationManifest { get; set; }
        ITaskItem InputManifest { get; set; }
        ITaskItem OutputManifest { get; set; }

        #endregion
    }
}
