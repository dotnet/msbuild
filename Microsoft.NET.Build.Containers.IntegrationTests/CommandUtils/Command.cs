// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.CommandUtils
{
    internal sealed class Command
    {
        private readonly Process _process;

        private readonly bool _trimTrailingNewlines;

        private StreamForwarder? _stdOut;
        private StreamForwarder? _stdErr;
        private bool _running;

        public Command(Process process, bool trimtrailingNewlines = false)
        {
            _trimTrailingNewlines = trimtrailingNewlines;
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public string CommandName => _process.StartInfo.FileName;

        public string CommandArgs => _process.StartInfo.Arguments;

        public CommandResult Execute()
        {
            return Execute(_ => { });
        }

        public CommandResult Execute(Action<Process>? processStarted)
        {
            Console.WriteLine($"Running {_process.StartInfo.FileName} {_process.StartInfo.Arguments}");
            ThrowIfRunning();

            _running = true;

            _process.EnableRaisingEvents = true;

#if DEBUG
            var sw = Stopwatch.StartNew();

            Console.WriteLine($"> {FormatProcessInfo(_process.StartInfo)}");
#endif
            using (var reaper = new ProcessReaper(_process))
            {
                _process.Start();
                processStarted?.Invoke(_process);
                reaper.NotifyProcessStarted();

                Console.WriteLine($"Process ID: {_process.Id}");

                var taskOut = _stdOut?.BeginRead(_process.StandardOutput);
                var taskErr = _stdErr?.BeginRead(_process.StandardError);
                _process.WaitForExit();

                taskOut?.Wait();
                taskErr?.Wait();
            }

            var exitCode = _process.ExitCode;

#if DEBUG
            var message = string.Format($"&lt; {FormatProcessInfo(_process.StartInfo)} exited with {exitCode} in {sw.ElapsedMilliseconds} ms");
            if (exitCode == 0)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.WriteLine(message);
            }
#endif

            return new CommandResult(
                _process.StartInfo,
                exitCode,
                _stdOut?.CapturedOutput,
                _stdErr?.CapturedOutput);
        }

        public Command WorkingDirectory(string projectDirectory)
        {
            _process.StartInfo.WorkingDirectory = projectDirectory;
            return this;
        }

        public Command EnvironmentVariable(string name, string value)
        {
            _process.StartInfo.Environment[name] = value;
            return this;
        }

        public Command CaptureStdOut()
        {
            ThrowIfRunning();
            EnsureStdOut();
            _stdOut!.Capture(_trimTrailingNewlines);
            return this;
        }

        public Command CaptureStdErr()
        {
            ThrowIfRunning();
            EnsureStdErr();
            _stdErr!.Capture(_trimTrailingNewlines);
            return this;
        }

        public Command ForwardStdOut(TextWriter? to = null)
        {
            ThrowIfRunning();
            EnsureStdOut();

            if (to == null)
            {
                _stdOut!.ForwardTo(writeLine: Console.Out.WriteLine);
            }
            else
            {
                _stdOut!.ForwardTo(writeLine: to.WriteLine);
            }

            return this;
        }

        public Command ForwardStdErr(TextWriter? to = null)
        {
            ThrowIfRunning();

            EnsureStdErr();

            if (to == null)
            {
                _stdErr!.ForwardTo(writeLine: Console.Error.WriteLine);
            }
            else
            {
                _stdErr!.ForwardTo(writeLine: to.WriteLine);
            }

            return this;
        }

        public Command OnOutputLine(Action<string> handler)
        {
            ThrowIfRunning();
            EnsureStdOut();

            _stdOut!.ForwardTo(writeLine: handler);
            return this;
        }

        public Command OnErrorLine(Action<string> handler)
        {
            ThrowIfRunning();
            EnsureStdErr();

            _stdErr!.ForwardTo(writeLine: handler);
            return this;
        }

        public Command SetCommandArgs(string commandArgs)
        {
            _process.StartInfo.Arguments = commandArgs;
            return this;
        }

        private static string FormatProcessInfo(ProcessStartInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.Arguments))
            {
                return info.FileName;
            }

            return info.FileName + " " + info.Arguments;
        }

        private void EnsureStdOut()
        {
            _stdOut ??= new StreamForwarder();
            _process.StartInfo.RedirectStandardOutput = true;
        }

        private void EnsureStdErr()
        {
            _stdErr ??= new StreamForwarder();
            _process.StartInfo.RedirectStandardError = true;
        }

        private void ThrowIfRunning([CallerMemberName] string? memberName = null)
        {
            if (_running)
            {
                throw new InvalidOperationException($"Unable to invoke {memberName} after the command has been run.");
            }
        }
    }
}
