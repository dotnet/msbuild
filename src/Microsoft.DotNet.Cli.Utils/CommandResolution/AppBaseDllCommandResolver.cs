using System.IO;
using System.Linq;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public class AppBaseDllCommandResolver : ICommandResolver
    {
        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.CommandName == null)
            {
                return null;
            }
            if (commandResolverArguments.CommandName.EndsWith(FileNameSuffixes.DotNet.DynamicLib))
            {
                var localPath = Path.Combine(ApplicationEnvironment.ApplicationBasePath,
                    commandResolverArguments.CommandName);
                if (File.Exists(localPath))
                {
                    var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                        new[] { localPath }
                        .Concat(commandResolverArguments.CommandArguments.OrEmptyIfNull()));
                    return new CommandSpec(
                        new Muxer().MuxerPath,
                        escapedArgs,
                        CommandResolutionStrategy.RootedPath);
                }
            }
            return null;
        }
    }
}
