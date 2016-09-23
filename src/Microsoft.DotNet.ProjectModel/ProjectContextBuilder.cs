// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectContextBuilder
    {
        // Note: When adding a property, make sure to add it to Clone below. You'll also need to update the CloneTest in
        // Microsoft.DotNet.ProjectModel.Tests.ProjectContextBuilderTests

        private Project Project { get; set; }

        private LockFile LockFile { get; set; }

        private NuGetFramework TargetFramework { get; set; }

        private IEnumerable<string> RuntimeIdentifiers { get; set; } = Enumerable.Empty<string>();

        private string RootDirectory { get; set; }

        private string ProjectDirectory { get; set; }

        private string PackagesDirectory { get; set; }

        private string ReferenceAssembliesPath { get; set; }

        private bool IsDesignTime { get; set; }

        private Func<string, Project> ProjectResolver { get; set; }

        private Func<string, LockFile> LockFileResolver { get; set; }

        private ProjectReaderSettings ProjectReaderSettings { get; set; } = ProjectReaderSettings.ReadFromEnvironment();

        public ProjectContextBuilder()
        {
            ProjectResolver = ResolveProject;
            LockFileResolver = ResolveLockFile;
        }

        public ProjectContextBuilder Clone()
        {
            var builder = new ProjectContextBuilder()
                .WithLockFile(LockFile)
                .WithProject(Project)
                .WithProjectDirectory(ProjectDirectory)
                .WithTargetFramework(TargetFramework)
                .WithRuntimeIdentifiers(RuntimeIdentifiers)
                .WithReferenceAssembliesPath(ReferenceAssembliesPath)
                .WithPackagesDirectory(PackagesDirectory)
                .WithRootDirectory(RootDirectory)
                .WithProjectResolver(ProjectResolver)
                .WithLockFileResolver(LockFileResolver)
                .WithProjectReaderSettings(ProjectReaderSettings);
            if (IsDesignTime)
            {
                builder.AsDesignTime();
            }

            return builder;
        }

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

        public ProjectContextBuilder WithProjectResolver(Func<string, Project> projectResolver)
        {
            ProjectResolver = projectResolver;
            return this;
        }

        public ProjectContextBuilder WithLockFileResolver(Func<string, LockFile> lockFileResolver)
        {
            LockFileResolver = lockFileResolver;
            return this;
        }

        public ProjectContextBuilder WithProjectReaderSettings(ProjectReaderSettings projectReaderSettings)
        {
            ProjectReaderSettings = projectReaderSettings;
            return this;
        }

        public ProjectContextBuilder AsDesignTime()
        {
            IsDesignTime = true;
            return this;
        }

        /// <summary>
        /// Produce all targets found in the lock file associated with this builder.
        /// Returns an empty enumerable if there is no lock file
        /// (making this unsuitable for scenarios where the lock file may not be present,
        /// such as at design-time)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProjectContext> BuildAllTargets()
        {
            ProjectDirectory = Project?.ProjectDirectory ?? ProjectDirectory;
            EnsureProjectLoaded();
            LockFile = LockFile ?? LockFileResolver(ProjectDirectory);

            if (LockFile != null)
            {
                var deduper = new HashSet<string>();
                foreach (var target in LockFile.Targets)
                {
                    var context = Clone()
                        .WithTargetFramework(target.TargetFramework)
                        .WithRuntimeIdentifiers(new[] { target.RuntimeIdentifier }).Build();

                    var id = $"{context.TargetFramework}/{context.RuntimeIdentifier}";
                    if (deduper.Add(id))
                    {
                        yield return context;
                    }
                }
            }
            else
            {
                // Build a context for each framework. It won't be fully valid, since it won't have resolved data or runtime data, but the diagnostics will show that.
                foreach (var framework in Project.GetTargetFrameworks())
                {
                    var builder = new ProjectContextBuilder()
                        .WithProject(Project)
                        .WithTargetFramework(framework.FrameworkName);
                    if (IsDesignTime)
                    {
                        builder.AsDesignTime();
                    }
                    yield return builder.Build();
                }
            }
        }

        public ProjectContext Build()
        {
            var diagnostics = new List<DiagnosticMessage>();

            ProjectDirectory = Project?.ProjectDirectory ?? ProjectDirectory;

            GlobalSettings globalSettings = null;
            if (ProjectDirectory != null)
            {
                RootDirectory = ProjectRootResolver.ResolveRootDirectory(ProjectDirectory);
                GlobalSettings.TryGetGlobalSettings(RootDirectory, out globalSettings);
            }

            RootDirectory = globalSettings?.DirectoryPath ?? RootDirectory;

            FrameworkReferenceResolver frameworkReferenceResolver;
            if (string.IsNullOrEmpty(ReferenceAssembliesPath))
            {
                // Use the default static resolver
                frameworkReferenceResolver = FrameworkReferenceResolver.Default;
            }
            else
            {
                frameworkReferenceResolver = new FrameworkReferenceResolver(ReferenceAssembliesPath);
            }

            LockFileLookup lockFileLookup = null;
            EnsureProjectLoaded();

            ReadLockFile(diagnostics);

            // some callers only give ProjectContextBuilder a LockFile
            ProjectDirectory = ProjectDirectory ?? TryGetProjectDirectoryFromLockFile();

            INuGetPathContext nugetPathContext = null;
            if (ProjectDirectory != null)
            {
                nugetPathContext = NuGetPathContext.Create(ProjectDirectory);
            }

            PackagesDirectory = PackagesDirectory ?? nugetPathContext?.UserPackageFolder;

            var validLockFile = true;
            string lockFileValidationMessage = null;

            if (LockFile != null)
            {
                if (Project != null)
                {
                    validLockFile = LockFile.IsValidForProject(Project, out lockFileValidationMessage);
                }

                lockFileLookup = new LockFileLookup(LockFile);
            }

            var libraries = new Dictionary<LibraryKey, LibraryDescription>();
            var projectResolver = new ProjectDependencyProvider(ProjectResolver);

            ProjectDescription mainProject = null;
            if (Project != null)
            {
                mainProject = projectResolver.GetDescription(TargetFramework, Project, targetLibrary: null);

                // Add the main project
                libraries.Add(new LibraryKey(mainProject.Identity.Name), mainProject);
            }

            ProjectLibraryDependency platformDependency = null;
            if (mainProject != null)
            {
                platformDependency = mainProject.Dependencies
                    .Where(d => d.Type.Equals(LibraryDependencyType.Platform))
                    .Cast<ProjectLibraryDependency>()
                    .FirstOrDefault();
            }
            bool isPortable = platformDependency != null;

            LockFileTarget target = null;
            LibraryDescription platformLibrary = null;

            if (lockFileLookup != null)
            {
                target = SelectTarget(LockFile, isPortable);
                if (target != null)
                {
                    var nugetPackageResolver = new PackageDependencyProvider(nugetPathContext, frameworkReferenceResolver);
                    var msbuildProjectResolver = new MSBuildDependencyProvider(Project, ProjectResolver);
                    ScanLibraries(target, lockFileLookup, libraries, msbuildProjectResolver, nugetPackageResolver, projectResolver);

                    if (platformDependency != null)
                    {
                        libraries.TryGetValue(new LibraryKey(platformDependency.Name), out platformLibrary);
                    }
                }
            }

            string runtime = target?.RuntimeIdentifier;
            if (string.IsNullOrEmpty(runtime) && TargetFramework.IsDesktop())
            {
                // we got a ridless target for desktop so turning portable mode on
                isPortable = true;
                var legacyRuntime = RuntimeEnvironmentRidExtensions.GetLegacyRestoreRuntimeIdentifier();
                if (RuntimeIdentifiers.Contains(legacyRuntime))
                {
                    runtime = legacyRuntime;
                }
                else
                {
                    runtime = RuntimeIdentifiers.FirstOrDefault();
                }
            }

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver(frameworkReferenceResolver);
            bool requiresFrameworkAssemblies;

            // Resolve the dependencies
            ResolveDependencies(libraries, referenceAssemblyDependencyResolver, out requiresFrameworkAssemblies);

            // REVIEW: Should this be in NuGet (possibly stored in the lock file?)
            if (LockFile == null)
            {
                diagnostics.Add(new DiagnosticMessage(
                    ErrorCodes.NU1009,
                    $"The expected lock file doesn't exist. Please run \"dotnet restore\" to generate a new lock file.",
                    Path.Combine(Project.ProjectDirectory, LockFileFormat.LockFileName),
                    DiagnosticMessageSeverity.Error));
            }

            if (!validLockFile)
            {
                diagnostics.Add(new DiagnosticMessage(
                    ErrorCodes.NU1006,
                    $"{lockFileValidationMessage}. Please run \"dotnet restore\" to generate a new lock file.",
                    Path.Combine(Project.ProjectDirectory, LockFileFormat.LockFileName),
                    DiagnosticMessageSeverity.Warning));
            }

            if (requiresFrameworkAssemblies)
            {
                var frameworkInfo = Project.GetTargetFramework(TargetFramework);

                if (frameworkReferenceResolver == null || string.IsNullOrEmpty(frameworkReferenceResolver.ReferenceAssembliesPath))
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

            List<DiagnosticMessage> allDiagnostics = new List<DiagnosticMessage>(diagnostics);
            if (Project != null)
            {
                allDiagnostics.AddRange(Project.Diagnostics);
            }

            // Create a library manager
            var libraryManager = new LibraryManager(libraries.Values.ToList(), allDiagnostics, Project?.ProjectFilePath);

            return new ProjectContext(
                globalSettings,
                mainProject,
                platformLibrary,
                TargetFramework,
                isPortable,
                runtime,
                PackagesDirectory,
                libraryManager,
                LockFile,
                diagnostics);
        }

        private string TryGetProjectDirectoryFromLockFile()
        {
            string result = null;

            if (LockFile != null && !string.IsNullOrEmpty(LockFile.Path))
            {
                result = Path.GetDirectoryName(LockFile.Path);
            }

            return result;
        }

        private void ReadLockFile(ICollection<DiagnosticMessage> diagnostics)
        {
            try
            {
                LockFile = LockFile ?? LockFileResolver(ProjectDirectory);
            }
            catch (FileFormatException e)
            {
                var lockFilePath = "";
                if (LockFile != null)
                {
                    lockFilePath = LockFile.Path;
                }
                else if (Project != null)
                {
                    lockFilePath = Path.Combine(Project.ProjectDirectory, LockFileFormat.LockFileName);
                }

                diagnostics.Add(new DiagnosticMessage(
                    ErrorCodes.DOTNET1014,
                    ComposeMessageFromInnerExceptions(e),
                    lockFilePath,
                    DiagnosticMessageSeverity.Error));
            }
        }

        private static string ComposeMessageFromInnerExceptions(Exception exception)
        {
            var sb = new StringBuilder();
            var messages = new HashSet<string>();

            while (exception != null)
            {
                messages.Add(exception.Message);
                exception = exception.InnerException;
            }

            foreach (var message in messages)
            {
                sb.AppendLine(message);
            }

            return sb.ToString();
        }

        private void ResolveDependencies(Dictionary<LibraryKey, LibraryDescription> libraries,
                                         ReferenceAssemblyDependencyResolver referenceAssemblyDependencyResolver,
                                         out bool requiresFrameworkAssemblies)
        {
            // Remark: the LibraryType in the key of the given dictionary are all "Unspecified" at the beginning.
            requiresFrameworkAssemblies = false;

            foreach (var pair in libraries.ToList())
            {
                var library = pair.Value;

                // The System.* packages provide placeholders on any non netstandard platform
                // To make them work seamlessly on those platforms, we fill the gap with a reference
                // assembly (if available)
                var package = library as PackageDescription;
                if (package != null &&
                    package.Resolved &&
                    package.HasCompileTimePlaceholder &&
                    !TargetFramework.IsPackageBased)
                {
                    // requiresFrameworkAssemblies is true whenever we find a CompileTimePlaceholder in a non-package based framework, even if
                    // the reference is unresolved. This ensures the best error experience when someone is building on a machine without
                    // the target framework installed.
                    requiresFrameworkAssemblies = true;

                    var newKey = new LibraryKey(library.Identity.Name, LibraryType.Reference);
                    var dependency = new ProjectLibraryDependency
                    {
                        LibraryRange = new LibraryRange(library.Identity.Name, LibraryDependencyTarget.Reference)
                    };

                    var replacement = referenceAssemblyDependencyResolver.GetDescription(dependency, TargetFramework);

                    // If the reference is unresolved, just skip it.  Don't replace the package dependency
                    if (replacement == null)
                    {
                        continue;
                    }

                    // Remove the original package reference
                    libraries.Remove(pair.Key);

                    // Insert a reference assembly key if there isn't one
                    if (!libraries.ContainsKey(newKey))
                    {
                        libraries[newKey] = replacement;
                    }
                }
            }

            foreach (var pair in libraries.ToList())
            {
                var library = pair.Value;
                library.Framework = library.Framework ?? TargetFramework;
                foreach (var dependency in library.Dependencies)
                {
                    var keyType = dependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference ?
                                  LibraryType.Reference :
                                  (LibraryType?) null;

                    var key = new LibraryKey(dependency.Name, keyType);

                    LibraryDescription dependencyDescription;
                    if (!libraries.TryGetValue(key, out dependencyDescription))
                    {
                        if (keyType == LibraryType.Reference)
                        {
                            // a dependency is specified to be reference assembly but fail to match
                            // then add a unresolved dependency
                            dependencyDescription = referenceAssemblyDependencyResolver.GetDescription(dependency, TargetFramework) ??
                                                    UnresolvedDependencyProvider.GetDescription(dependency, TargetFramework);
                            libraries[key] = dependencyDescription;
                        }
                        else if (!libraries.TryGetValue(new LibraryKey(dependency.Name, LibraryType.Reference), out dependencyDescription))
                        {
                            // a dependency which type is unspecified fails to match, then try to find a 
                            // reference assembly type dependency
                            dependencyDescription = UnresolvedDependencyProvider.GetDescription(dependency, TargetFramework);
                            libraries[key] = dependencyDescription;
                        }
                    }

                    dependencyDescription.RequestedRanges.Add(dependency);
                    dependencyDescription.Parents.Add(library);
                }
            }

            // Deduplicate libraries with the same name
            // Priority list is backwards so not found -1 would be last when sorting by descending
            var priorities = new[] { LibraryType.Package, LibraryType.Project, LibraryType.Reference };
            var nameGroups = libraries.Keys.ToLookup(libraryKey => libraryKey.Name);
            foreach (var nameGroup in nameGroups)
            {
                var librariesToRemove = nameGroup
                    .OrderByDescending(libraryKey => Array.IndexOf(priorities, libraryKey.LibraryType))
                    .Skip(1);

                foreach (var library in librariesToRemove)
                {
                    libraries.Remove(library);
                }
            }
        }

        private void ScanLibraries(LockFileTarget target,
                                   LockFileLookup lockFileLookup,
                                   Dictionary<LibraryKey, LibraryDescription> libraries,
                                   MSBuildDependencyProvider msbuildResolver,
                                   PackageDependencyProvider packageResolver,
                                   ProjectDependencyProvider projectResolver)
        {
            foreach (var library in target.Libraries)
            {
                LibraryDescription description = null;
                LibraryDependencyTarget type = LibraryDependencyTarget.All;

                if (string.Equals(library.Type, "project"))
                {
                    var projectLibrary = lockFileLookup.GetProject(library.Name);

                    if (projectLibrary != null)
                    {
                        if (MSBuildDependencyProvider.IsMSBuildProjectLibrary(projectLibrary))
                        {
                            description = msbuildResolver.GetDescription(TargetFramework, projectLibrary, library, IsDesignTime);
                            type = LibraryDependencyTarget.Project;
                        }
                        else
                        {
                            var path = Path.GetFullPath(Path.Combine(ProjectDirectory, projectLibrary.Path));
                            description = projectResolver.GetDescription(library.Name, path, library, ProjectResolver);
                            type = LibraryDependencyTarget.Project;
                        }
                    }
                }
                else
                {
                    var packageEntry = lockFileLookup.GetPackage(library.Name, library.Version);

                    if (packageEntry != null)
                    {
                        description = packageResolver.GetDescription(TargetFramework, packageEntry, library);
                    }

                    type = LibraryDependencyTarget.Package;
                }

                description = description ??
                    UnresolvedDependencyProvider.GetDescription(
                        new ProjectLibraryDependency
                        {
                            LibraryRange = new LibraryRange(library.Name, type)
                        },
                        target.TargetFramework);

                libraries.Add(new LibraryKey(library.Name), description);
            }
        }

        private void EnsureProjectLoaded()
        {
            if (Project == null && ProjectDirectory != null)
            {
                Project = ProjectResolver(ProjectDirectory);
                if (Project == null)
                {
                    throw new InvalidOperationException($"Could not resolve project at: {ProjectDirectory}. " +
                                                        $"This could happen when project.lock.json was moved after restore.");
                }
            }
        }

        private LockFileTarget SelectTarget(LockFile lockFile, bool isPortable)
        {
            if (!isPortable)
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

        private Project ResolveProject(string projectDirectory)
        {
            Project project;
            if (ProjectReader.TryGetProject(projectDirectory, out project, settings: ProjectReaderSettings))
            {
                return project;
            }
            else
            {
                return null;
            }
        }

        private static LockFile ResolveLockFile(string projectDir)
        {
            var projectLockJsonPath = Path.Combine(projectDir, LockFileFormat.LockFileName);
            return File.Exists(projectLockJsonPath) ?
                        new LockFileFormat().Read(Path.Combine(projectDir, LockFileFormat.LockFileName)) :
                        null;
        }

        private struct LibraryKey
        {
            public LibraryKey(string name) : this(name, null)
            {
            }

            public LibraryKey(string name, LibraryType? libraryType)
            {
                Name = name;
                LibraryType = libraryType;
            }

            public string Name { get; }
            public LibraryType? LibraryType { get; }

            public override bool Equals(object obj)
            {
                var otherKey = (LibraryKey)obj;

                return string.Equals(otherKey.Name, Name, StringComparison.OrdinalIgnoreCase) &&
                    otherKey.LibraryType.Equals(LibraryType);
            }

            public override int GetHashCode()
            {
                var combiner = new HashCodeCombiner();
                combiner.Add(Name.ToLowerInvariant());
                combiner.Add(LibraryType);

                return combiner.CombinedHash;
            }

            public override string ToString()
            {
                return Name + " " + LibraryType;
            }
        }
    }
}
