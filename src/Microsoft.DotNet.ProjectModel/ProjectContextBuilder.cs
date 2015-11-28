// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.Extensions.Internal;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectContextBuilder
    {
        private Project Project { get; set; }

        private LockFile LockFile { get; set; }

        private GlobalSettings GlobalSettings { get; set; }

        private NuGetFramework TargetFramework { get; set; }

        private IEnumerable<string> RuntimeIdentifiers { get; set; } = Enumerable.Empty<string>();

        private string RootDirectory { get; set; }

        private string ProjectDirectory { get; set; }

        private string PackagesDirectory { get; set; }
        
        private string ReferenceAssembliesPath { get; set; }

        public ProjectContextBuilder WithLockFile(LockFile lockFile)
        {
            LockFile = lockFile;
            return this;
        }
        
        public ProjectContextBuilder WithProject(Project project)
        {
            Project = project;
            return this;
        }
        
        public ProjectContextBuilder WithProjectDirectory(string projectDirectory)
        {
            ProjectDirectory = projectDirectory;
            return this;
        }
        
        public ProjectContextBuilder WithTargetFramework(NuGetFramework targetFramework)
        {
            TargetFramework = targetFramework;
            return this;
        }
        
        public ProjectContextBuilder WithTargetFramework(string targetFramework)
        {
            TargetFramework = NuGetFramework.Parse(targetFramework);
            return this;
        }

        public ProjectContextBuilder WithRuntimeIdentifiers(IEnumerable<string> runtimeIdentifiers)
        {
            RuntimeIdentifiers = runtimeIdentifiers;
            return this;
        }

        public ProjectContextBuilder WithReferenceAssembliesPath(string referenceAssembliesPath)
        {
            ReferenceAssembliesPath = referenceAssembliesPath;
            return this;
        }
        
        public ProjectContextBuilder WithPackagesDirectory(string packagesDirectory)
        {
            PackagesDirectory = packagesDirectory;
            return this;
        }
        
        public ProjectContextBuilder WithRootDirectory(string rootDirectory)
        {
            RootDirectory = rootDirectory;
            return this;
        }

        public ProjectContext Build()
        {
            ProjectDirectory = Project?.ProjectDirectory ?? ProjectDirectory;

            if (GlobalSettings == null)
            {
                RootDirectory = ProjectRootResolver.ResolveRootDirectory(ProjectDirectory);

                GlobalSettings globalSettings;
                if (GlobalSettings.TryGetGlobalSettings(RootDirectory, out globalSettings))
                {
                    GlobalSettings = globalSettings;
                }
            }

            RootDirectory = GlobalSettings?.DirectoryPath ?? RootDirectory;
            PackagesDirectory = PackagesDirectory ?? PackageDependencyProvider.ResolvePackagesPath(RootDirectory, GlobalSettings);
            ReferenceAssembliesPath = ReferenceAssembliesPath ?? GetDefaultReferenceAssembliesPath();

            LockFileLookup lockFileLookup = null;

            EnsureProjectLoaded();

            var projectLockJsonPath = Path.Combine(ProjectDirectory, LockFile.FileName);

            if (LockFile == null && File.Exists(projectLockJsonPath))
            {
                LockFile = LockFileReader.Read(projectLockJsonPath);
            }

            var validLockFile = true;
            string lockFileValidationMessage = null;

            if (LockFile != null)
            {
                validLockFile = LockFile.IsValidForProject(Project, out lockFileValidationMessage);

                lockFileLookup = new LockFileLookup(LockFile);
            }

            var libraries = new Dictionary<LibraryKey, LibraryDescription>();
            var projectResolver = new ProjectDependencyProvider();

            var mainProject = projectResolver.GetDescription(TargetFramework, Project);

            // Add the main project
            libraries.Add(new LibraryKey(mainProject.Identity.Name), mainProject);

            LockFileTarget target = null;
            if (lockFileLookup != null)
            {
                target = SelectTarget(LockFile);
                if (target != null)
                {
                    var packageResolver = new PackageDependencyProvider(PackagesDirectory);
                    ScanLibraries(target, lockFileLookup, libraries, packageResolver, projectResolver);
                }
            }

            var frameworkReferenceResolver = new FrameworkReferenceResolver(ReferenceAssembliesPath);
            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver(frameworkReferenceResolver);
            bool requiresFrameworkAssemblies;

            // Resolve the dependencies
            ResolveDependencies(libraries, referenceAssemblyDependencyResolver, out requiresFrameworkAssemblies);

            var diagnostics = new List<DiagnosticMessage>();

            // REVIEW: Should this be in NuGet (possibly stored in the lock file?)
            if (LockFile == null)
            {
                diagnostics.Add(new DiagnosticMessage(
                    ErrorCodes.NU1009,
                    $"The expected lock file doesn't exist. Please run \"dotnet restore\" to generate a new lock file.",
                    Path.Combine(Project.ProjectDirectory, LockFile.FileName),
                    DiagnosticMessageSeverity.Error));
            }

            if (!validLockFile)
            {
                diagnostics.Add(new DiagnosticMessage(
                    ErrorCodes.NU1006,
                    $"{lockFileValidationMessage}. Please run \"dotnet restore\" to generate a new lock file.",
                    Path.Combine(Project.ProjectDirectory, LockFile.FileName),
                    DiagnosticMessageSeverity.Warning));
            }

            if (requiresFrameworkAssemblies)
            {
                var frameworkInfo = Project.GetTargetFramework(TargetFramework);

                if (string.IsNullOrEmpty(ReferenceAssembliesPath))
                {
                    // If there was an attempt to use reference assemblies but they were not installed
                    // report an error
                    diagnostics.Add(new DiagnosticMessage(
                        ErrorCodes.DOTNET1012,
                        $"The reference assemblies directory was not specified. You can set the location using the DOTNET_REFERENCE_ASSEMBLIES_PATH environment variable.",
                        filePath: Project.ProjectFilePath,
                        severity: DiagnosticMessageSeverity.Error,
                        startLine: frameworkInfo.Line,
                        startColumn: frameworkInfo.Column
                    ));
                }
                else if (!frameworkReferenceResolver.IsInstalled(TargetFramework))
                {
                    // If there was an attempt to use reference assemblies but they were not installed
                    // report an error
                    diagnostics.Add(new DiagnosticMessage(
                        ErrorCodes.DOTNET1011,
                        $"Framework not installed: {TargetFramework.DotNetFrameworkName} in {ReferenceAssembliesPath}",
                        filePath: Project.ProjectFilePath,
                        severity: DiagnosticMessageSeverity.Error,
                        startLine: frameworkInfo.Line,
                        startColumn: frameworkInfo.Column
                    ));
                }
            }

            // Create a library manager
            var libraryManager = new LibraryManager(libraries.Values.ToList(), diagnostics, Project.ProjectFilePath);

            return new ProjectContext(
                GlobalSettings,
                mainProject,
                TargetFramework,
                target?.RuntimeIdentifier,
                PackagesDirectory,
                libraryManager);
        }

        private void ResolveDependencies(Dictionary<LibraryKey, LibraryDescription> libraries, 
                                         ReferenceAssemblyDependencyResolver referenceAssemblyDependencyResolver,
                                         out bool requiresFrameworkAssemblies)
        {
            requiresFrameworkAssemblies = false;
            
            foreach (var library in libraries.Values.ToList())
            {
                if (Equals(library.Identity.Type, LibraryType.Package) &&
                    !Directory.Exists(library.Path))
                {
                    // If the package path doesn't exist then mark this dependency as unresolved
                    library.Resolved = false;
                }

                library.Framework = library.Framework ?? TargetFramework;
                foreach (var dependency in library.Dependencies)
                {
                    var keyType = dependency.Target == LibraryType.ReferenceAssembly ? LibraryType.ReferenceAssembly : LibraryType.Unspecified;
                    var key = new LibraryKey(dependency.Name, keyType);

                    LibraryDescription dep;
                    if (!libraries.TryGetValue(key, out dep))
                    {
                        if (Equals(LibraryType.ReferenceAssembly, dependency.Target))
                        {
                            requiresFrameworkAssemblies = true;

                            dep = referenceAssemblyDependencyResolver.GetDescription(dependency, TargetFramework) ??
                                  UnresolvedDependencyProvider.GetDescription(dependency, TargetFramework);

                            dep.Framework = TargetFramework;
                            libraries[key] = dep;
                        }
                        else
                        {
                            dep = UnresolvedDependencyProvider.GetDescription(dependency, TargetFramework);
                            libraries[key] = dep;
                        }
                    }

                    dep.RequestedRanges.Add(dependency);
                    dep.Parents.Add(library);
                }
            }
        }

        private void ScanLibraries(LockFileTarget target, LockFileLookup lockFileLookup, Dictionary<LibraryKey, LibraryDescription> libraries, PackageDependencyProvider packageResolver, ProjectDependencyProvider projectResolver)
        {
            foreach (var library in target.Libraries)
            {
                LibraryDescription description = null;
                var type = LibraryType.Unspecified;

                if (string.Equals(library.Type, "project"))
                {
                    var projectLibrary = lockFileLookup.GetProject(library.Name);

                    if (projectLibrary != null)
                    {
                        var path = Path.GetFullPath(Path.Combine(ProjectDirectory, projectLibrary.Path));

                        description = projectResolver.GetDescription(library.Name, path, library);
                    }

                    type = LibraryType.Project;
                }
                else
                {
                    var packageEntry = lockFileLookup.GetPackage(library.Name, library.Version);

                    if (packageEntry != null)
                    {
                        description = packageResolver.GetDescription(packageEntry, library);
                    }

                    type = LibraryType.Package;
                }

                description = description ?? UnresolvedDependencyProvider.GetDescription(new LibraryRange(library.Name, type), target.TargetFramework);

                libraries.Add(new LibraryKey(library.Name), description);
            }
        }

        public static string GetDefaultReferenceAssembliesPath()
        {
            // Allow setting the reference assemblies path via an environment variable
            var referenceAssembliesPath = Environment.GetEnvironmentVariable("DOTNET_REFERENCE_ASSEMBLIES_PATH");

            if (!string.IsNullOrEmpty(referenceAssembliesPath))
            {
                return referenceAssembliesPath;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // There is no reference assemblies path outside of windows
                // The enviorment variable can be used to specify one
                return null;
            }

            // References assemblies are in %ProgramFiles(x86)% on
            // 64 bit machines
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)");

            if (string.IsNullOrEmpty(programFiles))
            {
                // On 32 bit machines they are in %ProgramFiles%
                programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            }

            if (string.IsNullOrEmpty(programFiles))
            {
                // Reference assemblies aren't installed
                return null;
            }

            return Path.Combine(
                    programFiles,
                    "Reference Assemblies", "Microsoft", "Framework");
        }
        
        private void EnsureProjectLoaded()
        {
            if (Project == null)
            {
                Project project;
                if (ProjectReader.TryGetProject(ProjectDirectory, out project))
                {
                    Project = project;
                }
                else
                {
                    throw new InvalidOperationException($"Unable to resolve project from {ProjectDirectory}");
                }
            }
        }

        private LockFileTarget SelectTarget(LockFile lockFile)
        {
            foreach (var runtimeIdentifier in RuntimeIdentifiers)
            {
                foreach (var scanTarget in lockFile.Targets)
                {
                    if (Equals(scanTarget.TargetFramework, TargetFramework) && string.Equals(scanTarget.RuntimeIdentifier, runtimeIdentifier, StringComparison.Ordinal))
                    {
                        return scanTarget;
                    }
                }
            }

            foreach (var scanTarget in lockFile.Targets)
            {
                if (Equals(scanTarget.TargetFramework, TargetFramework) && string.IsNullOrEmpty(scanTarget.RuntimeIdentifier))
                {
                    return scanTarget;
                }
            }

            return null;
        }

        private struct LibraryKey
        {
            public LibraryKey(string name) : this(name, LibraryType.Unspecified)
            {
            }

            public LibraryKey(string name, LibraryType libraryType)
            {
                Name = name;
                LibraryType = libraryType;
            }

            public string Name { get; }
            public LibraryType LibraryType { get; }

            public override bool Equals(object obj)
            {
                var otherKey = (LibraryKey)obj;

                return string.Equals(otherKey.Name, Name, StringComparison.Ordinal) &&
                    otherKey.LibraryType.Equals(LibraryType);
            }

            public override int GetHashCode()
            {
                var combiner = new HashCodeCombiner();
                combiner.Add(Name);
                combiner.Add(LibraryType);

                return combiner.CombinedHash;
            }
        }
    }
}
