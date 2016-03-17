using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;

using LockFile = Microsoft.DotNet.ProjectModel.Graph.LockFile;
using FileFormatException = Microsoft.DotNet.ProjectModel.FileFormatException;

namespace Microsoft.DotNet.Cli.Utils
{
    public class ProjectToolsCommandResolver : ICommandResolver
    {
        private static readonly NuGetFramework s_toolPackageFramework = FrameworkConstants.CommonFrameworks.NetStandardApp15;
        
        private static readonly CommandResolutionStrategy s_commandResolutionStrategy = 
            CommandResolutionStrategy.ProjectToolsPackage;

        private static readonly string s_currentRuntimeIdentifier = PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier();


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
            var nugetPackagesRoot = projectContext.PackagesDirectory;

            var lockFile = GetToolLockFile(toolLibrary, nugetPackagesRoot);
            var lockFilePackageLibrary = lockFile.PackageLibraries.FirstOrDefault(l => l.Name == toolLibrary.Name);

            var depsFileRoot = Path.GetDirectoryName(lockFile.LockFilePath);
            var depsFilePath = GetToolDepsFilePath(toolLibrary, lockFile, depsFileRoot);

            var toolProjectContext = new ProjectContextBuilder()
                    .WithLockFile(lockFile)
                    .WithTargetFramework(s_toolPackageFramework.ToString())
                    .Build();

            var exporter = toolProjectContext.CreateExporter(Constants.DefaultConfiguration);

            return _packagedCommandSpecFactory.CreateCommandSpecFromLibrary(
                    lockFilePackageLibrary,
                    commandName,
                    args,
                    _allowedCommandExtensions,
                    projectContext.PackagesDirectory,
                    s_commandResolutionStrategy,
                    depsFilePath);
        }

        private LockFile GetToolLockFile(
            LibraryRange toolLibrary,
            string nugetPackagesRoot)
        {
            var lockFilePath = GetToolLockFilePath(toolLibrary, nugetPackagesRoot);

            if (!File.Exists(lockFilePath))
            {
                return null;
            }

            LockFile lockFile = null;

            try
            {
                lockFile = LockFileReader.Read(lockFilePath);
            }
            catch (FileFormatException ex)
            {
                throw ex;
            }

            return lockFile;
        }

        private string GetToolLockFilePath(
            LibraryRange toolLibrary,
            string nugetPackagesRoot)
        {
            var toolPathCalculator = new ToolPathCalculator(nugetPackagesRoot);

            return toolPathCalculator.GetBestLockFilePath(
                toolLibrary.Name, 
                toolLibrary.VersionRange, 
                s_toolPackageFramework);
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

        private string GetToolDepsFilePath(
            LibraryRange toolLibrary, 
            LockFile toolLockFile, 
            string depsPathRoot)
        {
            var depsJsonPath = Path.Combine(
                depsPathRoot,
                toolLibrary.Name + FileNameSuffixes.DepsJson);

            EnsureToolJsonDepsFileExists(toolLibrary, toolLockFile, depsJsonPath);

            return depsJsonPath;
        }

        private void EnsureToolJsonDepsFileExists(
            LibraryRange toolLibrary, 
            LockFile toolLockFile, 
            string depsPath)
        {
            if (!File.Exists(depsPath))
            {
                var projectContext = new ProjectContextBuilder()
                    .WithLockFile(toolLockFile)
                    .WithTargetFramework(s_toolPackageFramework.ToString())
                    .Build();

                var exporter = projectContext.CreateExporter(Constants.DefaultConfiguration);

                var dependencyContext = new DependencyContextBuilder()
                    .Build(null, 
                        null, 
                        exporter.GetAllExports(), 
                        true, 
                        s_toolPackageFramework, 
                        string.Empty);

                using (var fileStream = File.Create(depsPath))
                {
                    var dependencyContextWriter = new DependencyContextWriter();

                    dependencyContextWriter.Write(dependencyContext, fileStream);
                }
            }
        }
    }
}
