// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    public class AppBaseDllCommandResolver : ICommandResolver
    {
        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.CommandName == null)
            {
                return null;
            }
            if (commandResolverArguments.CommandName.EndsWith(FileNameSuffixes.DotNet.DynamicLib))
            {
                var localPath = Path.Combine(AppContext.BaseDirectory,
                    commandResolverArguments.CommandName);
                if (File.Exists(localPath))
                {
                    var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                        new[] { localPath }
                        .Concat(commandResolverArguments.CommandArguments.OrEmptyIfNull()));
                    return new CommandSpec(
                        new Muxer().MuxerPath,
                        escapedArgs);
                }
            }
            return null;
        }
    }
}
