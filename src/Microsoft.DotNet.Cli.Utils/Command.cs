// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Command : ICommand
    {
        private readonly Process _process;
        private StreamForwarder _stdOut;
        private StreamForwarder _stdErr;

        private bool _running = false;

        private Command(CommandSpec commandSpec)
        {
            var psi = new ProcessStartInfo
            {
                FileName = commandSpec.Path,
                Arguments = commandSpec.Args,
                UseShellExecute = false
            };

            _process = new Process
            {
                StartInfo = psi
            };

            ResolutionStrategy = commandSpec.ResolutionStrategy;
        }

        public static Command CreateDotNet(
            string commandName, 
            IEnumerable<string> args, 
            NuGetFramework framework = null,  
            string configuration = Constants.DefaultConfiguration)
        {
            return Create("dotnet", 
                new[] { commandName }.Concat(args), 
                framework, 
                configuration: configuration);
        }

        /// <summary>
        /// Create a command with the specified arg array. Args will be 
        /// escaped properly to ensure that exactly the strings in this
        /// array will be present in the corresponding argument array
        /// in the command's process.
        /// </summary>
        public static Command Create(
            string commandName, 
            IEnumerable<string> args, 
            NuGetFramework framework = null, 
            string configuration = Constants.DefaultConfiguration,
            string outputPath = null,
            string applicationName  = null)
        {
            var commandSpec = CommandResolver.TryResolveCommandSpec(commandName, 
                args, 
                framework, 
                configuration: configuration,
                outputPath: outputPath,
                applicationName: applicationName);

            if (commandSpec == null)
            {
                throw new CommandUnknownException(commandName);
            }

            var command = new Command(commandSpec);

            return command;
        }

        public static Command Create(CommandSpec commandSpec)
        {
            return new Command(commandSpec);
        }

        public static Command CreateForScript(
            string commandName,
            IEnumerable<string> args,
            Project project,
            string[] inferredExtensionList)
        {
            var commandSpec = CommandResolver.TryResolveScriptCommandSpec(commandName,
                args,
                project,
                inferredExtensionList);

            if (commandSpec == null)
            {
                throw new CommandUnknownException(commandName);
            }

            var command = new Command(commandSpec);

            return command;
        }

        public CommandResult Execute()
        {

            Reporter.Verbose.WriteLine($"Running {_process.StartInfo.FileName} {_process.StartInfo.Arguments}");

            ThrowIfRunning();
            _running = true;

            _process.EnableRaisingEvents = true;

#if DEBUG
            var sw = Stopwatch.StartNew();
            Reporter.Verbose.WriteLine($"> {FormatProcessInfo(_process.StartInfo)}".White());
#endif
            using (PerfTrace.Current.CaptureTiming($"{Path.GetFileNameWithoutExtension(_process.StartInfo.FileName)} {_process.StartInfo.Arguments}"))
            {
                _process.Start();

                Reporter.Verbose.WriteLine($"Process ID: {_process.Id}");

                var taskOut = _stdOut?.BeginRead(_process.StandardOutput);
                var taskErr = _stdErr?.BeginRead(_process.StandardError);
                _process.WaitForExit();

                taskOut?.Wait();
                taskErr?.Wait();
            }

            var exitCode = _process.ExitCode;

#if DEBUG
            var message = $"< {FormatProcessInfo(_process.StartInfo)} exited with {exitCode} in {sw.ElapsedMilliseconds} ms.";
            if (exitCode == 0)
            {
                Reporter.Verbose.WriteLine(message.Green());
            }
            else
            {
                Reporter.Verbose.WriteLine(message.Red().Bold());
            }
#endif

            return new CommandResult(
                this._process.StartInfo,
                exitCode,
                _stdOut?.CapturedOutput,
                _stdErr?.CapturedOutput);
        }

        public ICommand WorkingDirectory(string projectDirectory)
        {
            _process.StartInfo.WorkingDirectory = projectDirectory;
            return this;
        }

        public ICommand EnvironmentVariable(string name, string value)
        {
#if NET451
            _process.StartInfo.EnvironmentVariables[name] = value;
#else
            _process.StartInfo.Environment[name] = value;
#endif
            return this;
        }

        public ICommand CaptureStdOut()
        {
            ThrowIfRunning();
            EnsureStdOut();
            _stdOut.Capture();
            return this;
        }

        public ICommand CaptureStdErr()
        {
            ThrowIfRunning();
            EnsureStdErr();
            _stdErr.Capture();
            return this;
        }

        public ICommand ForwardStdOut(TextWriter to = null, bool onlyIfVerbose = false, bool ansiPassThrough = true)
        {
            ThrowIfRunning();
            if (!onlyIfVerbose || CommandContext.IsVerbose())
            {
                EnsureStdOut();

                if (to == null)
                {
                    _stdOut.ForwardTo(writeLine: Reporter.Output.WriteLine);
                    EnvironmentVariable(CommandContext.Variables.AnsiPassThru, ansiPassThrough.ToString());
                }
                else
                {
                    _stdOut.ForwardTo(writeLine: to.WriteLine);
                }
            }
            return this;
        }

        public ICommand ForwardStdErr(TextWriter to = null, bool onlyIfVerbose = false, bool ansiPassThrough = true)
        {
            ThrowIfRunning();
            if (!onlyIfVerbose || CommandContext.IsVerbose())
            {
                EnsureStdErr();

                if (to == null)
                {
                    _stdErr.ForwardTo(writeLine: Reporter.Error.WriteLine);
                    EnvironmentVariable(CommandContext.Variables.AnsiPassThru, ansiPassThrough.ToString());
                }
                else
                {
                    _stdErr.ForwardTo(writeLine: to.WriteLine);
                }
            }
            return this;
        }

        public ICommand OnOutputLine(Action<string> handler)
        {
            ThrowIfRunning();
            EnsureStdOut();

            _stdOut.ForwardTo(writeLine: handler);
            return this;
        }

        public ICommand OnErrorLine(Action<string> handler)
        {
            ThrowIfRunning();
            EnsureStdErr();

            _stdErr.ForwardTo(writeLine: handler);
            return this;
        }

        public CommandResolutionStrategy ResolutionStrategy { get; }

        public string CommandName => _process.StartInfo.FileName;

        public string CommandArgs => _process.StartInfo.Arguments;

        private string FormatProcessInfo(ProcessStartInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.Arguments))
            {
                return info.FileName;
            }

            return info.FileName + " " + info.Arguments;
        }

        private void EnsureStdOut()
        {
            _stdOut = _stdOut ?? new StreamForwarder();
            _process.StartInfo.RedirectStandardOutput = true;
        }

        private void EnsureStdErr()
        {
            _stdErr = _stdErr ?? new StreamForwarder();
            _process.StartInfo.RedirectStandardError = true;
        }

        private void ThrowIfRunning([CallerMemberName] string memberName = null)
        {
            if (_running)
            {
                throw new InvalidOperationException($"Unable to invoke {memberName} after the command has been run");
            }
        }
    }
}
