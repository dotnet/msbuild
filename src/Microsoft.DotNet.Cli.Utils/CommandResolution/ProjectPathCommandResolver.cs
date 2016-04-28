namespace Microsoft.DotNet.Cli.Utils
{
    public class ProjectPathCommandResolver : AbstractPathBasedCommandResolver
    {
        public ProjectPathCommandResolver(IEnvironmentProvider environment,
            IPlatformCommandSpecFactory commandSpecFactory) : base(environment, commandSpecFactory) { }

        internal override string ResolveCommandPath(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.ProjectDirectory == null)
            {
                return null;
            }

            return _environment.GetCommandPathFromRootPath(
                commandResolverArguments.ProjectDirectory,
                commandResolverArguments.CommandName,
                commandResolverArguments.InferredExtensions.OrEmptyIfNull());
        }

        internal override CommandResolutionStrategy GetCommandResolutionStrategy()
        {
            return CommandResolutionStrategy.ProjectLocal;
        }
    }
}
