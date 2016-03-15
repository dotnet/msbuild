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
    public class PackagedCommandSpecFactory : IPackagedCommandSpecFactory
    {
        public CommandSpec CreateCommandSpecFromLibrary(
            LockFilePackageLibrary library,
            string commandName,
            IEnumerable<string> commandArguments,
            IEnumerable<string> allowedExtensions,
            string nugetPackagesRoot,
            CommandResolutionStrategy commandResolutionStrategy,
            string depsFilePath)
        {
            var packageDirectory = GetPackageDirectoryFullPath(library, nugetPackagesRoot);

            if (!Directory.Exists(packageDirectory))
            {
                return null;
            }

            var commandFile = GetCommandFileRelativePath(library, commandName, allowedExtensions);

            if (commandFile == null)
            {
                return null;
            }

            var commandPath = Path.Combine(packageDirectory, commandFile);

            return CreateCommandSpecWrappingWithCorehostfDll(
                commandPath, 
                commandArguments, 
                depsFilePath, 
                commandResolutionStrategy);
        }

        private string GetPackageDirectoryFullPath(LockFilePackageLibrary library, string nugetPackagesRoot)
        {
            var packageDirectory = new VersionFolderPathResolver(nugetPackagesRoot)
                .GetInstallPath(library.Name, library.Version);

            return packageDirectory;
        }

        private string GetCommandFileRelativePath(
            LockFilePackageLibrary library, 
            string commandName, 
            IEnumerable<string> allowedExtensions)
        {
            // TODO: Should command names be case sensitive?
            return library.Files
                    .Where(f => Path.GetFileNameWithoutExtension(f) == commandName)
                    .Where(e => allowedExtensions.Contains(Path.GetExtension(e)))
                    .FirstOrDefault();
        }

        private CommandSpec CreateCommandSpecWrappingWithCorehostfDll(
            string commandPath, 
            IEnumerable<string> commandArguments, 
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy)
        {
            var commandExtension = Path.GetExtension(commandPath);

            if (commandExtension == FileNameSuffixes.DotNet.DynamicLib)
            {
                return CreatePackageCommandSpecUsingCorehost(
                    commandPath, 
                    commandArguments, 
                    depsFilePath, 
                    commandResolutionStrategy);
            }
            
            return CreateCommandSpec(commandPath, commandArguments, commandResolutionStrategy);
        }

        private CommandSpec CreatePackageCommandSpecUsingCorehost(
            string commandPath, 
            IEnumerable<string> commandArguments, 
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy)
        {
            var corehost = CoreHost.HostExePath;

            var arguments = new List<string>();
            arguments.Add(commandPath);

            if (depsFilePath != null)
            {
                arguments.Add("--depsfile");
                arguments.Add(depsFilePath);
            }

            arguments.AddRange(commandArguments);

            return CreateCommandSpec(corehost, arguments, commandResolutionStrategy);
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
