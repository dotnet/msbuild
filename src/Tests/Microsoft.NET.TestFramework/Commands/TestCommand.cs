// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using System.Diagnostics;
using static Microsoft.DotNet.Cli.Utils.ExponentialRetry;

namespace Microsoft.NET.TestFramework.Commands
{
    public abstract class TestCommand
    {
        private Dictionary<string, string> _environment = new Dictionary<string, string>();
        private bool _doNotEscapeArguments;

        public ITestOutputHelper Log { get; }

        public string WorkingDirectory { get; set; }

        public List<string> Arguments { get; set; } = new List<string>();

        public List<string> EnvironmentToRemove { get; } = new List<string>();

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

        /// <summary>
        /// Instructs not to escape the arguments when launching command.
        /// This may be used to pass ready arguments line as single string argument.
        /// </summary>
        public TestCommand WithRawArguments()
        {
            _doNotEscapeArguments = true;
            return this;
        }

        public TestCommand WithCulture(string locale) => WithEnvironmentVariable(UILanguageOverride.DOTNET_CLI_UI_LANGUAGE, locale);

        public TestCommand WithTraceOutput()
        {
            WithEnvironmentVariable("DOTNET_CLI_VSTEST_TRACE", "1");
            return this;
        }

        private SdkCommandSpec CreateCommandSpec(IEnumerable<string> args)
        {
            var commandSpec = CreateCommand(args);
            foreach (var kvp in _environment)
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

        public ProcessStartInfo GetProcessStartInfo(params string[] args)
        {
            var commandSpec = CreateCommandSpec(args);

            var psi = commandSpec.ToProcessStartInfo();

            return psi;
        }

        public CommandResult Execute(params string[] args)
        {
            IEnumerable<string> enumerableArgs = args;
            return ExecuteWithRetry(
                    action: () => Execute(enumerableArgs),
                    shouldStopRetry: SuccessOrNotTransientRestoreError,
                    maxRetryCount: 3,
                    timer: () => Timer(Intervals),
                    taskDescription: "Run command while retry transient restore error")
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static bool SuccessOrNotTransientRestoreError(CommandResult result)
        {
            if (result.ExitCode == 0)
            {
                return true;
            }

            return !NuGetTransientErrorDetector.IsTransientError(result.StdOut);
        }

        public virtual CommandResult Execute(IEnumerable<string> args)
        { 
            var command = CreateCommandSpec(args)
                .ToCommand(_doNotEscapeArguments)
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
