using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Common;
using NuGet.Packaging;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    public class PackagedCommandSpecFactory : IPackagedCommandSpecFactory
    {
        public CommandSpec CreateCommandSpecFromLibrary(
            LockFileTargetLibrary toolLibrary,
            string commandName,
            IEnumerable<string> commandArguments,
            IEnumerable<string> allowedExtensions,
            string nugetPackagesRoot,
            CommandResolutionStrategy commandResolutionStrategy,
            string depsFilePath,
            string runtimeConfigPath)
        {

            var toolAssembly = toolLibrary?.RuntimeAssemblies
                    .FirstOrDefault(r => Path.GetFileNameWithoutExtension(r.Path) == commandName);

            if (toolAssembly == null)
            {
                return null;
            }
            
            var commandPath = GetCommandFilePath(nugetPackagesRoot, toolLibrary, toolAssembly);

            if (!File.Exists(commandPath))
            {
                return null;
            }

            return CreateCommandSpecWrappingWithMuxerIfDll(
                commandPath, 
                commandArguments, 
                depsFilePath, 
                commandResolutionStrategy,
                nugetPackagesRoot,
                runtimeConfigPath);
        }

        private string GetCommandFilePath(string nugetPackagesRoot, LockFileTargetLibrary toolLibrary, LockFileItem runtimeAssembly)
        {
            var packageDirectory = new VersionFolderPathResolver(nugetPackagesRoot)
                .GetInstallPath(toolLibrary.Name, toolLibrary.Version);

            var filePath = Path.Combine(packageDirectory, PathUtility.GetPathWithDirectorySeparator(runtimeAssembly.Path));

            return filePath;
        }

        private CommandSpec CreateCommandSpecWrappingWithMuxerIfDll(
            string commandPath, 
            IEnumerable<string> commandArguments, 
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy,
            string nugetPackagesRoot,
            string runtimeConfigPath)
        {
            var commandExtension = Path.GetExtension(commandPath);

            if (commandExtension == FileNameSuffixes.DotNet.DynamicLib)
            {
                return CreatePackageCommandSpecUsingMuxer(
                    commandPath, 
                    commandArguments, 
                    depsFilePath, 
                    commandResolutionStrategy,
                    nugetPackagesRoot,
                    runtimeConfigPath);
            }
            
            return CreateCommandSpec(commandPath, commandArguments, commandResolutionStrategy);
        }

        private CommandSpec CreatePackageCommandSpecUsingMuxer(
            string commandPath, 
            IEnumerable<string> commandArguments, 
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy,
            string nugetPackagesRoot,
            string runtimeConfigPath)
        {
            var host = string.Empty;
            var arguments = new List<string>();

            var muxer = new Muxer();

            host = muxer.MuxerPath;
            if (host == null)
            {
                throw new Exception("Unable to locate dotnet multiplexer");
            }

            arguments.Add("exec");

            if (runtimeConfigPath != null)
            {
                arguments.Add("--runtimeconfig");
                arguments.Add(runtimeConfigPath);
            }
            
            if (depsFilePath != null)
            {
                arguments.Add("--depsfile");
                arguments.Add(depsFilePath);
            }

            arguments.Add("--additionalprobingpath");
            arguments.Add(nugetPackagesRoot);

            arguments.Add(commandPath);
            arguments.AddRange(commandArguments);

            return CreateCommandSpec(host, arguments, commandResolutionStrategy);
        }

        private CommandSpec CreateCommandSpec(
            string commandPath, 
            IEnumerable<string> commandArguments,
            CommandResolutionStrategy commandResolutionStrategy)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(commandArguments);

            return new CommandSpec(commandPath, escapedArgs, commandResolutionStrategy);
        }
    }
}
