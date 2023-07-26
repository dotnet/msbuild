// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    public class PathCommandResolverPolicy : ICommandResolverPolicy
    {
        public CompositeCommandResolver CreateCommandResolver()
        {
            return Create();
        }

        public static CompositeCommandResolver Create()
        {
            var environment = new EnvironmentProvider();

            var platformCommandSpecFactory = default(IPlatformCommandSpecFactory);
            if (OperatingSystem.IsWindows())
            {
                platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
            }
            else
            {
                platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();
            }

            return CreatePathCommandResolverPolicy(
                environment,
                platformCommandSpecFactory);
        }

        public static CompositeCommandResolver CreatePathCommandResolverPolicy(
            IEnvironmentProvider environment,
            IPlatformCommandSpecFactory platformCommandSpecFactory)
        {
            var compositeCommandResolver = new CompositeCommandResolver();

            compositeCommandResolver.AddCommandResolver(
                new PathCommandResolver(environment, platformCommandSpecFactory));

            return compositeCommandResolver;
        }
    }
}
