// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
            BuiltInCommandMetadata builtInCommand;
            if (!_alwaysRunOutOfProc && Program.TryGetBuiltInCommand(commandName, out builtInCommand))
            {
                Debug.Assert(framework == null, "BuiltInCommand doesn't support the 'framework' argument.");
                Debug.Assert(configuration == Constants.DefaultConfiguration, "BuiltInCommand doesn't support the 'configuration' argument.");

                return new BuiltInCommand(commandName, args, builtInCommand.Command);
            }

            return CommandFactoryUsingResolver.CreateDotNet(commandName, args, framework, configuration);
        }
    }
}
