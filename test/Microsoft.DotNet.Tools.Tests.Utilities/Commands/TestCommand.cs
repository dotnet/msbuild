// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Diagnostics;
using System.IO;


namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class TestCommand
    {
        protected string _command;
        
        public TestCommand(string command)
        {
            _command = command;
        }

        public virtual CommandResult Execute(string args = "")
        {
            var commandPath = _command;
            if (!Path.IsPathRooted(_command))
            {
                _command = Env.GetCommandPath(_command) ??
                           Env.GetCommandPathFromAppBase(AppContext.BaseDirectory, _command);
            }

            Console.WriteLine($"Executing - {_command} {args}");

            var stdOut = new StreamForwarder();
            var stdErr = new StreamForwarder();

            stdOut.ForwardTo(write: Reporter.Output.Write, writeLine: Reporter.Output.WriteLine);
            stdErr.ForwardTo(write: Reporter.Error.Write, writeLine: Reporter.Output.WriteLine);

            return RunProcess(commandPath, args, stdOut, stdErr);
        }

        public virtual CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            Console.WriteLine($"Executing (Captured Output) - {_command} {args}");

            var commandPath = Env.GetCommandPath(_command, ".exe", ".cmd", "") ??
                Env.GetCommandPathFromAppBase(AppContext.BaseDirectory, _command, ".exe", ".cmd", "");
                
            var stdOut = new StreamForwarder();
            var stdErr = new StreamForwarder();

            stdOut.Capture();
            stdErr.Capture();

            return RunProcess(commandPath, args, stdOut, stdErr);
        }

        private CommandResult RunProcess(string executable, string args, StreamForwarder stdOut, StreamForwarder stdErr)
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var process = new Process
            {
                StartInfo = psi
            };

            process.EnableRaisingEvents = true;
            process.Start();

            var threadOut = stdOut.BeginRead(process.StandardOutput);
            var threadErr = stdErr.BeginRead(process.StandardError);

            process.WaitForExit();
            threadOut.Join();
            threadErr.Join();

            var result = new CommandResult(
                process.ExitCode, 
                stdOut.GetCapturedOutput(), 
                stdErr.GetCapturedOutput());

            return result;
        }
    }
}
