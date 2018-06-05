// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This task determines if this project requires VS2008 SP1 assembly.
    /// </summary>
    public sealed class RequiresFramework35SP1Assembly : TaskExtension
    {
        #region Fields

        private string _targetFrameworkVersion = Constants.TargetFrameworkVersion20;
        private bool? _createDesktopShortcut;

        #endregion

        #region Properties

        public string ErrorReportUrl { get; set; }

        public string TargetFrameworkVersion
        {
            get
            {
                if (string.IsNullOrEmpty(_targetFrameworkVersion))
                {
                    return Constants.TargetFrameworkVersion35;
                }
                return _targetFrameworkVersion;
            }
            set => _targetFrameworkVersion = value;
        }

        public bool CreateDesktopShortcut
        {
            get
            {
                if (!_createDesktopShortcut.HasValue)
                {
                    return false;
                }
                if (CompareFrameworkVersions(TargetFrameworkVersion, Constants.TargetFrameworkVersion35) < 0)
                {
                    return false;
                }
                return (bool)_createDesktopShortcut;
            }
            set => _createDesktopShortcut = value;
        }

        public bool SigningManifests { get; set; }

        public ITaskItem[] ReferencedAssemblies { get; set; }

        public ITaskItem[] Assemblies { get; set; }

        public ITaskItem DeploymentManifestEntryPoint { get; set; }

        public ITaskItem EntryPoint { get; set; }

        public ITaskItem[] Files { get; set; }

        public string SuiteName { get; set; }

        [Output]
        public bool RequiresMinimumFramework35SP1 { get; set; }

        #endregion

        #region helper
        private static Version ConvertFrameworkVersionToString(string version)
        {
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return new Version(version.Substring(1));
            }
            return new Version(version);
        }

        private static int CompareFrameworkVersions(string versionA, string versionB)
        {
            Version version1 = ConvertFrameworkVersionToString(versionA);
            Version version2 = ConvertFrameworkVersionToString(versionB);
            return version1.CompareTo(version2);
        }

        private bool HasErrorUrl()
        {
            if (string.IsNullOrEmpty(ErrorReportUrl))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool HasCreatedShortcut()
        {
            return CreateDesktopShortcut;
        }

        private bool UncheckedSigning()
        {
            return !SigningManifests;
        }

        private bool ExcludeReferenceFromHashing()
        {
            if (HasExcludedFileOrSP1File(ReferencedAssemblies) ||
                HasExcludedFileOrSP1File(Assemblies) ||
                HasExcludedFileOrSP1File(Files) ||
                IsExcludedFileOrSP1File(DeploymentManifestEntryPoint) ||
                IsExcludedFileOrSP1File(EntryPoint))
            {
                return true;
            }

            return false;
        }

        private static bool HasExcludedFileOrSP1File(ITaskItem[] candidateFiles)
        {
            if (candidateFiles != null)
            {
                foreach (ITaskItem file in candidateFiles)
                {
                    if (IsExcludedFileOrSP1File(file))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Is this file's IncludeHash set to false?
        /// Is this file System.Data.Entity.dll?
        /// Is this file Client Sentinel Assembly? 
        /// </summary>
        private static bool IsExcludedFileOrSP1File(ITaskItem candidateFile)
        {
            if (candidateFile != null &&
                (string.Equals(candidateFile.GetMetadata("IncludeHash"), "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidateFile.ItemSpec, Constants.NET35SP1AssemblyIdentity[0], StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidateFile.ItemSpec, Constants.NET35ClientAssemblyIdentity[0], StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private bool HasSuiteName()
        {
            return !string.IsNullOrEmpty(SuiteName);
        }

        #endregion

        public override bool Execute()
        {
            RequiresMinimumFramework35SP1 = HasErrorUrl() || HasCreatedShortcut() || UncheckedSigning() || ExcludeReferenceFromHashing() || HasSuiteName();

            return true;
        }
    }
}
