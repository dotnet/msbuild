// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System.Collections.Generic;
using Xunit.Abstractions;
using System.Diagnostics;
using System.Linq;
using System;

namespace Microsoft.NET.TestFramework.Commands
{
    public abstract class TestCommand
    {
        private Dictionary<string, string> _environment = new Dictionary<string, string>();

        public ITestOutputHelper Log { get; }

        public string WorkingDirectory { get; set; }

        public List<string> Arguments { get; set; } = new List<string>();

        //  These only work via Execute(), not when using GetProcessStartInfo()
        public Action<string> CommandOutputHandler { get; set; }
        public Action<Process> ProcessStartedHandler { get; set; }

        protected TestCommand(ITestOutputHelper log)
        {
            Log = log;
        }

        protected abstract SdkCommandSpec CreateCommand(IEnumerable<string> args);

        public TestCommand WithEnvironmentVariable(string name, string value)
        {
            _environment[name] = value;
            return this;
        }

        public TestCommand WithWorkingDirectory(string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
            return this;
        }

        private SdkCommandSpec CreateCommandSpec(IEnumerable<string> args)
        {
            var commandSpec = CreateCommand(args);
            foreach (var kvp in _environment)
            {
                commandSpec.Environment[kvp.Key] = kvp.Value;
            }

            if (WorkingDirectory != null)
            {
                commandSpec.WorkingDirectory = WorkingDirectory;
            }

            if (Arguments.Any())
            {
                commandSpec.Arguments = Arguments.Concat(commandSpec.Arguments).ToList();
            }

            return commandSpec;
        }

        public ProcessStartInfo GetProcessStartInfo(params string[] args)
        {
            var commandSpec = CreateCommandSpec(args);

            return commandSpec.ToProcessStartInfo();
        }

        public CommandResult Execute(params string[] args)
        {
            IEnumerable<string> enumerableArgs = args;
            return Execute(enumerableArgs);
        }

        public virtual CommandResult Execute(IEnumerable<string> args)
        { 
            var command = CreateCommandSpec(args)
                .ToCommand()
                .CaptureStdOut()
                .CaptureStdErr();

            if (CommandOutputHandler != null)
            {
                command.OnOutputLine(CommandOutputHandler);
            }

            var result = ((Command)command).Execute(ProcessStartedHandler);

            Log.WriteLine($"> {result.StartInfo.FileName} {result.StartInfo.Arguments}");
            Log.WriteLine(result.StdOut);

            if (!string.IsNullOrEmpty(result.StdErr))
            {
                Log.WriteLine("");
                Log.WriteLine("StdErr:");
                Log.WriteLine(result.StdErr);
            }

            if (result.ExitCode != 0)
            {
                Log.WriteLine($"Exit Code: {result.ExitCode}");
            }

            return result;
        }
    }
}
