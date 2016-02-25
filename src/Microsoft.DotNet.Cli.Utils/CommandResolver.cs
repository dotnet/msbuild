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
<<<<<<< HEAD
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
=======
        private static DefaultCommandResolver _defaultCommandResolver;
        private static ScriptCommandResolver _scriptCommandResolver;
>>>>>>> 9c4329a... Refactor CommandResolver into individual CommandResolver Implementation

        public static CommandSpec TryResolveCommandSpec(
            string commandName, 
            IEnumerable<string> args, 
            NuGetFramework framework = null, 
            string configuration=Constants.DefaultConfiguration, 
            string outputPath=null)
        {
            var commandResolverArgs = new CommandResolverArguments
            {
                CommandName = commandName,
                CommandArguments = args,
                Framework = framework,
                ProjectDirectory = Directory.GetCurrentDirectory(),
                Configuration = configuration
            };

            if (_defaultCommandResolver == null)
            {
                _defaultCommandResolver = DefaultCommandResolver.Create();
            }

            return _defaultCommandResolver.Resolve(commandResolverArgs);
        }
        
        public static CommandSpec TryResolveScriptCommandSpec(
            string commandName, 
            IEnumerable<string> args, 
            Project project, 
            string[] inferredExtensionList)
        {
            var commandResolverArgs = new CommandResolverArguments
            {
                CommandName = commandName,
                CommandArguments = args,
                ProjectDirectory = project.ProjectDirectory,
                InferredExtensions = inferredExtensionList
            };

            if (_scriptCommandResolver == null)
            {
                _scriptCommandResolver = ScriptCommandResolver.Create();
            }

            return _scriptCommandResolver.Resolve(commandResolverArgs);
        }
    }
}

