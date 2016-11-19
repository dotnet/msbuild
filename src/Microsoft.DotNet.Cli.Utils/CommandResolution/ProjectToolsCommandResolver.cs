// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.DependencyModel;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Utils
{
    public class ProjectToolsCommandResolver : ICommandResolver
    {
        private static readonly NuGetFramework s_toolPackageFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;

        private static readonly CommandResolutionStrategy s_commandResolutionStrategy =
            CommandResolutionStrategy.ProjectToolsPackage;

        private List<string> _allowedCommandExtensions;
        private IPackagedCommandSpecFactory _packagedCommandSpecFactory;

        private IEnvironmentProvider _environment;

        public ProjectToolsCommandResolver(
            IPackagedCommandSpecFactory packagedCommandSpecFactory,
            IEnvironmentProvider environment)
        {
            _packagedCommandSpecFactory = packagedCommandSpecFactory;
            _environment = environment;

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
                Reporter.Verbose.WriteLine($"projecttoolscommandresolver: Invalid CommandResolverArguments");

                return null;
            }

            return ResolveFromProjectTools(commandResolverArguments);
        }

        private CommandSpec ResolveFromProjectTools(CommandResolverArguments commandResolverArguments)
        {
            var projectFactory = new ProjectFactory(_environment);

            var project = projectFactory.GetProject(
                commandResolverArguments.ProjectDirectory,
                commandResolverArguments.Framework,
                commandResolverArguments.Configuration,
                commandResolverArguments.BuildBasePath,
                commandResolverArguments.OutputPath);

            if (project == null)
            {
                Reporter.Verbose.WriteLine($"projecttoolscommandresolver: ProjectFactory did not find Project.");

                return null;
            }
            
            var tools = project.GetTools();

            return ResolveCommandSpecFromAllToolLibraries(
                tools,
                commandResolverArguments.CommandName,
                commandResolverArguments.CommandArguments.OrEmptyIfNull(),
                project);
        }

        private CommandSpec ResolveCommandSpecFromAllToolLibraries(
            IEnumerable<SingleProjectInfo> toolsLibraries,
            string commandName,
            IEnumerable<string> args,
            IProject project)
        {
            Reporter.Verbose.WriteLine($"projecttoolscommandresolver: resolving commandspec from {toolsLibraries.Count()} Tool Libraries.");

            foreach (var toolLibrary in toolsLibraries)
            {
                var commandSpec = ResolveCommandSpecFromToolLibrary(
                    toolLibrary,
                    commandName,
                    args,
                    project);

                if (commandSpec != null)
                {
                    return commandSpec;
                }
            }

            Reporter.Verbose.WriteLine($"projecttoolscommandresolver: failed to resolve commandspec from library.");

            return null;
        }

        private CommandSpec ResolveCommandSpecFromToolLibrary(
            SingleProjectInfo toolLibraryRange,
            string commandName,
            IEnumerable<string> args,
            IProject project)
        {
            Reporter.Verbose.WriteLine($"projecttoolscommandresolver: Attempting to resolve command spec from tool {toolLibraryRange.Name}");

            var nuGetPathContext = NuGetPathContext.Create(project.ProjectRoot);

            var nugetPackagesRoot = nuGetPathContext.UserPackageFolder;

            Reporter.Verbose.WriteLine($"projecttoolscommandresolver: nuget packages root:\n{nugetPackagesRoot}"); 

            var toolLockFile = GetToolLockFile(toolLibraryRange, nugetPackagesRoot);

            Reporter.Verbose.WriteLine($"projecttoolscommandresolver: found tool lockfile at : {toolLockFile.Path}");

            var toolLibrary = toolLockFile.Targets
                .FirstOrDefault(
                    t => t.TargetFramework.GetShortFolderName().Equals(s_toolPackageFramework.GetShortFolderName()))
                ?.Libraries.FirstOrDefault(l => l.Name == toolLibraryRange.Name);

            if (toolLibrary == null)
            {
                Reporter.Verbose.WriteLine($"projecttoolscommandresolver: library not found in lock file.");  

                return null;
            }

            var depsFileRoot = Path.GetDirectoryName(toolLockFile.Path);

            var depsFilePath = GetToolDepsFilePath(toolLibraryRange, toolLockFile, depsFileRoot);

            var normalizedNugetPackagesRoot = PathUtility.EnsureNoTrailingDirectorySeparator(nugetPackagesRoot);

            Reporter.Verbose.WriteLine($"projecttoolscommandresolver: attempting to create commandspec");

            var commandSpec = _packagedCommandSpecFactory.CreateCommandSpecFromLibrary(
                    toolLibrary,
                    commandName,
                    args,
                    _allowedCommandExtensions,
                    normalizedNugetPackagesRoot,
                    s_commandResolutionStrategy,
                    depsFilePath,
                    null);

            if (commandSpec == null)
            {
                Reporter.Verbose.WriteLine($"projecttoolscommandresolver: commandSpec is null.");
            }

            commandSpec?.AddEnvironmentVariablesFromProject(project);

            return commandSpec;
        }

        private LockFile GetToolLockFile(
            SingleProjectInfo toolLibrary,
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
                lockFile = new LockFileFormat().Read(lockFilePath);
            }
            catch (FileFormatException ex)
            {
                throw ex;
            }

            return lockFile;
        }

        private string GetToolLockFilePath(
            SingleProjectInfo toolLibrary,
            string nugetPackagesRoot)
        {
            var toolPathCalculator = new ToolPathCalculator(nugetPackagesRoot);

            return toolPathCalculator.GetBestLockFilePath(
                toolLibrary.Name,
                VersionRange.Parse(toolLibrary.Version),
                s_toolPackageFramework);
        }

        private string GetToolDepsFilePath(
            SingleProjectInfo toolLibrary,
            LockFile toolLockFile,
            string depsPathRoot)
        {
            var depsJsonPath = Path.Combine(
                depsPathRoot,
                toolLibrary.Name + FileNameSuffixes.DepsJson);

            Reporter.Verbose.WriteLine($"projecttoolscommandresolver: expect deps.json at: {depsJsonPath}");

            EnsureToolJsonDepsFileExists(toolLockFile, depsJsonPath, toolLibrary);

            return depsJsonPath;
        }

        private void EnsureToolJsonDepsFileExists(
            LockFile toolLockFile,
            string depsPath,
            SingleProjectInfo toolLibrary)
        {
            if (!File.Exists(depsPath))
            {
                GenerateDepsJsonFile(toolLockFile, depsPath, toolLibrary);
            }
        }

        internal void GenerateDepsJsonFile(
            LockFile toolLockFile,
            string depsPath,
            SingleProjectInfo toolLibrary)
        {
            Reporter.Verbose.WriteLine($"Generating deps.json at: {depsPath}");

            var dependencyContext = new DepsJsonBuilder()
                .Build(toolLibrary, null, toolLockFile, s_toolPackageFramework, null);

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
