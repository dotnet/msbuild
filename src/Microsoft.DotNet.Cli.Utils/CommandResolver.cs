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
    internal static class CommandResolver
    {
        public static CommandSpec TryResolveCommandSpec(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration,
            string outputPath = null)
        {
            return ResolveFromRootedCommand(commandName, args) ??
                   ResolveFromProjectDependencies(commandName, args, framework, configuration, outputPath) ??
                   ResolveFromProjectTools(commandName, args) ??
                   ResolveFromAppBase(commandName, args) ??
                   ResolveFromPath(commandName, args);
        }
        
        public static CommandSpec TryResolveScriptCommandSpec(string commandName, IEnumerable<string> args, Project project, string[] inferredExtensionList)
        {
            return ResolveFromRootedCommand(commandName, args) ??
                   ResolveFromProjectPath(commandName, args, project, inferredExtensionList) ??
                   ResolveFromAppBase(commandName, args) ??
                   ResolveFromPath(commandName, args);
        }
        

        private static CommandSpec ResolveFromPath(string commandName, IEnumerable<string> args)
        {
            var commandPath = Env.GetCommandPath(commandName);
            return commandPath == null
                ? null
                : CreateCommandSpecPreferringExe(commandName, args, commandPath, CommandResolutionStrategy.Path);
        }

        private static CommandSpec ResolveFromAppBase(string commandName, IEnumerable<string> args)
        {
            var commandPath = Env.GetCommandPathFromRootPath(PlatformServices.Default.Application.ApplicationBasePath, commandName);
            return commandPath == null
                ? null
                : CreateCommandSpecPreferringExe(commandName, args, commandPath, CommandResolutionStrategy.BaseDirectory);
        }
        
        private static CommandSpec ResolveFromProjectPath(string commandName, IEnumerable<string> args, Project project, string[] inferredExtensionList)
        {
            var commandPath = Env.GetCommandPathFromRootPath(project.ProjectDirectory, commandName, inferredExtensionList);
            return commandPath == null
                ? null
                : CreateCommandSpecPreferringExe(commandName, args, commandPath, CommandResolutionStrategy.ProjectLocal);
        }

        private static CommandSpec ResolveFromRootedCommand(string commandName, IEnumerable<string> args)
        {
            if (Path.IsPathRooted(commandName))
            {
                var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);
                return new CommandSpec(commandName, escapedArgs, CommandResolutionStrategy.Path);
            }

            return null;
        }

        public static CommandSpec ResolveFromProjectDependencies(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework,
            string configuration,
            string outputPath)
        {
            if (framework == null) return null;

            var projectContext = GetProjectContext(framework);

            if (projectContext == null) return null;

            var commandPackage = GetCommandPackage(projectContext, commandName);

            if (commandPackage == null) return null;

            var depsPath = projectContext.GetOutputPaths(configuration, outputPath: outputPath).RuntimeFiles.Deps;

            return ConfigureCommandFromPackage(commandName, args, commandPackage, projectContext, depsPath);
        }

        private static ProjectContext GetProjectContext(NuGetFramework framework)
        {
            var projectRootPath = Directory.GetCurrentDirectory();

            if (!File.Exists(Path.Combine(projectRootPath, Project.FileName)))
            {
                return null;
            }

            var projectContext = ProjectContext.Create(projectRootPath, framework, PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers());
            return projectContext;
        }

        private static PackageDescription GetCommandPackage(ProjectContext projectContext, string commandName)
        {
            return projectContext.LibraryManager.GetLibraries()
                .Where(l => l.GetType() == typeof(PackageDescription))
                .Select(l => l as PackageDescription)
                .FirstOrDefault(p => p.Library.Files
                    .Select(Path.GetFileName)
                    .Where(f => Path.GetFileNameWithoutExtension(f) == commandName)
                    .Select(Path.GetExtension)
                    .Any(e => Env.ExecutableExtensions.Contains(e) ||
                              e == FileNameSuffixes.DotNet.DynamicLib));
        }

        public static CommandSpec ResolveFromProjectTools(string commandName, IEnumerable<string> args)
        {
            var context = GetProjectContext(FrameworkConstants.CommonFrameworks.NetStandardApp15);

            if (context == null)
            {
                return null;
            }

            var commandLibrary = context.ProjectFile.Tools
                .FirstOrDefault(l => l.Name == commandName);

            if (commandLibrary == default(LibraryRange))
            {
                return null;
            }

            var lockPath = Path.Combine(context.ProjectDirectory, "artifacts", "Tools", commandName,
                "project.lock.json");

            if (!File.Exists(lockPath))
            {
                return null;
            }

            var lockFile = LockFileReader.Read(lockPath);

            var lib = lockFile.PackageLibraries.FirstOrDefault(l => l.Name == commandName);
            var packageDir = new VersionFolderPathResolver(context.PackagesDirectory)
                .GetInstallPath(lib.Name, lib.Version);

            return Directory.Exists(packageDir)
                ? ConfigureCommandFromPackage(commandName, args, lib.Files, packageDir)
                : null;
        }

        private static CommandSpec ConfigureCommandFromPackage(string commandName, IEnumerable<string> args, string packageDir)
        {
            var commandPackage = new PackageFolderReader(packageDir);

            var files = commandPackage.GetFiles();

            return ConfigureCommandFromPackage(commandName, args, files, packageDir);
        }

        private static CommandSpec ConfigureCommandFromPackage(string commandName, IEnumerable<string> args,
            PackageDescription commandPackage, ProjectContext projectContext, string depsPath = null)
        {
            var files = commandPackage.Library.Files;

            var packageRoot = projectContext.PackagesDirectory;

            var packagePath = commandPackage.Path;

            var packageDir = Path.Combine(packageRoot, packagePath);

            return ConfigureCommandFromPackage(commandName, args, files, packageDir, depsPath);
        }

        private static CommandSpec ConfigureCommandFromPackage(string commandName, IEnumerable<string> args,
            IEnumerable<string> files, string packageDir, string depsPath = null)
        {
            var fileName = string.Empty;

            var commandPath = files
                .FirstOrDefault(f => Env.ExecutableExtensions.Contains(Path.GetExtension(f)));

            if (commandPath == null)
            {
                var dllPath = files
                    .Where(f => Path.GetFileName(f) == commandName + FileNameSuffixes.DotNet.DynamicLib)
                    .Select(f => Path.Combine(packageDir, f))
                    .FirstOrDefault();

                fileName = CoreHost.HostExePath;

                var additionalArgs = new List<string>();
                additionalArgs.Add(dllPath);

                if (depsPath != null)
                {
                    additionalArgs.Add($"--depsfile:{depsPath}");
                }

                args = additionalArgs.Concat(args);
            }
            else
            {
                fileName = Path.Combine(packageDir, commandPath);
            }

            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);
            return new CommandSpec(fileName, escapedArgs, CommandResolutionStrategy.NugetPackage);
        }

        private static CommandSpec CreateCommandSpecPreferringExe(
            string commandName,
            IEnumerable<string> args,
            string commandPath,
            CommandResolutionStrategy resolutionStrategy)
        {
            var useComSpec = false;
            
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows &&
                Path.GetExtension(commandPath).Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                var preferredCommandPath = Env.GetCommandPath(commandName, ".exe");

                // Use cmd if we can't find an exe
                if (preferredCommandPath == null)
                {
                    useComSpec = true;
                }
                else
                {
                    commandPath = preferredCommandPath;
                }
            }

            if (useComSpec)
            {
                return CreateCmdCommandSpec(commandPath, args, resolutionStrategy);
            }
            else
            {
                var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);
                return new CommandSpec(commandPath, escapedArgs, resolutionStrategy);
            }
        }

        private static CommandSpec CreateCmdCommandSpec(
            string command,
            IEnumerable<string> args,
            CommandResolutionStrategy resolutionStrategy)
        {
            var comSpec = Environment.GetEnvironmentVariable("ComSpec");
            
            // Handle the case where ComSpec is already the command
            if (command.Equals(comSpec, StringComparison.OrdinalIgnoreCase))
            {
                command = args.FirstOrDefault();
                args = args.Skip(1);
            }
            var cmdEscapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForCmdProcessStart(args);

            if (ArgumentEscaper.ShouldSurroundWithQuotes(command))
            {
                command = $"\"{command}\"";
            }

            var escapedArgString = $"/s /c \"{command} {cmdEscapedArgs}\"";

            return new CommandSpec(comSpec, escapedArgString, resolutionStrategy);
        }
    }
}

