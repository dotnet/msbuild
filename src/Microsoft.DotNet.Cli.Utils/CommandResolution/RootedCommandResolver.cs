// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.DotNet.Cli.Utils
{
    public class RootedCommandResolver : ICommandResolver
    {
        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.CommandName == null)
            {
                return null;
            }

            if (Path.IsPathRooted(commandResolverArguments.CommandName))
            {
                var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                    commandResolverArguments.CommandArguments.OrEmptyIfNull());

                return new CommandSpec(commandResolverArguments.CommandName, escapedArgs);
            }

            return null;
        }
    }
}
