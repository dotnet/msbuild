// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class DotNetCommandFactory : ICommandFactory
    {
        public ICommand Create(string commandName, IEnumerable<string> args, NuGetFramework framework = null, bool useComSpec = false)
        {
            return Command.CreateDotNet(commandName, args, framework, useComSpec);
        }
    }
}
