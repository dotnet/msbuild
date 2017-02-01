// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Updates selected properties in a manifest and resigns.
    /// </summary>
    public class UpdateManifest : Task
    {
        private string _applicationPath;
        private string _targetFrameworkVersion;
        private ITaskItem _applicationManifest;
        private ITaskItem _inputManifest;
        private ITaskItem _outputManifest;

        [Required]
        public string ApplicationPath
        {
            get { return _applicationPath; }
            set { _applicationPath = value; }
        }

        public string TargetFrameworkVersion
        {
            get { return _targetFrameworkVersion; }
            set { _targetFrameworkVersion = value; }
        }

        [Required]
        public ITaskItem ApplicationManifest
        {
            get { return _applicationManifest; }
            set { _applicationManifest = value; }
        }

        [Required]
        public ITaskItem InputManifest
        {
            get { return _inputManifest; }
            set { _inputManifest = value; }
        }

        [Output]
        public ITaskItem OutputManifest
        {
            get { return _outputManifest; }
            set { _outputManifest = value; }
        }

        public override bool Execute()
        {
            Manifest.UpdateEntryPoint(InputManifest.ItemSpec, OutputManifest.ItemSpec, ApplicationPath, ApplicationManifest.ItemSpec, _targetFrameworkVersion);

            return true;
        }
    }
}

