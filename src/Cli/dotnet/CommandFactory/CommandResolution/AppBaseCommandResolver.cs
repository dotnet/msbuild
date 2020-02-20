// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.CommandFactory
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
    }
}
