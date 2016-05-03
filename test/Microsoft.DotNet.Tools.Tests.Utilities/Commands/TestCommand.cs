// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class TestCommand
    {
        protected string _command;

        private string _baseDirectory;

        public string WorkingDirectory { get; set; }

        public Process CurrentProcess { get; set; }

        public Dictionary<string, string> Environment { get; } = new Dictionary<string, string>();

        public TestCommand(string command)
        {
            _command = command;
#if NET451            
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#else
            _baseDirectory = AppContext.BaseDirectory;
#endif 
        }

        public TestCommand WithWorkingDirectory(string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
            return this;
        }

        public virtual CommandResult Execute(string args = "")
        {
            var commandPath = _command;
            ResolveCommand(ref commandPath, ref args);

            Console.WriteLine($"Executing - {commandPath} {args}");

            var stdOut = new StreamForwarder();
            var stdErr = new StreamForwarder();

            stdOut.ForwardTo(writeLine: Reporter.Output.WriteLine);
            stdErr.ForwardTo(writeLine: Reporter.Output.WriteLine);

            return RunProcess(commandPath, args, stdOut, stdErr);
        }

        public virtual Task<CommandResult> ExecuteAsync(string args = "")
        {
            var commandPath = _command;
            ResolveCommand(ref commandPath, ref args);

            Console.WriteLine($"Executing - {commandPath} {args}");

            var stdOut = new StreamForwarder();
            var stdErr = new StreamForwarder();

            stdOut.ForwardTo(writeLine: Reporter.Output.WriteLine);
            stdErr.ForwardTo(writeLine: Reporter.Output.WriteLine);

            return RunProcessAsync(commandPath, args, stdOut, stdErr);
        }

        public virtual CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            var command = _command;
            ResolveCommand(ref command, ref args);
            var commandPath = Env.GetCommandPath(command, ".exe", ".cmd", "") ??
                Env.GetCommandPathFromRootPath(_baseDirectory, command, ".exe", ".cmd", "");

            Console.WriteLine($"Executing (Captured Output) - {commandPath} {args}");

            var stdOut = new StreamForwarder();
            var stdErr = new StreamForwarder();

            stdOut.Capture();
            stdErr.Capture();

            return RunProcess(commandPath, args, stdOut, stdErr);
        }

        public void KillTree()
        {
            if (CurrentProcess == null)
            {
                throw new InvalidOperationException("No process is available to be killed");
            }

            CurrentProcess.KillTree();
        }

        private void ResolveCommand(ref string executable, ref string args)
        {
            if (executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var newArgs = ArgumentEscaper.EscapeSingleArg(executable);
                if (!string.IsNullOrEmpty(args))
                {
                    newArgs += " " + args;
                }
                args = newArgs;
                executable = "dotnet";
            }

            if (!Path.IsPathRooted(executable))
            {
                executable = Env.GetCommandPath(executable) ??
                           Env.GetCommandPathFromRootPath(_baseDirectory, executable);
            }
        }

        private CommandResult RunProcess(string executable, string args, StreamForwarder stdOut, StreamForwarder stdErr)
        {
            CurrentProcess = StartProcess(executable, args);
            var taskOut = stdOut.BeginRead(CurrentProcess.StandardOutput);
            var taskErr = stdErr.BeginRead(CurrentProcess.StandardError);

            CurrentProcess.WaitForExit();
            Task.WaitAll(taskOut, taskErr);

            var result = new CommandResult(
                CurrentProcess.StartInfo,
                CurrentProcess.ExitCode,
                stdOut.CapturedOutput,
                stdErr.CapturedOutput);

            return result;
        }

        private Task<CommandResult> RunProcessAsync(string executable, string args, StreamForwarder stdOut, StreamForwarder stdErr)
        {
            CurrentProcess = StartProcess(executable, args);
            var taskOut = stdOut.BeginRead(CurrentProcess.StandardOutput);
            var taskErr = stdErr.BeginRead(CurrentProcess.StandardError);

            var tcs = new TaskCompletionSource<CommandResult>();
            CurrentProcess.Exited += (sender, arg) =>
            {
                Task.WaitAll(taskOut, taskErr);
                var result = new CommandResult(
                                    CurrentProcess.StartInfo,
                                    CurrentProcess.ExitCode,
                                    stdOut.CapturedOutput,
                                    stdErr.CapturedOutput);
                tcs.SetResult(result);
            };

            return tcs.Task;
        }

        private Process StartProcess(string executable, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false
            };

            foreach (var item in Environment)
            {
#if NET451
                psi.EnvironmentVariables[item.Key] = item.Value;
#else
                psi.Environment[item.Key] = item.Value;
#endif
            }

            if (!string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                psi.WorkingDirectory = WorkingDirectory;
            }

            var process = new Process
            {
                StartInfo = psi
            };

            process.EnableRaisingEvents = true;
            process.Start();
            return process;
        }
        
        public TestCommand WithEnvironmentVariable(string name, string value)
        {
            Environment.Add(name, value);
            
            return this;
        }
    }
}
