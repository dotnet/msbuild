namespace Microsoft.DotNet.Cli.Utils
{
    public class PathCommandResolver : AbstractPathBasedCommandResolver
    {
        public PathCommandResolver(IEnvironmentProvider environment,
            IPlatformCommandSpecFactory commandSpecFactory) : base(environment, commandSpecFactory) { }

        internal override string ResolveCommandPath(CommandResolverArguments commandResolverArguments)
        {
            return _environment.GetCommandPath(commandResolverArguments.CommandName);
        }

        internal override CommandResolutionStrategy GetCommandResolutionStrategy()
        {
            return CommandResolutionStrategy.Path;
        }
    }
}
