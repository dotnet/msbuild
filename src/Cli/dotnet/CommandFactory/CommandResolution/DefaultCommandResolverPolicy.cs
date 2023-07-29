// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    public class DefaultCommandResolverPolicy : ICommandResolverPolicy
    {
        public CompositeCommandResolver CreateCommandResolver()
        {
            return Create();
        }

        public static CompositeCommandResolver Create()
        {
            var environment = new EnvironmentProvider();
            var packagedCommandSpecFactory = new PackagedCommandSpecFactoryWithCliRuntime();
            var publishedPathCommandSpecFactory = new PublishPathCommandSpecFactory();

            var platformCommandSpecFactory = default(IPlatformCommandSpecFactory);
            if (OperatingSystem.IsWindows())
            {
                platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
            }
            else
            {
                platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();
            }

            return CreateDefaultCommandResolver(
                environment,
                packagedCommandSpecFactory,
                platformCommandSpecFactory,
                publishedPathCommandSpecFactory);
        }

        public static CompositeCommandResolver CreateDefaultCommandResolver(
            IEnvironmentProvider environment,
            IPackagedCommandSpecFactory packagedCommandSpecFactory,
            IPlatformCommandSpecFactory platformCommandSpecFactory,
            IPublishedPathCommandSpecFactory publishedPathCommandSpecFactory)
        {
            var compositeCommandResolver = new CompositeCommandResolver();

            compositeCommandResolver.AddCommandResolver(new MuxerCommandResolver());
            compositeCommandResolver.AddCommandResolver(new DotnetToolsCommandResolver());
            compositeCommandResolver.AddCommandResolver(new LocalToolsCommandResolver());
            compositeCommandResolver.AddCommandResolver(new RootedCommandResolver());
            compositeCommandResolver.AddCommandResolver(
                new ProjectToolsCommandResolver(packagedCommandSpecFactory, environment));
            compositeCommandResolver.AddCommandResolver(new AppBaseDllCommandResolver());
            compositeCommandResolver.AddCommandResolver(
                new AppBaseCommandResolver(environment, platformCommandSpecFactory));
            compositeCommandResolver.AddCommandResolver(
                new PathCommandResolver(environment, platformCommandSpecFactory));
            compositeCommandResolver.AddCommandResolver(
                new PublishedPathCommandResolver(environment, publishedPathCommandSpecFactory));

            return compositeCommandResolver;
        }
    }
}
