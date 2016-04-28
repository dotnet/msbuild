using Microsoft.DotNet.InternalAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public class AppBaseCommandResolver : AbstractPathBasedCommandResolver
    {
        public AppBaseCommandResolver(IEnvironmentProvider environment,
            IPlatformCommandSpecFactory commandSpecFactory) : base(environment, commandSpecFactory) { }

        internal override string ResolveCommandPath(CommandResolverArguments commandResolverArguments)
        {
            return _environment.GetCommandPathFromRootPath(
                ApplicationEnvironment.ApplicationBasePath,
                commandResolverArguments.CommandName);
        }

        internal override CommandResolutionStrategy GetCommandResolutionStrategy()
        {
            return CommandResolutionStrategy.BaseDirectory;
        }
    }
}
