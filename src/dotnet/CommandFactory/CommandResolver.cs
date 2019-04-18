// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.CommandFactory
{
    internal class CommandResolver
    {
        public static CommandSpec TryResolveCommandSpec(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration,
            string outputPath = null,
            string applicationName = null)
        {
            return TryResolveCommandSpec(
                new DefaultCommandResolverPolicy(),
                commandName,
                args,
                framework,
                configuration,
                outputPath,
                applicationName);
        }

        public static CommandSpec TryResolveCommandSpec(
            ICommandResolverPolicy commandResolverPolicy,
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration,
            string outputPath = null,
            string applicationName = null)
        {
            var commandResolverArgs = new CommandResolverArguments
            {
                CommandName = commandName,
                CommandArguments = args,
                Framework = framework,
                ProjectDirectory = Directory.GetCurrentDirectory(),
                Configuration = configuration,
                OutputPath = outputPath,
                ApplicationName = applicationName
            };

            var defaultCommandResolver = commandResolverPolicy.CreateCommandResolver();

            return defaultCommandResolver.Resolve(commandResolverArgs);
        }
    }
}

