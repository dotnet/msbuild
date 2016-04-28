using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Utils
{
    public interface IPlatformCommandSpecFactory
    {
        CommandSpec CreateCommandSpec(
           string commandName,
           IEnumerable<string> args,
           string commandPath,
           CommandResolutionStrategy resolutionStrategy,
           IEnvironmentProvider environment);
    }
}
