using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Utils
{
    public class GenericPlatformCommandSpecFactory : IPlatformCommandSpecFactory
    {
        public CommandSpec CreateCommandSpec(
           string commandName,
           IEnumerable<string> args,
           string commandPath,
           CommandResolutionStrategy resolutionStrategy,
           IEnvironmentProvider environment)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);
            return new CommandSpec(commandPath, escapedArgs, resolutionStrategy);
        }
    }
}
