// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    public class AppBaseCommandResolver : AbstractPathBasedCommandResolver
    {
        public AppBaseCommandResolver(IEnvironmentProvider environment,
            IPlatformCommandSpecFactory commandSpecFactory) : base(environment, commandSpecFactory) { }

        internal override string ResolveCommandPath(CommandResolverArguments commandResolverArguments)
        {
            return _environment.GetCommandPathFromRootPath(
                AppContext.BaseDirectory,
                commandResolverArguments.CommandName);
        }
    }
}
