// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
