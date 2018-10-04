// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
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
    }
}
