// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectModel.Files;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel
{
    public class Project
    {
        public static readonly string FileName = "project.json";

        // REVIEW: It's kinda hacky making these internal but the reader needs to set them
        internal Dictionary<NuGetFramework, TargetFrameworkInformation> _targetFrameworks = new Dictionary<NuGetFramework, TargetFrameworkInformation>();
        internal Dictionary<string, CommonCompilerOptions> _compilerOptionsByConfiguration = new Dictionary<string, CommonCompilerOptions>(StringComparer.OrdinalIgnoreCase);

        internal CommonCompilerOptions _defaultCompilerOptions;
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

        public AnalyzerOptions AnalyzerOptions { get; set; }

        public string Name { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Copyright { get; set; }

        public string Language { get; set; }

        public string[] Authors { get; set; }

        public bool EmbedInteropTypes { get; set; }

        public NuGetVersion Version { get; set; }

        public Version AssemblyFileVersion { get; set; }

        public IList<ProjectLibraryDependency> Dependencies { get; set; }

        public List<ProjectLibraryDependency> Tools { get; set; }

        public string EntryPoint { get; set; }

        public string TestRunner { get; set; }

        public ProjectFilesCollection Files { get; set; }

        public PackOptions PackOptions { get; set; }

        public bool Serviceable { get; set; }

        public RuntimeOptions RuntimeOptions { get; set; }

        public IDictionary<string, string> Commands { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, IEnumerable<string>> Scripts { get; } = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

        public string RawRuntimeOptions { get; set; }

        public IncludeContext PublishOptions { get; set; }

        public List<DiagnosticMessage> Diagnostics { get; } = new List<DiagnosticMessage>();

        public bool IsTestProject => !string.IsNullOrEmpty(TestRunner);
        
        public IEnumerable<TargetFrameworkInformation> GetTargetFrameworks()
        {
            return _targetFrameworks.Values;
        }

        public IEnumerable<string> GetConfigurations()
        {
            return _compilerOptionsByConfiguration.Keys;
        }

        public CommonCompilerOptions GetCompilerOptions(NuGetFramework targetFramework, string configurationName)
        {
            // Get all project options and combine them
            var rootOptions = GetCompilerOptions();
            var configurationOptions = configurationName != null ? GetRawCompilerOptions(configurationName) : null;
            var targetFrameworkOptions = targetFramework != null ? GetRawCompilerOptions(targetFramework) : null;

            // Combine all of the options
            var compilerOptions = CommonCompilerOptions.Combine(rootOptions, configurationOptions, targetFrameworkOptions);

            if (compilerOptions.OutputName == null)
            {
                compilerOptions.OutputName = Name;
            }

            return compilerOptions;
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

        public bool HasRuntimeOutput(string configuration)
        {
            var compilerOptions = GetCompilerOptions(targetFramework: null, configurationName: configuration);

            // TODO: Make this opt in via another mechanism
            return compilerOptions.EmitEntryPoint.GetValueOrDefault() || IsTestProject;
        }

        private CommonCompilerOptions GetCompilerOptions()
        {
            return _defaultCompilerOptions;
        }

        internal CommonCompilerOptions GetRawCompilerOptions(string configurationName)
        {
            CommonCompilerOptions options;
            if (_compilerOptionsByConfiguration.TryGetValue(configurationName, out options))
            {
                return options;
            }

            return null;
        }

        internal CommonCompilerOptions GetRawCompilerOptions(NuGetFramework frameworkName)
        {
            return GetTargetFramework(frameworkName)?.CompilerOptions;
        }
    }
}
