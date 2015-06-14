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
        private string _errorReportUrl;
        private string _targetFrameworkVersion = Constants.TargetFrameworkVersion20;
        private bool? _createDesktopShortcut;
        private bool _signingManifests;
        private bool _outputRequiresMinimumFramework35SP1;

        private ITaskItem[] _referencedAssemblies;
        private ITaskItem[] _assemblies;
        private ITaskItem _deploymentManifestEntryPoint;
        private ITaskItem _entryPoint;
        private ITaskItem[] _files;
        private string _suiteName;
        #endregion

        #region Properties

        public string ErrorReportUrl
        {
            get { return _errorReportUrl; }
            set { _errorReportUrl = value; }
        }

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
            set { _targetFrameworkVersion = value; }
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
            set { _createDesktopShortcut = value; }
        }

        public bool SigningManifests
        {
            get { return _signingManifests; }
            set { _signingManifests = value; }
        }

        public ITaskItem[] ReferencedAssemblies
        {
            get { return _referencedAssemblies; }
            set { _referencedAssemblies = value; }
        }

        public ITaskItem[] Assemblies
        {
            get { return _assemblies; }
            set { _assemblies = value; }
        }

        public ITaskItem DeploymentManifestEntryPoint
        {
            get { return _deploymentManifestEntryPoint; }
            set { _deploymentManifestEntryPoint = value; }
        }

        public ITaskItem EntryPoint
        {
            get { return _entryPoint; }
            set { _entryPoint = value; }
        }

        public ITaskItem[] Files
        {
            get { return _files; }
            set { _files = value; }
        }

        public string SuiteName
        {
            get { return _suiteName; }
            set { _suiteName = value; }
        }

        [Output]
        public bool RequiresMinimumFramework35SP1
        {
            get { return _outputRequiresMinimumFramework35SP1; }
            set { _outputRequiresMinimumFramework35SP1 = value; }
        }

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
            if (HasExcludedFileOrSP1File(_referencedAssemblies) ||
                HasExcludedFileOrSP1File(_assemblies) ||
                HasExcludedFileOrSP1File(_files) ||
                IsExcludedFileOrSP1File(_deploymentManifestEntryPoint) ||
                IsExcludedFileOrSP1File(_entryPoint))
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
        /// <param name="file"></param>
        /// <returns></returns>
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

        public RequiresFramework35SP1Assembly()
        {
        }

        public override bool Execute()
        {
            _outputRequiresMinimumFramework35SP1 = false;

            if (HasErrorUrl() || HasCreatedShortcut() || UncheckedSigning() || ExcludeReferenceFromHashing() || HasSuiteName())
            {
                _outputRequiresMinimumFramework35SP1 = true;
            }

            return true;
        }
    }
}
