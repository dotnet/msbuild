using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class ProjectDependenciesCommandResolver : ICommandResolver
    {
        private static readonly CommandResolutionStrategy s_commandResolutionStrategy = 
            CommandResolutionStrategy.ProjectDependenciesPackage;

        private readonly IEnvironmentProvider _environment;
        private readonly IPackagedCommandSpecFactory _packagedCommandSpecFactory;

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
                    commandResolverArguments.OutputPath,
                    commandResolverArguments.BuildBasePath);
        }

        private CommandSpec ResolveFromProjectDependencies(
            string projectDirectory,
            NuGetFramework framework,
            string configuration,
            string commandName,
            IEnumerable<string> commandArguments,
            string outputPath,
            string buildBasePath)
        {
            var allowedExtensions = GetAllowedCommandExtensionsFromEnvironment(_environment);

            var projectContext = GetProjectContextFromDirectory(
                projectDirectory,
                framework);

            if (projectContext == null)
            {
                return null;
            }

            var depsFilePath =
                projectContext.GetOutputPaths(configuration, buildBasePath, outputPath).RuntimeFiles.DepsJson;

            var toolLibrary = GetToolLibraryForContext(projectContext, commandName);

            return ResolveFromDependencyLibrary(
                toolLibrary,
                depsFilePath,
                commandName,
                allowedExtensions,
                commandArguments,
                projectContext);
        }

        private CommandSpec ResolveFromDependencyLibrary(
            LockFileTargetLibrary toolLibrary,
            string depsFilePath,
            string commandName,
            IEnumerable<string> allowedExtensions,
            IEnumerable<string> commandArguments,
            ProjectContext projectContext)
        {
            return _packagedCommandSpecFactory.CreateCommandSpecFromRuntimeAssembly(
                        toolLibrary,
                        commandName,
                        commandArguments,
                        allowedExtensions,
                        projectContext.PackagesDirectory,
                        s_commandResolutionStrategy,
                        depsFilePath);
        }

        private LockFileTargetLibrary GetToolLibraryForContext(
            ProjectContext projectContext, string commandName)
        {
            var toolLibrary = projectContext.LockFile.Targets
                .FirstOrDefault(t => t.TargetFramework.GetShortFolderName()
                                      .Equals(projectContext.TargetFramework.GetShortFolderName()))
                ?.Libraries.FirstOrDefault(l => l.Name == commandName);

            return toolLibrary;
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
