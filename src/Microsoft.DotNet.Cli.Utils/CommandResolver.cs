using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Frameworks;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class CommandResolver
    {
        public static CommandSpec TryResolveCommandSpec(string commandName, IEnumerable<string> args, NuGetFramework framework = null, bool useComSpec = false)
        {
            return ResolveFromRootedCommand(commandName, args, useComSpec) ??
                   ResolveFromProjectDependencies(commandName, args, framework, useComSpec) ??
                   ResolveFromProjectTools(commandName, args, useComSpec) ??
                   ResolveFromAppBase(commandName, args, useComSpec) ??
                   ResolveFromPath(commandName, args, useComSpec);
        }

        private static CommandSpec ResolveFromPath(string commandName, IEnumerable<string> args, bool useComSpec = false)
        {
            var commandPath = Env.GetCommandPath(commandName);
            return commandPath == null 
                ? null 
                : CreateCommandSpecPreferringExe(commandName, args, commandPath, CommandResolutionStrategy.Path, useComSpec);
        }

        private static CommandSpec ResolveFromAppBase(string commandName, IEnumerable<string> args, bool useComSpec = false)
        {
            var commandPath = Env.GetCommandPathFromAppBase(AppContext.BaseDirectory, commandName);
            return commandPath == null 
                ? null 
                : CreateCommandSpecPreferringExe(commandName, args, commandPath, CommandResolutionStrategy.BaseDirectory, useComSpec);
        }

        private static CommandSpec ResolveFromRootedCommand(string commandName, IEnumerable<string> args, bool useComSpec = false)
        {
            if (Path.IsPathRooted(commandName))
            {
                if (useComSpec)
                {
                    return CreateComSpecCommandSpec(commandName, args, CommandResolutionStrategy.Path);
                }
                else
                {
                    var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArray(args);
                    return new CommandSpec(commandName, escapedArgs, CommandResolutionStrategy.Path);
                }
                
            }

            return null;
        }

        public static CommandSpec ResolveFromProjectDependencies(string commandName, IEnumerable<string> args,
            NuGetFramework framework, bool useComSpec = false)
        {
            if (framework == null) return null;

            var projectContext = GetProjectContext(framework);

            if (projectContext == null) return null;

            var commandPackage = GetCommandPackage(projectContext, commandName);

            if (commandPackage == null) return null;

            var depsPath = GetDepsPath(projectContext, Constants.DefaultConfiguration);

            return ConfigureCommandFromPackage(commandName, args, commandPackage, projectContext, depsPath, useComSpec);
        }

        private static ProjectContext GetProjectContext(NuGetFramework framework)
        {
            var projectRootPath = Directory.GetCurrentDirectory();

            if (!File.Exists(Path.Combine(projectRootPath, Project.FileName)))
            {
                return null;
            }

            var projectContext = ProjectContext.Create(projectRootPath, framework);
            return projectContext;
        }

        private static PackageDescription GetCommandPackage(ProjectContext projectContext, string commandName)
        {
            return projectContext.LibraryManager.GetLibraries()
                .Where(l => l.GetType() == typeof (PackageDescription))
                .Select(l => l as PackageDescription)
                .FirstOrDefault(p => p.Library.Files
                    .Select(Path.GetFileName)
                    .Where(f => Path.GetFileNameWithoutExtension(f) == commandName)
                    .Select(Path.GetExtension)
                    .Any(e => Env.ExecutableExtensions.Contains(e) ||
                              e == FileNameSuffixes.DotNet.DynamicLib));
        }

        public static CommandSpec ResolveFromProjectTools(string commandName, IEnumerable<string> args, bool useComSpec = false)
        {
            var context = GetProjectContext(FrameworkConstants.CommonFrameworks.DnxCore50);

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
            PackageDescription commandPackage, ProjectContext projectContext, string depsPath = null, bool useComSpec = false)
        {
            var files = commandPackage.Library.Files;

            var packageRoot = projectContext.PackagesDirectory;

            var packagePath = commandPackage.Path;

            var packageDir = Path.Combine(packageRoot, packagePath);

            return ConfigureCommandFromPackage(commandName, args, files, packageDir, depsPath, useComSpec);
        }

        private static CommandSpec ConfigureCommandFromPackage(string commandName, IEnumerable<string> args,
            IEnumerable<string> files, string packageDir, string depsPath = null, bool useComSpec = false)
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
                    additionalArgs.Add("--depsfile");
                    additionalArgs.Add(depsPath);
                }

                args = additionalArgs.Concat(args);
            }
            else
            {
                fileName = Path.Combine(packageDir, commandPath);
            }

            if (useComSpec)
            {
                return CreateComSpecCommandSpec(fileName, args, CommandResolutionStrategy.NugetPackage);
            }
            else
            {
                var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArray(args);
                return new CommandSpec(fileName, escapedArgs, CommandResolutionStrategy.NugetPackage);
            }

            
        }

        private static string GetDepsPath(ProjectContext context, string buildConfiguration)
        {
            return Path.Combine(context.GetOutputDirectoryPath(buildConfiguration),
                context.ProjectFile.Name + FileNameSuffixes.Deps);
        }

        private static CommandSpec CreateCommandSpecPreferringExe(
            string commandName, 
            IEnumerable<string> args, 
            string commandPath,
            CommandResolutionStrategy resolutionStrategy, 
            bool useComSpec = false)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
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
                return CreateComSpecCommandSpec(commandPath, args, resolutionStrategy);
            }
            else
            {
                var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArray(args);
                return new CommandSpec(commandPath, escapedArgs, resolutionStrategy);
            }
        }

        private static CommandSpec CreateComSpecCommandSpec(
            string command, 
            IEnumerable<string> args, 
            CommandResolutionStrategy resolutionStrategy)
        {
            // To prevent Command Not Found, comspec gets passed in as
            // the command already in some cases
            var comSpec = Environment.GetEnvironmentVariable("ComSpec");
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

