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
    public class ProjectToolsCommandResolver : ICommandResolver
    {
        private static readonly NuGetFramework s_toolPackageFramework = FrameworkConstants.CommonFrameworks.NetStandardApp15;
        private static readonly CommandResolutionStrategy s_commandResolutionStrategy = 
            CommandResolutionStrategy.ProjectToolsPackage;

        private List<string> _allowedCommandExtensions;
        private IPackagedCommandSpecFactory _packagedCommandSpecFactory;

        public ProjectToolsCommandResolver(IPackagedCommandSpecFactory packagedCommandSpecFactory)
        {
            _packagedCommandSpecFactory = packagedCommandSpecFactory;

            _allowedCommandExtensions = new List<string>() 
            {
                FileNameSuffixes.DotNet.DynamicLib
            };
        }

        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.CommandName == null
                || commandResolverArguments.ProjectDirectory == null)
            {
                return null;
            }
            
            return ResolveFromProjectTools(
                commandResolverArguments.CommandName, 
                commandResolverArguments.CommandArguments.OrEmptyIfNull(),
                commandResolverArguments.ProjectDirectory);
        }

        private CommandSpec ResolveFromProjectTools(
            string commandName, 
            IEnumerable<string> args,
            string projectDirectory)
        {
            var projectContext = GetProjectContextFromDirectory(projectDirectory, s_toolPackageFramework);

            if (projectContext == null)
            {
                return null;
            }

            var toolsLibraries = projectContext.ProjectFile.Tools.OrEmptyIfNull();

            return ResolveCommandSpecFromAllToolLibraries(
                toolsLibraries,
                commandName, 
                args,
                projectContext);
        }

        private CommandSpec ResolveCommandSpecFromAllToolLibraries(
            IEnumerable<LibraryRange> toolsLibraries,
            string commandName,
            IEnumerable<string> args,
            ProjectContext projectContext)
        {
            foreach (var toolLibrary in toolsLibraries)
            {
                var commandSpec = ResolveCommandSpecFromToolLibrary(toolLibrary, commandName, args, projectContext);

                if (commandSpec != null)
                {
                    return commandSpec;
                }
            }

            return null;
        }

        private CommandSpec ResolveCommandSpecFromToolLibrary(
            LibraryRange toolLibrary,
            string commandName,
            IEnumerable<string> args,
            ProjectContext projectContext)
        {
            //todo: change this for new resolution strategy
            var lockFilePath = Path.Combine(
                projectContext.ProjectDirectory, 
                "artifacts", "Tools", toolLibrary.Name,
                "project.lock.json"); 

            if (!File.Exists(lockFilePath))
            {
                return null;
            }

            var lockFile = LockFileReader.Read(lockFilePath);

            var lockFilePackageLibrary = lockFile.PackageLibraries.FirstOrDefault(l => l.Name == toolLibrary.Name);
            
            var nugetPackagesRoot = projectContext.PackagesDirectory;

            return _packagedCommandSpecFactory.CreateCommandSpecFromLibrary(
                    lockFilePackageLibrary,
                    commandName,
                    args,
                    _allowedCommandExtensions,
                    projectContext.PackagesDirectory,
                    s_commandResolutionStrategy,
                    null);
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
    }
}
