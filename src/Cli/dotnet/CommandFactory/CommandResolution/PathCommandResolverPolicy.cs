// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
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
