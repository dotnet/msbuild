// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli
{
    public class DotNetCommandFactory : ICommandFactory
    {
        private bool _alwaysRunOutOfProc;

        public DotNetCommandFactory(bool alwaysRunOutOfProc = false)
        {
            _alwaysRunOutOfProc = alwaysRunOutOfProc;
        }

        public ICommand Create(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration)
        {
            if (!_alwaysRunOutOfProc && TryGetBuiltInCommand(commandName, out var builtInCommand))
            {
                Debug.Assert(framework == null, "BuiltInCommand doesn't support the 'framework' argument.");
                Debug.Assert(configuration == Constants.DefaultConfiguration, "BuiltInCommand doesn't support the 'configuration' argument.");

                return new BuiltInCommand(commandName, args, builtInCommand);
            }

            return CommandFactoryUsingResolver.CreateDotNet(commandName, args, framework, configuration);
        }

        private bool TryGetBuiltInCommand(string commandName, out Func<string[], int> commandFunc)
        {
            var command = Parser.GetBuiltInCommand(commandName);
            if (command != null && command.Action != null)
            {
                commandFunc = (args) => command.Action.InvokeAsync(Parser.Instance.Parse(args)).Result;
                return true;
            }
            commandFunc = null;
            return false;
        }
    }
}
