using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;

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

            if (! File.Exists(depsFilePath))
            {
                Reporter.Verbose.WriteLine($"projectdependenciescommandresolver: {depsFilePath} does not exist");
                return null;
            }

            var runtimeConfigPath =
                projectContext.GetOutputPaths(configuration, buildBasePath, outputPath).RuntimeFiles.RuntimeConfigJson;

            if (! File.Exists(runtimeConfigPath))
            {
                Reporter.Verbose.WriteLine($"projectdependenciescommandresolver: {runtimeConfigPath} does not exist");
                return null;
            }

            var toolLibrary = GetToolLibraryForContext(projectContext, commandName);
            var normalizedNugetPackagesRoot = PathUtility.EnsureNoTrailingDirectorySeparator(projectContext.PackagesDirectory);

            return _packagedCommandSpecFactory.CreateCommandSpecFromLibrary(
                        toolLibrary,
                        commandName,
                        commandArguments,
                        allowedExtensions,
                        normalizedNugetPackagesRoot,
                        s_commandResolutionStrategy,
                        depsFilePath,
                        runtimeConfigPath);
        }

        private LockFileTargetLibrary GetToolLibraryForContext(
            ProjectContext projectContext, string commandName)
        {
            var toolLibraries = projectContext.LockFile.Targets
                .FirstOrDefault(t => t.TargetFramework.GetShortFolderName()
                                      .Equals(projectContext.TargetFramework.GetShortFolderName()))
                ?.Libraries.Where(l => l.Name == commandName ||
                    l.RuntimeAssemblies.Any(r => Path.GetFileNameWithoutExtension(r.Path) == commandName)).ToList();

            if (toolLibraries?.Count() > 1)
            {
                throw new InvalidOperationException($"Ambiguous command name: {commandName}");
            }

            return toolLibraries?.FirstOrDefault();
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

            return ProjectContext.Create(
                projectRootPath,
                framework,
                RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers());

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
