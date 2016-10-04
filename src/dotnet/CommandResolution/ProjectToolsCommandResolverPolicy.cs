using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.CommandResolution
{
    public class ProjectToolsCommandResolverPolicy : ICommandResolverPolicy
    {
        public CompositeCommandResolver CreateCommandResolver()
        {
            var defaultCommandResolverPolicy = new DefaultCommandResolverPolicy();
            var compositeCommandResolver = defaultCommandResolverPolicy.CreateCommandResolver();
            var packagedCommandSpecFactory = new PackagedCommandSpecFactory();

            compositeCommandResolver.AddCommandResolver(new ProjectToolsCommandResolver(packagedCommandSpecFactory));

            return compositeCommandResolver;
        }
    }
}