// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli
{
    public class DotNetCommandFactory : ICommandFactory
    {
        public ICommand Create(
        	string commandName, 
        	IEnumerable<string> args, 
        	NuGetFramework framework = null, 
        	string configuration = Constants.DefaultConfiguration)
        {
            Func<string[], int> builtInCommand;
            if (Program.TryGetBuiltInCommand(commandName, out builtInCommand))
            {
                Debug.Assert(framework == null, "BuiltInCommand doesn't support the 'framework' argument.");
                Debug.Assert(configuration == Constants.DefaultConfiguration, "BuiltInCommand doesn't support the 'configuration' argument.");

                return new BuiltInCommand(commandName, args, builtInCommand);
            }

            return Command.CreateDotNet(commandName, args, framework, configuration);
        }
    }
}
