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
        private static readonly NuGetFramework s_toolPackageFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;
        
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
            var projectContext = GetProjectContextFromDirectoryForFirstTarget(projectDirectory);

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
            LibraryRange toolLibraryRange,
            string commandName,
            IEnumerable<string> args,
            ProjectContext projectContext)
        {
            var nugetPackagesRoot = projectContext.PackagesDirectory;
            
            var lockFile = GetToolLockFile(toolLibraryRange, nugetPackagesRoot);

            var toolLibrary = lockFile.Targets
                .FirstOrDefault(t => t.TargetFramework.GetShortFolderName().Equals(s_toolPackageFramework.GetShortFolderName()))
                ?.Libraries.FirstOrDefault(l => l.Name == toolLibraryRange.Name);

            if (toolLibrary == null)
            {
                return null;
            }
            
            var depsFileRoot = Path.GetDirectoryName(lockFile.LockFilePath);
            var depsFilePath = GetToolDepsFilePath(toolLibraryRange, lockFile, depsFileRoot);
            
            return _packagedCommandSpecFactory.CreateCommandSpecFromLibrary(
                    toolLibrary,
                    commandName,
                    args,
                    _allowedCommandExtensions,
                    projectContext.PackagesDirectory,
                    s_commandResolutionStrategy,
                    depsFilePath,
                    null);
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
                lockFile = LockFileReader.Read(lockFilePath, designTime: false);
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

        private ProjectContext GetProjectContextFromDirectoryForFirstTarget(string projectRootPath)
        {
            if (projectRootPath == null)
            {
                return null;
            }

            if (!File.Exists(Path.Combine(projectRootPath, Project.FileName)))
            {
                return null;
            }

            var projectContext = ProjectContext.CreateContextForEachTarget(projectRootPath).FirstOrDefault();

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

            EnsureToolJsonDepsFileExists(toolLockFile, depsJsonPath);

            return depsJsonPath;
        }

        private void EnsureToolJsonDepsFileExists(
            LockFile toolLockFile, 
            string depsPath)
        {
            if (!File.Exists(depsPath))
            {
                GenerateDepsJsonFile(toolLockFile, depsPath);
            }
        }

        // Need to unit test this, so public
        public void GenerateDepsJsonFile(
            LockFile toolLockFile, 
            string depsPath)
        {
            Reporter.Verbose.WriteLine($"Generating deps.json at: {depsPath}");

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

            var tempDepsFile = Path.GetTempFileName();
            using (var fileStream = File.Open(tempDepsFile, FileMode.Open, FileAccess.Write))
            {
                var dependencyContextWriter = new DependencyContextWriter();

                dependencyContextWriter.Write(dependencyContext, fileStream);
            }

            try
            {
                File.Copy(tempDepsFile, depsPath);
            }
            catch (Exception e)
            {
                Reporter.Verbose.WriteLine($"unable to generate deps.json, it may have been already generated: {e.Message}");
            }
            finally
            {
                try
                {
                    File.Delete(tempDepsFile);
                }
                catch (Exception e2)
                { 
                    Reporter.Verbose.WriteLine($"unable to delete temporary deps.json file: {e2.Message}");
                }
            }
        }
    }
}
