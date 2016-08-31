using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel.Graph;

namespace Microsoft.DotNet.Cli.Utils
{
    public interface IPackagedCommandSpecFactory
    {
        CommandSpec CreateCommandSpecFromLibrary(
            LockFileTargetLibrary toolLibrary,
            string commandName,
            IEnumerable<string> commandArguments,
            IEnumerable<string> allowedExtensions,
            string nugetPackagesRoot,
            CommandResolutionStrategy commandResolutionStrategy,
            string depsFilePath,
            string runtimeConfigPath);
        
    }
}
