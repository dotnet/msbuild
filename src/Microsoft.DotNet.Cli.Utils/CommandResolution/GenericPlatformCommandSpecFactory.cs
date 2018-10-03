// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Utils
{
    public class GenericPlatformCommandSpecFactory : IPlatformCommandSpecFactory
    {
        public CommandSpec CreateCommandSpec(
           string commandName,
           IEnumerable<string> args,
           string commandPath,
           IEnvironmentProvider environment)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);
            return new CommandSpec(commandPath, escapedArgs);
        }
    }
}
