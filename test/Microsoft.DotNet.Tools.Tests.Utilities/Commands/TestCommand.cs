// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private List<Action<string>> _writeLines = new List<Action<string>>();

        private List<string> _cliGeneratedEnvironmentVariables = new List<string> { "MSBuildSDKsPath" };

        public TestCommand(string command)
        {
            _command = command;
#if NET451            
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#else
            _baseDirectory = AppContext.BaseDirectory;
#endif 
        }

        public virtual CommandResult Execute(string args = "")
        {
            var commandPath = _command;
            ResolveCommand(ref commandPath, ref args);

            Console.WriteLine($"Executing - {commandPath} {args} - {WorkingDirectoryInfo()}");

            var stdOut = new StreamForwarder();
            var stdErr = new StreamForwarder();

            AddWriteLine(Reporter.Output.WriteLine);

            stdOut.ForwardTo(writeLine: WriteLine);
            stdErr.ForwardTo(writeLine: WriteLine);

            return RunProcess(commandPath, args, stdOut, stdErr);
        }

        public virtual Task<CommandResult> ExecuteAsync(string args = "")
        {
            var commandPath = _command;
            ResolveCommand(ref commandPath, ref args);

            Console.WriteLine($"Executing - {commandPath} {args} - {WorkingDirectoryInfo()}");

            var stdOut = new StreamForwarder();
            var stdErr = new StreamForwarder();

            AddWriteLine(Reporter.Output.WriteLine);

            stdOut.ForwardTo(writeLine: WriteLine);
            stdErr.ForwardTo(writeLine: WriteLine);

            return RunProcessAsync(commandPath, args, stdOut, stdErr);
        }

        public virtual CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            var command = _command;
            ResolveCommand(ref command, ref args);
            var commandPath = Env.GetCommandPath(command, ".exe", ".cmd", "") ??
                Env.GetCommandPathFromRootPath(_baseDirectory, command, ".exe", ".cmd", "");

            Console.WriteLine($"Executing (Captured Output) - {commandPath} {args} - {WorkingDirectoryInfo()}");

            var stdOut = new StreamForwarder();
            var stdErr = new StreamForwarder();

            stdOut.ForwardTo(writeLine: WriteLine);
            stdErr.ForwardTo(writeLine: WriteLine);

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

        public void AddWriteLine(Action<string> writeLine)
        {
            _writeLines.Add(writeLine);
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
            Task taskOut = null;

            Task taskErr = null;
         
            CurrentProcess = CreateProcess(executable, args);

            CurrentProcess.Start();

            try
            {
                taskOut = stdOut.BeginRead(CurrentProcess.StandardOutput);
            }
            catch (System.InvalidOperationException e)
            {
                if (!e.Message.Equals("The collection has been marked as complete with regards to additions."))
                {
                    throw;
                }
            }

            try
            {
                taskErr = stdErr.BeginRead(CurrentProcess.StandardError);
            }
            catch (System.InvalidOperationException e)
            {
                if (!e.Message.Equals("The collection has been marked as complete with regards to additions."))
                {
                    throw;
                }
            }

            CurrentProcess.WaitForExit();

            var tasksToAwait = new List<Task>();

            if (taskOut != null)
            {
                tasksToAwait.Add(taskOut);
            }

            if (taskErr != null)
            {
                tasksToAwait.Add(taskErr);
            }

            if (tasksToAwait.Any())
            {
                try
                {
                    Task.WaitAll(tasksToAwait.ToArray());
                }
                catch (System.ObjectDisposedException e)
                {
                    taskErr = null;

                    taskOut = null;
                }
            }

            var result = new CommandResult(
                CurrentProcess.StartInfo,
                CurrentProcess.ExitCode,
                stdOut?.CapturedOutput ?? CurrentProcess.StandardOutput.ReadToEnd(),
                stdErr?.CapturedOutput ?? CurrentProcess.StandardError.ReadToEnd());

            return result;
        }

        private Task<CommandResult> RunProcessAsync(string executable, string args, StreamForwarder stdOut, StreamForwarder stdErr)
        {
            Task taskOut = null;

            Task taskErr = null;
         
            CurrentProcess = CreateProcess(executable, args);

            CurrentProcess.Start();

            try
            {
                taskOut = stdOut.BeginRead(CurrentProcess.StandardOutput);
            }
            catch (System.InvalidOperationException e)
            {
                if (!e.Message.Equals("The collection has been marked as complete with regards to additions."))
                {
                    throw;
                }
            }

            try
            {
                taskErr = stdErr.BeginRead(CurrentProcess.StandardError);
            }
            catch (System.InvalidOperationException e)
            {
                if (!e.Message.Equals("The collection has been marked as complete with regards to additions."))
                {
                    throw;
                }
            }

            var tcs = new TaskCompletionSource<CommandResult>();

            CurrentProcess.Exited += (sender, arg) =>
            {
                var tasksToAwait = new List<Task>();

                if (taskOut != null)
                {
                    tasksToAwait.Add(taskOut);
                }

                if (taskErr != null)
                {
                    tasksToAwait.Add(taskErr);
                }

                if (tasksToAwait.Any())
                {
                    try
                    {
                        Task.WaitAll(tasksToAwait.ToArray());
                    }
                    catch (System.ObjectDisposedException e)
                    {
                        taskErr = null;

                        taskOut = null;
                    }
                }
                
                var result = new CommandResult(
                                    CurrentProcess.StartInfo,
                                    CurrentProcess.ExitCode,
                                    stdOut?.CapturedOutput ?? CurrentProcess.StandardOutput.ReadToEnd(),
                                    stdErr?.CapturedOutput ?? CurrentProcess.StandardError.ReadToEnd());

                tcs.SetResult(result);
            };

            return tcs.Task;
        }

        private Process CreateProcess(string executable, string args)
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

            RemoveCliGeneratedEnvironmentVariables(psi);

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

            return process;
        }

        private void WriteLine(string line)
        {
            foreach (var writeLine in _writeLines)
            {
                writeLine(line);
            }
        }

        private string WorkingDirectoryInfo()
        {
            if (WorkingDirectory == null)
            { 
                return "";
            }

            return $" in pwd {WorkingDirectory}";
        }

        private void RemoveCliGeneratedEnvironmentVariables(ProcessStartInfo psi)
        {
            foreach (var name in _cliGeneratedEnvironmentVariables)
            {
#if NET451
                psi.EnvironmentVariables.Remove(name);
#else
                psi.Environment.Remove(name);
#endif
            }
        }
    }
}