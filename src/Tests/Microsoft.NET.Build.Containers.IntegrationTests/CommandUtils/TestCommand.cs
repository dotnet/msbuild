// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Xunit.Abstractions;

namespace Microsoft.DotNet.CommandUtils
{
    internal abstract class TestCommand
    {
        private readonly ITestOutputHelper? _log;
        private bool _doNotEscapeArguments;

        protected TestCommand(ITestOutputHelper? log)
        {
            _log = log;
        }

        internal string? WorkingDirectory { get; set; }

        internal List<string> Arguments { get; set; } = new List<string>();

        internal List<string> EnvironmentToRemove { get; } = new List<string>();

        //  These only work via Execute(), not when using GetProcessStartInfo()
        internal Action<string>? CommandOutputHandler { get; set; }

        internal Action<Process>? ProcessStartedHandler { get; set; }

        protected Dictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();

        internal TestCommand WithEnvironmentVariable(string name, string value)
        {
            Environment[name] = value;
            return this;
        }

        internal TestCommand WithEnvironmentVariables(IReadOnlyDictionary<string, string>? variables)
        {
            if (variables != null)
            {
                foreach (KeyValuePair<string, string> pair in variables)
                {
                    Environment[pair.Key] = pair.Value;
                }
            }
            return this;
        }

        internal TestCommand WithWorkingDirectory(string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
            return this;
        }

        internal TestCommand WithRawArguments()
        {
            _doNotEscapeArguments = true;
            return this;
        }

        internal CommandResult Execute(params string[] args)
        {
            IEnumerable<string> enumerableArgs = args;
            return Execute(enumerableArgs);
        }

        internal virtual CommandResult Execute(IEnumerable<string> args)
        {
            Command command = CreateCommandSpec(args)
                .ToCommand(_doNotEscapeArguments)
                .CaptureStdOut()
                .CaptureStdErr();

            if (CommandOutputHandler != null)
            {
                command.OnOutputLine(CommandOutputHandler);
            }

            var result = command.Execute(ProcessStartedHandler);

            _log?.WriteLine($"> {result.StartInfo.FileName} {result.StartInfo.Arguments}");
            _log?.WriteLine(result.StdOut);

            if (!string.IsNullOrEmpty(result.StdErr))
            {
                _log?.WriteLine(string.Empty);
                _log?.WriteLine("StdErr:");
                _log?.WriteLine(result.StdErr);
            }

            if (result.ExitCode != 0)
            {
                _log?.WriteLine($"Exit Code: {result.ExitCode}");
            }

            return result;
        }

        private protected abstract SdkCommandSpec CreateCommand(IEnumerable<string> args);

        private SdkCommandSpec CreateCommandSpec(IEnumerable<string> args)
        {
            var commandSpec = CreateCommand(args);
            foreach (var kvp in Environment)
            {
                commandSpec.Environment[kvp.Key] = kvp.Value;
            }

            foreach (var envToRemove in EnvironmentToRemove)
            {
                commandSpec.EnvironmentToRemove.Add(envToRemove);
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
    }
}
