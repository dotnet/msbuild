// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    public abstract class AbstractPathBasedCommandResolver : ICommandResolver
    {
        protected IEnvironmentProvider _environment;
        protected IPlatformCommandSpecFactory _commandSpecFactory;

        public AbstractPathBasedCommandResolver(IEnvironmentProvider environment,
            IPlatformCommandSpecFactory commandSpecFactory)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (commandSpecFactory == null)
            {
                throw new ArgumentNullException(nameof(commandSpecFactory));
            }

            _environment = environment;
            _commandSpecFactory = commandSpecFactory;
        }

        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.CommandName == null)
            {
                return null;
            }

            var commandPath = ResolveCommandPath(commandResolverArguments);

            if (commandPath == null)
            {
                return null;
            }

            return _commandSpecFactory.CreateCommandSpec(
                    commandResolverArguments.CommandName,
                    commandResolverArguments.CommandArguments.OrEmptyIfNull(),
                    commandPath,
                    _environment);
        }

        internal abstract string ResolveCommandPath(CommandResolverArguments commandResolverArguments);
    }
}
