// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    public interface IPlatformCommandSpecFactory
    {
        CommandSpec CreateCommandSpec(
           string commandName,
           IEnumerable<string> args,
           string commandPath,
           IEnvironmentProvider environment);
    }
}
