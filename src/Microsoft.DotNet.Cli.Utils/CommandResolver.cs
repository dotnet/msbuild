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
        public static CommandSpec TryResolveCommandSpec(string commandName, string args, NuGetFramework framework = null)
        {
            return ResolveFromRootedCommand(commandName, args) ??
                   ResolveFromProjectDependencies(commandName, args, framework) ??
                   ResolveFromProjectTools(commandName, args) ??
                   ResolveFromAppBase(commandName, args) ??
                   ResolveFromPath(commandName, args);
        }

        private static CommandSpec ResolveFromPath(string commandName, string args)
        {
            var commandPath = Env.GetCommandPath(commandName);

            return commandPath == null 
                ? null 
                : CreateCommandSpecPreferringExe(commandName, args, commandPath, CommandResolutionStrategy.Path);
        }

        private static CommandSpec ResolveFromAppBase(string commandName, string args)
        {
            var commandPath = Env.GetCommandPathFromAppBase(AppContext.BaseDirectory, commandName);

            return commandPath == null 
                ? null 
                : CreateCommandSpecPreferringExe(commandName, args, commandPath, CommandResolutionStrategy.BaseDirectory);
        }

        private static CommandSpec ResolveFromRootedCommand(string commandName, string args)
        {
            if (Path.IsPathRooted(commandName))
            {
                return new CommandSpec(commandName, args, CommandResolutionStrategy.Path);
            }

            return null;
        }

        public static CommandSpec ResolveFromProjectDependencies(string commandName, string args,
            NuGetFramework framework)
        {
            if (framework == null) return null;

            var projectContext = GetProjectContext(framework);

            if (projectContext == null) return null;

            var commandPackage = GetCommandPackage(projectContext, commandName);

            if (commandPackage == null) return null;

            var depsPath = GetDepsPath(projectContext, Constants.DefaultConfiguration);

            return ConfigureCommandFromPackage(commandName, args, commandPackage, projectContext, depsPath);
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

        public static CommandSpec ResolveFromProjectTools(string commandName, string args)
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

        private static CommandSpec ConfigureCommandFromPackage(string commandName, string args, string packageDir)
        {
            var commandPackage = new PackageFolderReader(packageDir);

            var files = commandPackage.GetFiles();

            return ConfigureCommandFromPackage(commandName, args, files, packageDir);
        }

        private static CommandSpec ConfigureCommandFromPackage(string commandName, string args,
            PackageDescription commandPackage, ProjectContext projectContext, string depsPath = null)
        {
            var files = commandPackage.Library.Files;

            var packageRoot = projectContext.PackagesDirectory;

            var packagePath = commandPackage.Path;

            var packageDir = Path.Combine(packageRoot, packagePath);

            return ConfigureCommandFromPackage(commandName, args, files, packageDir, depsPath);
        }

        private static CommandSpec ConfigureCommandFromPackage(string commandName, string args,
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

                var depsArg = string.Empty;

                if (depsPath != null)
                {
                    depsArg = $"\"--depsfile:{depsPath}\" ";
                }

                args = $"\"{dllPath}\" {depsArg}{args}";
            }
            else
            {
                fileName = Path.Combine(packageDir, commandPath);
            }

            return new CommandSpec(fileName, args, CommandResolutionStrategy.NugetPackage);
        }

        private static string GetDepsPath(ProjectContext context, string buildConfiguration)
        {
            return Path.Combine(context.GetOutputDirectoryPath(buildConfiguration),
                context.ProjectFile.Name + FileNameSuffixes.Deps);
        }

        private static CommandSpec CreateCommandSpecPreferringExe(string commandName, string args, string commandPath,
            CommandResolutionStrategy resolutionStrategy)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                Path.GetExtension(commandPath).Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                var preferredCommandPath = Env.GetCommandPath(commandName, ".exe");

                if (preferredCommandPath != null)
                {
                    commandPath = Environment.GetEnvironmentVariable("ComSpec");

                    args = $"/S /C \"\"{preferredCommandPath}\" {args}\"";
                }
            }

            return new CommandSpec(commandPath, args, resolutionStrategy);
        }
    }
}
