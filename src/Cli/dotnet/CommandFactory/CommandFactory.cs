// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.CommandFactory
{
    public class CommandFactory : ICommandFactory
    {
        public ICommand Create(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration)
        {
            return CommandFactoryUsingResolver.Create(commandName, args, framework, configuration);
        }
    }
}
