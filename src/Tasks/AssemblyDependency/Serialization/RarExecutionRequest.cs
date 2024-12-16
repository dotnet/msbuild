// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal class RarExecutionRequest : RarSerializableMessageBase
    {
        private bool _enableMetadataInterning;
        private bool _autoUnify;
        private bool _copyLocalDependenciesWhenParentReferenceInGac;
        private bool _doNotCopyLocalIfInGac;
        private bool _findDependencies;
        private bool _findDependenciesOfExternallyResolvedReferences;
        private bool _findRelatedFiles;
        private bool _findSatellites;
        private bool _findSerializationAssemblies;
        private bool _ignoreDefaultInstalledAssemblySubsetTables;
        private bool _ignoreDefaultInstalledAssemblyTables;
        private bool _ignoreTargetFrameworkAttributeVersionMismatch;
        private bool _ignoreVersionForFrameworkReferences;
        private bool _silent;
        private bool _supportsBindingRedirectGeneration;
        private bool _unresolveFrameworkAssembliesFromHigherFrameworks;
        private bool _isTaskLoggingEnabled;
        private MessageImportance _minimumMessageImportance;
        private string _msBuildProjectFile = string.Empty;
        private string? _appConfigFile;
        private string? _profileName;
        private string? _stateFile;
        private string? _targetedRuntimeVersion;
        private string? _targetFrameworkMoniker;
        private string? _targetFrameworkMonikerDisplayName;
        private string? _targetFrameworkVersion;
        private string? _targetProcessorArchitecture;
        private string? _warnOrErrorOnTargetArchitectureMismatch;
        private string[] _allowedAssemblyExtensions = [];
        private string[] _allowedRelatedFileExtensions = [];
        private RarTaskItemInput[] _assemblies = [];
        private RarTaskItemInput[] _assemblyFiles = [];
        private string[] _candidateAssemblyFiles = [];
        private RarTaskItemInput[] _fullFrameworkAssemblyTables = [];
        private string[] _fullFrameworkFolders = [];
        private string[] _fullTargetFrameworkSubsetNames = [];
        private RarTaskItemInput[] _installedAssemblySubsetTables = [];
        private RarTaskItemInput[] _installedAssemblyTables = [];
        private string[] _searchPaths = [];
        private string[] _targetFrameworkDirectories = [];
        private RarTaskItemInput[] _resolvedSDKReferences = [];
        private string[] _latestTargetFrameworkDirectories = [];
        private string[] _targetFrameworkSubsets = [];

        public bool AutoUnify { get => _autoUnify; set => _autoUnify = value; }

        public bool CopyLocalDependenciesWhenParentReferenceInGac { get => _copyLocalDependenciesWhenParentReferenceInGac; set => _copyLocalDependenciesWhenParentReferenceInGac = value; }

        public bool DoNotCopyLocalIfInGac { get => _doNotCopyLocalIfInGac; set => _doNotCopyLocalIfInGac = value; }

        public bool FindDependencies { get => _findDependencies; set => _findDependencies = value; }

        public bool FindDependenciesOfExternallyResolvedReferences { get => _findDependenciesOfExternallyResolvedReferences; set => _findDependenciesOfExternallyResolvedReferences = value; }

        public bool FindRelatedFiles { get => _findRelatedFiles; set => _findRelatedFiles = value; }

        public bool FindSatellites { get => _findSatellites; set => _findSatellites = value; }

        public bool FindSerializationAssemblies { get => _findSerializationAssemblies; set => _findSerializationAssemblies = value; }

        public bool IgnoreDefaultInstalledAssemblySubsetTables { get => _ignoreDefaultInstalledAssemblySubsetTables; set => _ignoreDefaultInstalledAssemblySubsetTables = value; }

        public bool IgnoreDefaultInstalledAssemblyTables { get => _ignoreDefaultInstalledAssemblyTables; set => _ignoreDefaultInstalledAssemblyTables = value; }

        public bool IgnoreTargetFrameworkAttributeVersionMismatch { get => _ignoreTargetFrameworkAttributeVersionMismatch; set => _ignoreTargetFrameworkAttributeVersionMismatch = value; }

        public bool IgnoreVersionForFrameworkReferences { get => _ignoreVersionForFrameworkReferences; set => _ignoreVersionForFrameworkReferences = value; }

        public bool Silent { get => _silent; set => _silent = value; }

        public bool SupportsBindingRedirectGeneration { get => _supportsBindingRedirectGeneration; set => _supportsBindingRedirectGeneration = value; }

        public bool UnresolveFrameworkAssembliesFromHigherFrameworks { get => _unresolveFrameworkAssembliesFromHigherFrameworks; set => _unresolveFrameworkAssembliesFromHigherFrameworks = value; }

        public bool IsTaskLoggingEnabled { get => _isTaskLoggingEnabled; set => _isTaskLoggingEnabled = value; }

        public MessageImportance MinimumMessageImportance { get => _minimumMessageImportance; set => _minimumMessageImportance = value; }

        public string TargetPath { get => _msBuildProjectFile; set => _msBuildProjectFile = value; }

        public string? AppConfigFile { get => _appConfigFile; set => _appConfigFile = value; }

        public string? ProfileName { get => _profileName; set => _profileName = value; }

        public string? StateFile { get => _stateFile; set => _stateFile = value; }

        public string? TargetedRuntimeVersion { get => _targetedRuntimeVersion; set => _targetedRuntimeVersion = value; }

        public string? TargetFrameworkMoniker { get => _targetFrameworkMoniker; set => _targetFrameworkMoniker = value; }

        public string? TargetFrameworkMonikerDisplayName { get => _targetFrameworkMonikerDisplayName; set => _targetFrameworkMonikerDisplayName = value; }

        public string? TargetFrameworkVersion { get => _targetFrameworkVersion; set => _targetFrameworkVersion = value; }

        public string? TargetProcessorArchitecture { get => _targetProcessorArchitecture; set => _targetProcessorArchitecture = value; }

        public string? WarnOrErrorOnTargetArchitectureMismatch { get => _warnOrErrorOnTargetArchitectureMismatch; set => _warnOrErrorOnTargetArchitectureMismatch = value; }

        public string[] AllowedAssemblyExtensions { get => _allowedAssemblyExtensions; set => _allowedAssemblyExtensions = value; }

        public string[] AllowedRelatedFileExtensions { get => _allowedRelatedFileExtensions; set => _allowedRelatedFileExtensions = value; }

        public RarTaskItemInput[] Assemblies { get => _assemblies; set => _assemblies = value; }

        public RarTaskItemInput[] AssemblyFiles { get => _assemblyFiles; set => _assemblyFiles = value; }

        public string[] CandidateAssemblyFiles { get => _candidateAssemblyFiles; set => _candidateAssemblyFiles = value; }

        public RarTaskItemInput[] FullFrameworkAssemblyTables { get => _fullFrameworkAssemblyTables; set => _fullFrameworkAssemblyTables = value; }

        public string[] FullFrameworkFolders { get => _fullFrameworkFolders; set => _fullFrameworkFolders = value; }

        public string[] FullTargetFrameworkSubsetNames { get => _fullTargetFrameworkSubsetNames; set => _fullTargetFrameworkSubsetNames = value; }

        public RarTaskItemInput[] InstalledAssemblyTables { get => _installedAssemblyTables; set => _installedAssemblyTables = value; }

        public RarTaskItemInput[] InstalledAssemblySubsetTables { get => _installedAssemblySubsetTables; set => _installedAssemblySubsetTables = value; }

        public string[] LatestTargetFrameworkDirectories { get => _latestTargetFrameworkDirectories; set => _latestTargetFrameworkDirectories = value; }

        public RarTaskItemInput[] ResolvedSDKReferences { get => _resolvedSDKReferences; set => _resolvedSDKReferences = value; }

        public string[] SearchPaths { get => _searchPaths; set => _searchPaths = value; }

        public string[] TargetFrameworkDirectories { get => _targetFrameworkDirectories; set => _targetFrameworkDirectories = value; }

        public string[] TargetFrameworkSubsets { get => _targetFrameworkSubsets; set => _targetFrameworkSubsets = value; }

        public bool EnableMetadataInterning { get => _enableMetadataInterning; set => _enableMetadataInterning = value; }

        public override NodePacketType Type => NodePacketType.RarNodeExecutionRequest;

        public override void Translate(ITranslator translator)
        {
            // TODO: String interning needs further design and should only apply to metadata known to contain duplicates.
            // TODO: For now it is always disabled.
            translator.Translate(ref _enableMetadataInterning);
            RarMetadataInternCache? internCache = _enableMetadataInterning ? new() : null;

            if (internCache != null)
            {
                internCache = new();

                if (translator.Mode == TranslationDirection.WriteToStream)
                {
                    InternTaskItems(_assemblies, internCache);
                    InternTaskItems(_assemblyFiles, internCache);
                    InternTaskItems(_fullFrameworkAssemblyTables, internCache);
                    InternTaskItems(_installedAssemblyTables, internCache);
                    InternTaskItems(_installedAssemblySubsetTables, internCache);
                    InternTaskItems(_resolvedSDKReferences, internCache);
                }

                translator.Translate(ref internCache);
            }

            translator.Translate(ref _autoUnify);
            translator.Translate(ref _copyLocalDependenciesWhenParentReferenceInGac);
            translator.Translate(ref _doNotCopyLocalIfInGac);
            translator.Translate(ref _findDependencies);
            translator.Translate(ref _findDependenciesOfExternallyResolvedReferences);
            translator.Translate(ref _findRelatedFiles);
            translator.Translate(ref _findSatellites);
            translator.Translate(ref _findSerializationAssemblies);
            translator.Translate(ref _ignoreDefaultInstalledAssemblySubsetTables);
            translator.Translate(ref _ignoreDefaultInstalledAssemblyTables);
            translator.Translate(ref _ignoreTargetFrameworkAttributeVersionMismatch);
            translator.Translate(ref _ignoreVersionForFrameworkReferences);
            translator.Translate(ref _silent);
            translator.Translate(ref _supportsBindingRedirectGeneration);
            translator.Translate(ref _unresolveFrameworkAssembliesFromHigherFrameworks);
            translator.Translate(ref _isTaskLoggingEnabled);
            translator.TranslateEnum(ref _minimumMessageImportance, (int)_minimumMessageImportance);
            translator.Translate(ref _msBuildProjectFile);
            translator.Translate(ref _appConfigFile);
            translator.Translate(ref _profileName);
            translator.Translate(ref _stateFile);
            translator.Translate(ref _targetedRuntimeVersion);
            translator.Translate(ref _targetFrameworkMoniker);
            translator.Translate(ref _targetFrameworkMonikerDisplayName);
            translator.Translate(ref _targetFrameworkVersion);
            translator.Translate(ref _targetProcessorArchitecture);
            translator.Translate(ref _warnOrErrorOnTargetArchitectureMismatch);
            translator.Translate(ref _allowedAssemblyExtensions);
            translator.Translate(ref _allowedRelatedFileExtensions);
            translator.TranslateArray(ref _assemblies);
            translator.TranslateArray(ref _assemblyFiles);
            translator.Translate(ref _candidateAssemblyFiles);
            translator.TranslateArray(ref _fullFrameworkAssemblyTables);
            translator.Translate(ref _fullFrameworkFolders);
            translator.Translate(ref _fullTargetFrameworkSubsetNames);
            translator.TranslateArray(ref _installedAssemblyTables);
            translator.TranslateArray(ref _installedAssemblySubsetTables);
            translator.Translate(ref _latestTargetFrameworkDirectories);
            translator.TranslateArray(ref _resolvedSDKReferences);
            translator.Translate(ref _searchPaths);
            translator.Translate(ref _targetFrameworkDirectories);
            translator.Translate(ref _targetFrameworkSubsets);

            if (internCache != null && translator.Mode == TranslationDirection.ReadFromStream)
            {
                PopulateTaskItems(_assemblies, internCache);
                PopulateTaskItems(_assemblyFiles, internCache);
                PopulateTaskItems(_fullFrameworkAssemblyTables, internCache);
                PopulateTaskItems(_installedAssemblyTables, internCache);
                PopulateTaskItems(_installedAssemblySubsetTables, internCache);
                PopulateTaskItems(_resolvedSDKReferences, internCache);
            }
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            RarExecutionRequest request = new();
            request.Translate(translator);

            return request;
        }
    }
}