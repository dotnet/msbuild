// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using ConcurrencyUtilities = NuGet.Common.ConcurrencyUtilities;

namespace Microsoft.DotNet.CommandFactory
{
    public class ProjectToolsCommandResolver : ICommandResolver
    {
        private const string ProjectToolsCommandResolverName = "projecttoolscommandresolver";

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

            List<NuGetFramework> toolFrameworksToCheck = new List<NuGetFramework>();
            toolFrameworksToCheck.Add(project.DotnetCliToolTargetFramework);

            //  NuGet restore in Visual Studio may restore for netcoreapp1.0.  So if that happens, fall back to
            //  looking for a netcoreapp1.0 or netcoreapp1.1 tool restore.
            if (project.DotnetCliToolTargetFramework.Framework == FrameworkConstants.FrameworkIdentifiers.NetCoreApp &&
                project.DotnetCliToolTargetFramework.Version >= new Version(2, 0, 0))
            {
                toolFrameworksToCheck.Add(NuGetFramework.Parse("netcoreapp1.1"));
                toolFrameworksToCheck.Add(NuGetFramework.Parse("netcoreapp1.0"));
            }

            
            LockFile toolLockFile = null;
            NuGetFramework toolTargetFramework = null; ;

            foreach (var toolFramework in toolFrameworksToCheck)
            {
                toolLockFile = GetToolLockFile(
                    toolLibraryRange,
                    toolFramework,
                    possiblePackageRoots);

                if (toolLockFile != null)
                {
                    toolTargetFramework = toolFramework;
                    break;
                }
            }

