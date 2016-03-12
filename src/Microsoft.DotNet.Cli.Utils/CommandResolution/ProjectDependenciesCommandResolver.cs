using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Utils
{
    public class ProjectDependenciesCommandResolver : ICommandResolver
    {
        private static readonly CommandResolutionStrategy s_commandResolutionStrategy = 
            CommandResolutionStrategy.ProjectDependenciesPackage;

        private IEnvironmentProvider _environment;
        private IPackagedCommandSpecFactory _packagedCommandSpecFactory;

        public ProjectDependenciesCommandResolver(
            IEnvironmentProvider environment,
            IPackagedCommandSpecFactory packagedCommandSpecFactory)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (packagedCommandSpecFactory == null)
            {
                throw new ArgumentNullException(nameof(packagedCommandSpecFactory));
            }

            _environment = environment;
            _packagedCommandSpecFactory = packagedCommandSpecFactory;
        }

        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.Framework == null 
                || commandResolverArguments.ProjectDirectory == null
                || commandResolverArguments.Configuration == null
                || commandResolverArguments.CommandName == null)
            {
                return null;
            }

            return ResolveFromProjectDependencies(
                    commandResolverArguments.ProjectDirectory,
                    commandResolverArguments.Framework,
                    commandResolverArguments.Configuration,
                    commandResolverArguments.CommandName,
                    commandResolverArguments.CommandArguments.OrEmptyIfNull(),
                    commandResolverArguments.OutputPath);
        }

        private CommandSpec ResolveFromProjectDependencies(
            string projectDirectory,
            NuGetFramework framework,
            string configuration,
            string commandName,
            IEnumerable<string> commandArguments,
            string outputPath)
        {
            var allowedExtensions = GetAllowedCommandExtensionsFromEnvironment(_environment);

            var projectContext = GetProjectContextFromDirectory(
                projectDirectory, 
                framework);

            if (projectContext == null)
            { 
                return null;
            }

            var depsFilePath = projectContext.GetOutputPaths(configuration, outputPath: outputPath).RuntimeFiles.Deps;

            var dependencyLibraries = GetAllDependencyLibraries(projectContext);
             
            return ResolveFromDependencyLibraries(
                dependencyLibraries,
                depsFilePath,
                commandName,
                allowedExtensions,
                commandArguments,
                projectContext);
        }

        private CommandSpec ResolveFromDependencyLibraries(
            IEnumerable<LockFilePackageLibrary> dependencyLibraries,
            string depsFilePath,
            string commandName,
            IEnumerable<string> allowedExtensions,
            IEnumerable<string> commandArguments,
            ProjectContext projectContext)
        {
            foreach (var dependencyLibrary in dependencyLibraries)
            {
                var commandSpec = ResolveFromDependencyLibrary(
                    dependencyLibrary,
                    depsFilePath,
                    commandName,
                    allowedExtensions,
                    commandArguments,
                    projectContext);

                if (commandSpec != null)
                {
                    return commandSpec;
                }
            }

            return null;
        }

        private CommandSpec ResolveFromDependencyLibrary(
            LockFilePackageLibrary dependencyLibrary,
            string depsFilePath,
            string commandName,
            IEnumerable<string> allowedExtensions,
            IEnumerable<string> commandArguments,
            ProjectContext projectContext)
        {
            return _packagedCommandSpecFactory.CreateCommandSpecFromLibrary(
                        dependencyLibrary,
                        commandName,
                        commandArguments,
                        allowedExtensions,
                        projectContext.PackagesDirectory,
                        s_commandResolutionStrategy,
                        depsFilePath);
        }

        private IEnumerable<LockFilePackageLibrary> GetAllDependencyLibraries(
            ProjectContext projectContext)
        {
            return projectContext.LibraryManager.GetLibraries()
                .Where(l => l.GetType() == typeof(PackageDescription))
                .Select(l => l as PackageDescription)
                .Select(p => p.Library);
        }

        private ProjectContext GetProjectContextFromDirectory(string directory, NuGetFramework framework)
        {
            if (directory == null || framework == null)
            {
                return null;
            }

            var projectRootPath = directory;

            if (!File.Exists(Path.Combine(projectRootPath, Project.FileName)))
            {
                return null;
            }

            var projectContext = ProjectContext.Create(
                projectRootPath, 
                framework, 
                PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers());

            if (projectContext.RuntimeIdentifier == null)
            {
                return null;
            }

            return projectContext;
        }

        private IEnumerable<string> GetAllowedCommandExtensionsFromEnvironment(IEnvironmentProvider environment)
        {
            var allowedCommandExtensions = new List<string>();
            allowedCommandExtensions.AddRange(environment.ExecutableExtensions);
            allowedCommandExtensions.Add(FileNameSuffixes.DotNet.DynamicLib);

            return allowedCommandExtensions;
        }
    }
}
