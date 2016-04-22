// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// A Command that is capable of running in the current process.
    /// </summary>
    public class BuiltInCommand : ICommand
    {
        private readonly IEnumerable<string> _commandArgs;
        private readonly Func<string[], int> _builtInCommand;
        private readonly StreamForwarder _stdOut;
        private readonly StreamForwarder _stdErr;

        public string CommandName { get; }
        public string CommandArgs => string.Join(" ", _commandArgs);

        public BuiltInCommand(string commandName, IEnumerable<string> commandArgs, Func<string[], int> builtInCommand)
        {
            CommandName = commandName;
            _commandArgs = commandArgs;
            _builtInCommand = builtInCommand;

            _stdOut = new StreamForwarder();
            _stdErr = new StreamForwarder();
        }

        public CommandResult Execute()
        {
            TextWriter originalConsoleOut = Console.Out;
            TextWriter originalConsoleError = Console.Error;

            try
            {
                // redirecting the standard out and error so we can forward
                // the output to the caller
                using (BlockingMemoryStream outStream = new BlockingMemoryStream())
                using (BlockingMemoryStream errorStream = new BlockingMemoryStream())
                {
                    Console.SetOut(new StreamWriter(outStream) { AutoFlush = true });
                    Console.SetError(new StreamWriter(errorStream) { AutoFlush = true });

                    // Reset the Reporters to the new Console Out and Error.
                    Reporter.Reset();

                    var taskOut = _stdOut.BeginRead(new StreamReader(outStream));
                    var taskErr = _stdErr.BeginRead(new StreamReader(errorStream));

                    int exitCode = _builtInCommand(_commandArgs.ToArray());

                    outStream.DoneWriting();
                    errorStream.DoneWriting();

                    Task.WaitAll(taskOut, taskErr);

                    // fake out a ProcessStartInfo using the Muxer command name, since this is a built-in command
                    ProcessStartInfo startInfo = new ProcessStartInfo(new Muxer().MuxerPath, $"{CommandName} {CommandArgs}");
                    return new CommandResult(startInfo, exitCode, null, null);
                }
            }
            finally
            {
                Console.SetOut(originalConsoleOut);
                Console.SetError(originalConsoleError);

                Reporter.Reset();
            }
        }

        public ICommand OnOutputLine(Action<string> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _stdOut.ForwardTo(writeLine: handler);

            return this;
        }

        public ICommand OnErrorLine(Action<string> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _stdErr.ForwardTo(writeLine: handler);

            return this;
        }

        public CommandResolutionStrategy ResolutionStrategy
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ICommand CaptureStdErr()
        {
            throw new NotImplementedException();
        }

        public ICommand CaptureStdOut()
        {
            throw new NotImplementedException();
        }

        public ICommand EnvironmentVariable(string name, string value)
        {
            throw new NotImplementedException();
        }

        public ICommand ForwardStdErr(TextWriter to = null, bool onlyIfVerbose = false, bool ansiPassThrough = true)
        {
            throw new NotImplementedException();
        }

        public ICommand ForwardStdOut(TextWriter to = null, bool onlyIfVerbose = false, bool ansiPassThrough = true)
        {
            throw new NotImplementedException();
        }

        public ICommand WorkingDirectory(string projectDirectory)
        {
            throw new NotImplementedException();
        }
    }
}
