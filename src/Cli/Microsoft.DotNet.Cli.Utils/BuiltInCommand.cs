// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// A Command that is capable of running in the current process.
    /// </summary>
    public class BuiltInCommand : ICommand
    {
        private readonly IEnumerable<string> _commandArgs;
        private readonly Func<string[], int> _builtInCommand;
        private readonly IBuiltInCommandEnvironment _environment;
        private readonly StreamForwarder _stdOut;
        private readonly StreamForwarder _stdErr;
        private string _workingDirectory;

        public string CommandName { get; }
        public string CommandArgs => string.Join(" ", _commandArgs);

        public BuiltInCommand(string commandName, IEnumerable<string> commandArgs, Func<string[], int> builtInCommand)
            : this(commandName, commandArgs, builtInCommand, new BuiltInCommandEnvironment())
        {
        }

        internal BuiltInCommand(string commandName, IEnumerable<string> commandArgs, Func<string[], int> builtInCommand, IBuiltInCommandEnvironment environment)
        {
            CommandName = commandName;
            _commandArgs = commandArgs;
            _builtInCommand = builtInCommand;
            _environment = environment;

            _stdOut = new StreamForwarder();
            _stdErr = new StreamForwarder();
        }

        public CommandResult Execute()
        {
            TextWriter originalConsoleOut = _environment.GetConsoleOut();
            TextWriter originalConsoleError = _environment.GetConsoleError();
            string originalWorkingDirectory = _environment.GetWorkingDirectory();

            try
            {
                // redirecting the standard out and error so we can forward
                // the output to the caller
                using (BlockingMemoryStream outStream = new BlockingMemoryStream())
                using (BlockingMemoryStream errorStream = new BlockingMemoryStream())
                {
                    _environment.SetConsoleOut(new StreamWriter(outStream) { AutoFlush = true });
                    _environment.SetConsoleError(new StreamWriter(errorStream) { AutoFlush = true });

                    // Reset the Reporters to the new Console Out and Error.
                    Reporter.Reset();

                    if (!string.IsNullOrEmpty(_workingDirectory))
                    {
                        _environment.SetWorkingDirectory(_workingDirectory);
                    }

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
                _environment.SetConsoleOut(originalConsoleOut);
                _environment.SetConsoleError(originalConsoleError);
                _environment.SetWorkingDirectory(originalWorkingDirectory);

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

        public ICommand WorkingDirectory(string workingDirectory)
        {
            _workingDirectory = workingDirectory;

            return this;
        }

        private class BuiltInCommandEnvironment : IBuiltInCommandEnvironment
        {
            public TextWriter GetConsoleOut()
            {
                return Console.Out;
            }

            public void SetConsoleOut(TextWriter newOut)
            {
                Console.SetOut(newOut);
            }

            public TextWriter GetConsoleError()
            {
                return Console.Error;
            }

            public void SetConsoleError(TextWriter newError)
            {
                Console.SetError(newError);
            }

            public string GetWorkingDirectory()
            {
                return Directory.GetCurrentDirectory();
            }

            public void SetWorkingDirectory(string path)
            {
                Directory.SetCurrentDirectory(path);
            }
        }

        public ICommand CaptureStdErr()
        {
            _stdErr.Capture();

            return this;
        }

        public ICommand CaptureStdOut()
        {
            _stdOut.Capture();
            
            return this;
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
        public ICommand SetCommandArgs(string commandArgs)
        {
            throw new NotImplementedException();
        }
    }
}
