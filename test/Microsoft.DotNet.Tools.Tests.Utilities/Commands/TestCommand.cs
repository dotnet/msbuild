// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;


namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class TestCommand
    {
        protected string _command;
        
        public TestCommand(string command)
        {
            _command = command;
        }

        public virtual CommandResult Execute(string args)
        {
            Console.WriteLine($"Executing - {_command} {args}");
            var commandResult = Command.Create(_command, args)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            return commandResult;
        }

        public virtual CommandResult ExecuteWithCapturedOutput(string args)
        {
            Console.WriteLine($"Executing (Captured Output) - {_command} {args}");
            var commandResult = Command.Create(_command, args)
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            return commandResult;
        }
    }
}
