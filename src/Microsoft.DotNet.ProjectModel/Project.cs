// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.ProjectModel.Files;
using Microsoft.Extensions.ProjectModel.Graph;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel
{
    public class Project
    {
        public static readonly string FileName = "project.json";

        // REVIEW: It's kinda hacky making these internal but the reader needs to set them
        internal Dictionary<NuGetFramework, TargetFrameworkInformation> _targetFrameworks = new Dictionary<NuGetFramework, TargetFrameworkInformation>();
        internal Dictionary<string, CompilerOptions> _compilerOptionsByConfiguration = new Dictionary<string, CompilerOptions>(StringComparer.OrdinalIgnoreCase);

        internal CompilerOptions _defaultCompilerOptions;
        internal TargetFrameworkInformation _defaultTargetFrameworkConfiguration;

        public Project()
        {
        }

        public string ProjectFilePath { get; set; }

        public string ProjectDirectory
        {
            get
            {
                return Path.GetDirectoryName(ProjectFilePath);
            }
        }

        public string Name { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Copyright { get; set; }

        public string Summary { get; set; }

        public string Language { get; set; }

        public string ReleaseNotes { get; set; }

        public string[] Authors { get; set; }

        public string[] Owners { get; set; }

        public bool EmbedInteropTypes { get; set; }

        public NuGetVersion Version { get; set; }

        public Version AssemblyFileVersion { get; set; }

        public IList<LibraryRange> Dependencies { get; set; }

        public string EntryPoint { get; set; }

        public string ProjectUrl { get; set; }

        public string LicenseUrl { get; set; }

        public string IconUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string[] Tags { get; set; }

        public string CompilerName { get; set; }

        public ProjectFilesCollection Files { get; set; }

        public IDictionary<string, string> Commands { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, IEnumerable<string>> Scripts { get; } = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<TargetFrameworkInformation> GetTargetFrameworks()
        {
            return _targetFrameworks.Values;
        }

        public IEnumerable<string> GetConfigurations()
        {
            return _compilerOptionsByConfiguration.Keys;
        }

        public CompilerOptions GetCompilerOptions(NuGetFramework targetFramework,
                                                   string configurationName)
        {
            // Get all project options and combine them
            var rootOptions = GetCompilerOptions();
            var configurationOptions = configurationName != null ? GetCompilerOptions(configurationName) : null;
            var targetFrameworkOptions = targetFramework != null ? GetCompilerOptions(targetFramework) : null;

            // Combine all of the options
            return CompilerOptions.Combine(rootOptions, configurationOptions, targetFrameworkOptions);
        }

        public TargetFrameworkInformation GetTargetFramework(NuGetFramework targetFramework)
        {
            TargetFrameworkInformation targetFrameworkInfo = null;
            if (targetFramework != null && _targetFrameworks.TryGetValue(targetFramework, out targetFrameworkInfo))
            {
                return targetFrameworkInfo;
            }

            return targetFrameworkInfo ?? _defaultTargetFrameworkConfiguration;
        }

        private CompilerOptions GetCompilerOptions()
        {
            return _defaultCompilerOptions;
        }

        private CompilerOptions GetCompilerOptions(string configurationName)
        {
            CompilerOptions options;
            if (_compilerOptionsByConfiguration.TryGetValue(configurationName, out options))
            {
                return options;
            }

            return null;
        }

        private CompilerOptions GetCompilerOptions(NuGetFramework frameworkName)
        {
            return GetTargetFramework(frameworkName)?.CompilerOptions;
        }
    }
}
