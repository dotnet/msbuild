using Microsoft.DotNet.InternalAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public class ScriptCommandResolverPolicy
    {
        public static CompositeCommandResolver Create()
        {
            var environment = new EnvironmentProvider();

            var platformCommandSpecFactory = default(IPlatformCommandSpecFactory);
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows)
            {
                platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
            }
            else
            {
                platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();
            }

            return CreateScriptCommandResolver(environment, platformCommandSpecFactory);
        }

        public static CompositeCommandResolver CreateScriptCommandResolver(
            IEnvironmentProvider environment,
            IPlatformCommandSpecFactory platformCommandSpecFactory)
        {
            var compositeCommandResolver = new CompositeCommandResolver();

            compositeCommandResolver.AddCommandResolver(new RootedCommandResolver());
            compositeCommandResolver.AddCommandResolver(new MuxerCommandResolver());
            compositeCommandResolver.AddCommandResolver(new ProjectPathCommandResolver(environment, platformCommandSpecFactory));
            compositeCommandResolver.AddCommandResolver(new AppBaseCommandResolver(environment, platformCommandSpecFactory));
            compositeCommandResolver.AddCommandResolver(new PathCommandResolver(environment, platformCommandSpecFactory));

            return compositeCommandResolver;
        }
    }
}
