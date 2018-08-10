// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class TestCommand
    {
        private string _baseDirectory;

        private List<string> _cliGeneratedEnvironmentVariables = new List<string> { "MSBuildSDKsPath" };

        protected string _command;

        public Process CurrentProcess { get; private set; }

        public Dictionary<string, string> Environment { get; } = new Dictionary<string, string>();

        public event DataReceivedEventHandler ErrorDataReceived;

        public event DataReceivedEventHandler OutputDataReceived;

        public string WorkingDirectory { get; set; }

        public TestCommand(string command)
        {
            _command = command;

            _baseDirectory = GetBaseDirectory();
        }

        public void KillTree()
        {
            if (CurrentProcess == null)
            {
                throw new InvalidOperationException("No process is available to be killed");
            }

            CurrentProcess.KillTree();
        }

        public virtual CommandResult Execute(string args = "")
        {
            return Task.Run(async () => await ExecuteAsync(args)).Result;
        }

        public async virtual Task<CommandResult> ExecuteAsync(string args = "")
        {
            var resolvedCommand = _command;

            ResolveCommand(ref resolvedCommand, ref args);

            Console.WriteLine($"Executing - {resolvedCommand} {args} - {WorkingDirectoryInfo()}");
            
            return await ExecuteAsyncInternal(resolvedCommand, args);
        }

        public virtual CommandResult ExecuteWithCapturedOutput(string args = "")
        {
            var resolvedCommand = _command;

            ResolveCommand(ref resolvedCommand, ref args);

            Console.WriteLine($"Executing (Captured Output) - {resolvedCommand} {args} - {WorkingDirectoryInfo()}");

            return Task.Run(async () => await ExecuteAsyncInternal(resolvedCommand, args)).Result;
        }

        private async Task<CommandResult> ExecuteAsyncInternal(string executable, string args)
        {
            var stdOut = new List<String>();

            var stdErr = new List<String>();

            CurrentProcess = CreateProcess(executable, args); 

            CurrentProcess.ErrorDataReceived += (s, e) =>
            {
                stdErr.Add(e.Data);

                var handler = ErrorDataReceived;
                
                if (handler != null)
                {
                    handler(s, e);
                }
            };

            CurrentProcess.OutputDataReceived += (s, e) =>
            {
                stdOut.Add(e.Data);

                var handler = OutputDataReceived;
                
                if (handler != null)
                {
                    handler(s, e);
                }
            };
            
            var completionTask = CurrentProcess.StartAndWaitForExitAsync();

            CurrentProcess.BeginOutputReadLine();

            CurrentProcess.BeginErrorReadLine();

            await completionTask;

            CurrentProcess.WaitForExit();

            RemoveNullTerminator(stdOut);

            RemoveNullTerminator(stdErr);

            return new CommandResult(
                CurrentProcess.StartInfo,
                CurrentProcess.ExitCode,
                String.Join(System.Environment.NewLine, stdOut),
                String.Join(System.Environment.NewLine, stdErr));
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

            RemoveCliGeneratedEnvironmentVariablesFrom(psi);

            AddEnvironmentVariablesTo(psi);

            AddWorkingDirectoryTo(psi);

            var process = new Process
            {
                StartInfo = psi
            };

            process.EnableRaisingEvents = true;

            return process;
        }

        private string WorkingDirectoryInfo()
        {
            if (WorkingDirectory == null)
            { 
                return "";
            }

            return $" in pwd {WorkingDirectory}";
        }

        private void RemoveNullTerminator(List<string> strings)
        {
            var count = strings.Count;

            if (count < 1)
            {
                return;
            }

            if (strings[count - 1] == null)
            {
                strings.RemoveAt(count - 1);
            }
        }

        private string GetBaseDirectory()
        {
#if NET451
            return AppDomain.CurrentDomain.BaseDirectory;
#else
            return AppContext.BaseDirectory;
#endif
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

                executable = new Muxer().MuxerPath;
            }
            else if ( executable == "dotnet")
            {
                executable = new Muxer().MuxerPath;
            }
            else if (!Path.IsPathRooted(executable))
            {
                executable = Env.GetCommandPath(executable) ??
                             Env.GetCommandPathFromRootPath(_baseDirectory, executable);
            }
        }

        private void RemoveCliGeneratedEnvironmentVariablesFrom(ProcessStartInfo psi)
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

        private void AddEnvironmentVariablesTo(ProcessStartInfo psi)
        {
            foreach (var item in Environment)
            {
#if NET451
                psi.EnvironmentVariables[item.Key] = item.Value;
#else
                psi.Environment[item.Key] = item.Value;
#endif
            }

            //  Flow the TEST_PACKAGES environment variable to the child process
            psi.Environment["TEST_PACKAGES"] = System.Environment.GetEnvironmentVariable("TEST_PACKAGES");
        }

        private void AddWorkingDirectoryTo(ProcessStartInfo psi)
        {
            if (!string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                psi.WorkingDirectory = WorkingDirectory;
            }
        }
    }
}
