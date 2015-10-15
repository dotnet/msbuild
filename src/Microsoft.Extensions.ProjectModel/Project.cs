// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.ProjectModel.Graph;
using Microsoft.Extensions.ProjectModel.Utilities;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel
{
    public class Project
    {
        public static readonly string FileName = "project.json";

        // REVIEW: It's kinda hacky making these internal but the reader needs to set them
        internal Dictionary<NuGetFramework, TargetFrameworkInformation> _targetFrameworks = new Dictionary<NuGetFramework, TargetFrameworkInformation>();
        internal Dictionary<NuGetFramework, CompilerOptions> _compilerOptionsByFramework = new Dictionary<NuGetFramework, CompilerOptions>();
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

        public string WebRoot { get; set; }

        public string EntryPoint { get; set; }

        public string ProjectUrl { get; set; }

        public string LicenseUrl { get; set; }

        public string IconUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string[] Tags { get; set; }

        public bool IsLoadable { get; set; }

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

        public static bool HasProjectFile(string path)
        {
            string projectPath = Path.Combine(path, FileName);

            return File.Exists(projectPath);
        }

        public static bool TryGetProject(string path, out Project project, ICollection<DiagnosticMessage> diagnostics = null)
        {
            project = null;

            string projectPath = null;

            if (string.Equals(Path.GetFileName(path), FileName, StringComparison.OrdinalIgnoreCase))
            {
                projectPath = path;
                path = Path.GetDirectoryName(path);
            }
            else if (!HasProjectFile(path))
            {
                return false;
            }
            else
            {
                projectPath = Path.Combine(path, FileName);
            }

            // Assume the directory name is the project name if none was specified
            var projectName = PathUtility.GetDirectoryName(Path.GetFullPath(path));
            projectPath = Path.GetFullPath(projectPath);

            if (!File.Exists(projectPath))
            {
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(projectPath))
                {
                    var reader = new ProjectReader();
                    project = reader.ReadProject(stream, projectName, projectPath, diagnostics);
                }
            }
            catch (Exception ex)
            {
                throw FileFormatException.Create(ex, projectPath);
            }

            return true;
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
            CompilerOptions options;
            if (_compilerOptionsByFramework.TryGetValue(frameworkName, out options))
            {
                return options;
            }

            return null;
        }
    }
}