            if (toolLockFile == null)
            {
                return null;
            }

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.FoundToolLockFile,
                ProjectToolsCommandResolverName,
                toolLockFile.Path));

            var toolLibrary = toolLockFile.Targets
                .FirstOrDefault(t => toolTargetFramework == t.TargetFramework)
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
                toolTargetFramework,
                toolLockFile,
                depsFileRoot,
                project.ToolDepsJsonGeneratorProject);

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.AttemptingToCreateCommandSpec,
                ProjectToolsCommandResolverName));

            var commandSpec = _packagedCommandSpecFactory.CreateCommandSpecFromLibrary(
                    toolLibrary,
                    commandName,
                    args,
                    _allowedCommandExtensions,
                    toolLockFile,
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
            IEnumerable<string> possibleNugetPackagesRoot)
        {
            foreach (var packagesRoot in possibleNugetPackagesRoot)
            {
                if (TryGetToolLockFile(toolLibrary, framework, packagesRoot, out LockFile lockFile))
                {
                    return lockFile;
                }
            }

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
            catch (FileFormatException)
            {
                throw;
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
            string depsPathRoot,
            string toolDepsJsonGeneratorProject)
        {
            var depsJsonPath = Path.Combine(
                depsPathRoot,
                toolLibrary.Name + FileNameSuffixes.DepsJson);

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.ExpectDepsJsonAt,
                ProjectToolsCommandResolverName,
                depsJsonPath));

            EnsureToolJsonDepsFileExists(toolLockFile, framework, depsJsonPath, toolLibrary, toolDepsJsonGeneratorProject);

            return depsJsonPath;
        }

        private void EnsureToolJsonDepsFileExists(
            LockFile toolLockFile,
            NuGetFramework framework,
            string depsPath,
            SingleProjectInfo toolLibrary,
            string toolDepsJsonGeneratorProject)
        {
            if (!File.Exists(depsPath))
            {
                GenerateDepsJsonFile(toolLockFile, framework, depsPath, toolLibrary, toolDepsJsonGeneratorProject);
            }
        }

        internal void GenerateDepsJsonFile(
            LockFile toolLockFile,
            NuGetFramework framework,
            string depsPath,
            SingleProjectInfo toolLibrary,
            string toolDepsJsonGeneratorProject)
        {
            if (string.IsNullOrEmpty(toolDepsJsonGeneratorProject) ||
                !File.Exists(toolDepsJsonGeneratorProject))
            {
                throw new GracefulException(LocalizableStrings.DepsJsonGeneratorProjectNotSet);
            }

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.GeneratingDepsJson,
                depsPath));

            var tempDepsFile = Path.GetTempFileName();

            var args = new List<string>();

            args.Add(toolDepsJsonGeneratorProject);
            args.Add($"-property:ProjectAssetsFile=\"{toolLockFile.Path}\"");
            args.Add($"-property:ToolName={toolLibrary.Name}");
            args.Add($"-property:ProjectDepsFilePath={tempDepsFile}");

            var toolTargetFramework = toolLockFile.Targets.First().TargetFramework.GetShortFolderName();
            args.Add($"-property:TargetFramework={toolTargetFramework}");


            //  Look for the .props file in the Microsoft.NETCore.App package, until NuGet
            //  generates .props and .targets files for tool restores (https://github.com/NuGet/Home/issues/5037)
            var platformLibrary = toolLockFile.Targets
                .FirstOrDefault(t => framework == t.TargetFramework)
                ?.GetPlatformLibrary();

            if (platformLibrary != null)
            {
                string buildRelativePath = platformLibrary.Build.FirstOrDefault()?.Path;

                var platformLibraryPath = toolLockFile.GetPackageDirectory(platformLibrary);

                if (platformLibraryPath != null && buildRelativePath != null)
                {
                    //  Get rid of "_._" filename
                    buildRelativePath = Path.GetDirectoryName(buildRelativePath);

                    string platformLibraryBuildFolderPath = Path.Combine(platformLibraryPath, buildRelativePath);
                    var platformLibraryPropsFile = Directory.GetFiles(platformLibraryBuildFolderPath, "*.props").FirstOrDefault();

                    if (platformLibraryPropsFile != null)
                    {
                        args.Add($"-property:AdditionalImport={platformLibraryPropsFile}");
                    }
                }
            }

            //  Delete temporary file created by Path.GetTempFileName(), otherwise the GenerateBuildDependencyFile target
            //  will think the deps file is up-to-date and skip executing
            File.Delete(tempDepsFile);

            var msBuildExePath = _environment.GetEnvironmentVariable(Constants.MSBUILD_EXE_PATH);

            msBuildExePath = string.IsNullOrEmpty(msBuildExePath) ?
                Path.Combine(AppContext.BaseDirectory, "MSBuild.dll") :
                msBuildExePath;

            Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.MSBuildArgs,
                ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args)));

            int result;
            string stdOut;
            string stdErr;

            var forwardingAppWithoutLogging = new MSBuildForwardingAppWithoutLogging(args, msBuildExePath);
            if (forwardingAppWithoutLogging.ExecuteMSBuildOutOfProc)
            {
                result = forwardingAppWithoutLogging
                    .GetProcessStartInfo()
                    .ExecuteAndCaptureOutput(out stdOut, out stdErr);
            }
            else
            {
                // Execute and capture output of MSBuild running in-process.
                var outWriter = new StringWriter();
                var errWriter = new StringWriter();
                var savedOutWriter = Console.Out;
                var savedErrWriter = Console.Error;
                try
                {
                    Console.SetOut(outWriter);
                    Console.SetError(errWriter);

                    result = forwardingAppWithoutLogging.Execute();

                    stdOut = outWriter.ToString();
                    stdErr = errWriter.ToString();
                }
                finally
                {
                    Console.SetOut(savedOutWriter);
                    Console.SetError(savedErrWriter);
                }
            }

            if (result != 0)
            {
                Reporter.Verbose.WriteLine(string.Format(
                    LocalizableStrings.UnableToGenerateDepsJson,
                    stdOut + Environment.NewLine + stdErr));

                throw new GracefulException(string.Format(LocalizableStrings.UnableToGenerateDepsJson, toolDepsJsonGeneratorProject));
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
