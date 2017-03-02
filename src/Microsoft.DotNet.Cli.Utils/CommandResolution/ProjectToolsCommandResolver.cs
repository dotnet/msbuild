// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.DependencyModel;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using ConcurrencyUtilities = NuGet.Common.ConcurrencyUtilities;

namespace Microsoft.DotNet.Cli.Utils
{
    public class ProjectToolsCommandResolver : ICommandResolver
    {
        private const string ProjectToolsCommandResolverName = "projecttoolscommandresolver";

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
                Reporter.Verbose.WriteLine(string.Format(
                    LocalizableStrings.InvalidCommandResolverArguments,
                    ProjectToolsCommandResolverName));

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
                Reporter.Verbose.WriteLine(string.Format(
                    LocalizableStrings.DidNotFindProject, ProjectToolsCommandResolverName));

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
            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.ResolvingCommandSpec,
                ProjectToolsCommandResolverName,
                toolsLibraries.Count()));

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

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.FailedToResolveCommandSpec,
                ProjectToolsCommandResolverName));

            return null;
        }

        private CommandSpec ResolveCommandSpecFromToolLibrary(
            SingleProjectInfo toolLibraryRange,
            string commandName,
            IEnumerable<string> args,
            IProject project)
        {
            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.AttemptingToResolveCommandSpec,
                ProjectToolsCommandResolverName,
                toolLibraryRange.Name));

            var possiblePackageRoots = GetPossiblePackageRoots(project).ToList();
            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.NuGetPackagesRoot,
                ProjectToolsCommandResolverName,
                string.Join(Environment.NewLine, possiblePackageRoots.Select((p) => $"- {p}"))));

            var toolPackageFramework = project.DotnetCliToolTargetFramework;

            string nugetPackagesRoot;
            var toolLockFile = GetToolLockFile(
                toolLibraryRange,
                toolPackageFramework,
                possiblePackageRoots,
                out nugetPackagesRoot);

            if (toolLockFile == null)
            {
                return null;
            }

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.FoundToolLockFile,
                ProjectToolsCommandResolverName,
                toolLockFile.Path));

            var toolLibrary = toolLockFile.Targets
                .FirstOrDefault(t => toolPackageFramework == t.TargetFramework)
                ?.Libraries.FirstOrDefault(
                    l => StringComparer.OrdinalIgnoreCase.Equals(l.Name, toolLibraryRange.Name));
            if (toolLibrary == null)
            {
                Reporter.Verbose.WriteLine(string.Format(
                    LocalizableStrings.LibraryNotFoundInLockFile,
                    ProjectToolsCommandResolverName));

                return null;
            }

            var depsFileRoot = Path.GetDirectoryName(toolLockFile.Path);

            var depsFilePath = GetToolDepsFilePath(
                toolLibraryRange,
                toolPackageFramework,
                toolLockFile,
                depsFileRoot);

            var normalizedNugetPackagesRoot = PathUtility.EnsureNoTrailingDirectorySeparator(nugetPackagesRoot);

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.AttemptingToCreateCommandSpec,
                ProjectToolsCommandResolverName));

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
                Reporter.Verbose.WriteLine(string.Format(
                    LocalizableStrings.CommandSpecIsNull,
                    ProjectToolsCommandResolverName));
            }

            commandSpec?.AddEnvironmentVariablesFromProject(project);

            return commandSpec;
        }

        private IEnumerable<string> GetPossiblePackageRoots(IProject project)
        {
            if (project.TryGetLockFile(out LockFile lockFile))
            {
                return lockFile.PackageFolders.Select((packageFolder) => packageFolder.Path);
            }

            return Enumerable.Empty<string>();
        }

        private LockFile GetToolLockFile(
            SingleProjectInfo toolLibrary,
            NuGetFramework framework,
            IEnumerable<string> possibleNugetPackagesRoot,
            out string nugetPackagesRoot)
        {
            foreach (var packagesRoot in possibleNugetPackagesRoot)
            {
                if (TryGetToolLockFile(toolLibrary, framework, packagesRoot, out LockFile lockFile))
                {
                    nugetPackagesRoot = packagesRoot;
                    return lockFile;
                }
            }

            nugetPackagesRoot = null;
            return null;
        }


        private static async Task<bool> FileExistsWithLock(string path)
        {
            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                path, 
                lockedToken => Task.FromResult(File.Exists(path)),
                CancellationToken.None);
        }

        private bool TryGetToolLockFile(
            SingleProjectInfo toolLibrary,
            NuGetFramework framework,
            string nugetPackagesRoot,
            out LockFile lockFile)
        {
            lockFile = null;
            var lockFilePath = GetToolLockFilePath(toolLibrary, framework, nugetPackagesRoot);

            if (!FileExistsWithLock(lockFilePath).Result)
            {
                return false;
            }

            try
            {
                lockFile = new LockFileFormat()
                    .ReadWithLock(lockFilePath)
                    .Result;
            }
            catch (FileFormatException ex)
            {
                throw ex;
            }

            return true;
        }

        private string GetToolLockFilePath(
            SingleProjectInfo toolLibrary,
            NuGetFramework framework,
            string nugetPackagesRoot)
        {
            var toolPathCalculator = new ToolPathCalculator(nugetPackagesRoot);

            return toolPathCalculator.GetBestLockFilePath(
                toolLibrary.Name,
                VersionRange.Parse(toolLibrary.Version),
                framework);
        }

        private string GetToolDepsFilePath(
            SingleProjectInfo toolLibrary,
            NuGetFramework framework,
            LockFile toolLockFile,
            string depsPathRoot)
        {
            var depsJsonPath = Path.Combine(
                depsPathRoot,
                toolLibrary.Name + FileNameSuffixes.DepsJson);

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.ExpectDepsJsonAt,
                ProjectToolsCommandResolverName,
                depsJsonPath));

            EnsureToolJsonDepsFileExists(toolLockFile, framework, depsJsonPath, toolLibrary);

            return depsJsonPath;
        }

        private void EnsureToolJsonDepsFileExists(
            LockFile toolLockFile,
            NuGetFramework framework,
            string depsPath,
            SingleProjectInfo toolLibrary)
        {
            if (!File.Exists(depsPath))
            {
                GenerateDepsJsonFile(toolLockFile, framework, depsPath, toolLibrary);
            }
        }

        internal void GenerateDepsJsonFile(
            LockFile toolLockFile,
            NuGetFramework framework,
            string depsPath,
            SingleProjectInfo toolLibrary)
        {
            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.GeneratingDepsJson,
                depsPath));

            var dependencyContext = new DepsJsonBuilder()
                .Build(toolLibrary, null, toolLockFile, framework, null);

            var tempDepsFile = Path.GetTempFileName();
            using (var fileStream = File.Open(tempDepsFile, FileMode.Open, FileAccess.Write))
            {
                var dependencyContextWriter = new DependencyContextWriter();

                dependencyContextWriter.Write(dependencyContext, fileStream);
            }

            try
            {
                File.Move(tempDepsFile, depsPath);
            }
            catch (Exception e)
            {
                Reporter.Verbose.WriteLine(string.Format(
                    LocalizableStrings.UnableToGenerateDepsJson,
                    e.Message));
                
                try
                {
                    File.Delete(tempDepsFile);
                }
                catch (Exception e2)
                {
                    Reporter.Verbose.WriteLine(string.Format(
                        LocalizableStrings.UnableToDeleteTemporaryDepsJson,
                        e2.Message));
                }
            }
        }
    }
}
