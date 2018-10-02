// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates a deploy manifest for ClickOnce projects.
    /// </summary>
    public sealed class GenerateDeploymentManifest : GenerateManifestBase
    {
        private bool? _createDesktopShortcut;
        private bool? _disallowUrlActivation;
        private string _errorReportUrl;
        private bool? _install;
        private bool? _mapFileExtensions;
        private string _suiteName;
        private bool? _trustUrlParameters;
        private bool? _updateEnabled;
        private int? _updateInterval;
        private UpdateMode? _updateMode;
        private UpdateUnit? _updateUnit;

        public bool CreateDesktopShortcut
        {
            get
            {
                if (!_createDesktopShortcut.HasValue)
                {
                    return false;
                }

                if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) < 0)
                {
                    return false;
                }
                return (bool)_createDesktopShortcut;
            }
            set => _createDesktopShortcut = value;
        }

        public string DeploymentUrl { get; set; }

        public bool DisallowUrlActivation
        {
            get => (bool)_disallowUrlActivation;
            set => _disallowUrlActivation = value;
        }

        public string ErrorReportUrl
        {
            get
            {
                if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) < 0)
                {
                    return null;
                }
                return _errorReportUrl;
            }
            set => _errorReportUrl = value;
        }

        public bool Install
        {
            get => (bool)_install;
            set => _install = value;
        }

        public string MinimumRequiredVersion { get; set; } = null;

        public bool MapFileExtensions
        {
            get => (bool)_mapFileExtensions;
            set => _mapFileExtensions = value;
        }

        public string Product { get; set; }

        public string Publisher { get; set; }

        public string SuiteName
        {
            get
            {
                if (Util.CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) < 0)
                {
                    return null;
                }
                return _suiteName;
            }
            set => _suiteName = value;
        }

        public string SupportUrl { get; set; } = null;

        public bool TrustUrlParameters
        {
            get => (bool)_trustUrlParameters;
            set => _trustUrlParameters = value;
        }

        public bool UpdateEnabled
        {
            get => (bool)_updateEnabled;
            set => _updateEnabled = value;
        }

        public int UpdateInterval
        {
            get => (int)_updateInterval;
            set => _updateInterval = value;
        }

        public string UpdateMode { get; set; }

        public string UpdateUnit { get; set; }

        private bool BuildResolvedSettings(DeployManifest manifest)
        {
            // Note: if changing the logic in this function, please update the logic in 
            //  GenerateApplicationManifest.BuildResolvedSettings as well.
            if (Product != null)
            {
                manifest.Product = Product;
            }
            else if (String.IsNullOrEmpty(manifest.Product))
            {
                manifest.Product = Path.GetFileNameWithoutExtension(manifest.AssemblyIdentity.Name);
            }
            Debug.Assert(!String.IsNullOrEmpty(manifest.Product));

            if (Publisher != null)
            {
                manifest.Publisher = Publisher;
            }
            else if (String.IsNullOrEmpty(manifest.Publisher))
            {
                string org = Util.GetRegisteredOrganization();
                manifest.Publisher = !String.IsNullOrEmpty(org) ? org : manifest.Product;
            }
            Debug.Assert(!String.IsNullOrEmpty(manifest.Publisher));

            return true;
        }

        protected override Type GetObjectType()
        {
            return typeof(DeployManifest);
        }

        protected override bool OnManifestLoaded(Manifest manifest)
        {
            return BuildDeployManifest(manifest as DeployManifest);
        }

        protected override bool OnManifestResolved(Manifest manifest)
        {
            return BuildResolvedSettings(manifest as DeployManifest);
        }

        private bool BuildDeployManifest(DeployManifest manifest)
        {
            if (manifest.EntryPoint == null)
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.NoEntryPoint");
                return false;
            }

            if (SupportUrl != null)
            {
                manifest.SupportUrl = SupportUrl;
            }

            if (DeploymentUrl != null)
            {
                manifest.DeploymentUrl = DeploymentUrl;
            }

            if (_install.HasValue)
            {
                manifest.Install = (bool)_install;
            }

            if (_updateEnabled.HasValue)
            {
                manifest.UpdateEnabled = (bool)_updateEnabled;
            }

            if (_updateInterval.HasValue)
            {
                manifest.UpdateInterval = (int)_updateInterval;
            }

            if (_updateMode.HasValue)
            {
                manifest.UpdateMode = (UpdateMode)_updateMode;
            }

            if (_updateUnit.HasValue)
            {
                manifest.UpdateUnit = (UpdateUnit)_updateUnit;
            }

            if (MinimumRequiredVersion != null)
            {
                manifest.MinimumRequiredVersion = MinimumRequiredVersion;
            }

            if (manifest.Install) // Ignore DisallowUrlActivation flag for online-only apps
            {
                if (_disallowUrlActivation.HasValue)
                {
                    manifest.DisallowUrlActivation = (bool)_disallowUrlActivation;
                }
            }

            if (_mapFileExtensions.HasValue)
            {
                manifest.MapFileExtensions = (bool)_mapFileExtensions;
            }

            if (_trustUrlParameters.HasValue)
            {
                manifest.TrustUrlParameters = (bool)_trustUrlParameters;
            }

            if (_createDesktopShortcut.HasValue)
            {
                manifest.CreateDesktopShortcut = CreateDesktopShortcut;
            }

            if (SuiteName != null)
            {
                manifest.SuiteName = SuiteName;
            }

            if (ErrorReportUrl != null)
            {
                manifest.ErrorReportUrl = ErrorReportUrl;
            }

            return true;
        }

        protected internal override bool ValidateInputs()
        {
            bool valid = base.ValidateInputs();
            if (!String.IsNullOrEmpty(MinimumRequiredVersion) && !Util.IsValidVersion(MinimumRequiredVersion, 4))
            {
                Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "MinimumRequiredVersion");
                valid = false;
            }
            if (UpdateMode != null)
            {
                try
                {
                    _updateMode = (UpdateMode)Enum.Parse(typeof(UpdateMode), UpdateMode, true);
                }
                catch (FormatException)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "UpdateMode");
                    valid = false;
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "UpdateMode");
                    valid = false;
                }
            }
            if (UpdateUnit != null)
            {
                try
                {
                    _updateUnit = (UpdateUnit)Enum.Parse(typeof(UpdateUnit), UpdateUnit, true);
                }
                catch (FormatException)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "UpdateUnit");
                    valid = false;
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("GenerateManifest.InvalidValue", "UpdateUnit");
                    valid = false;
                }
            }
            return valid;
        }
    }
}
