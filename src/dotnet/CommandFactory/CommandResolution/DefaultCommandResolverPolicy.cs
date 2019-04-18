// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;

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
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows)
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
