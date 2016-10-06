// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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