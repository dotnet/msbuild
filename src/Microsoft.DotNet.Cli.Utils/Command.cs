using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Command
    {
        private static readonly string[] WindowsExecutableExtensions = new[] { "exe", "cmd", "bat" };
        
        private TaskCompletionSource<int> _processTcs;
        private Process _process;

        private StringWriter _stdOutCapture;
        private StringWriter _stdErrCapture;

        private TextWriter _stdOutForward;
        private TextWriter _stdErrForward;

        private Action<string> _stdOutHandler;
        private Action<string> _stdErrHandler;

        private bool _running = false;

        private Command(string executable, string args)
        {
            // Set the things we need
            var psi = new ProcessStartInfo()
            {
                FileName = executable,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            _processTcs = new TaskCompletionSource<int>();

            _process = new Process()
            {
                StartInfo = psi
            };
        }

        public static Command Create(string executable, IEnumerable<string> args)
        {
            return Create(executable, args.Any() ? string.Join(" ", args) : string.Empty);
        }

        public static Command Create(string executable, string args)
        {
            var comSpec = Environment.GetEnvironmentVariable("ComSpec");
            if (!string.IsNullOrEmpty(comSpec))
            {
                // cmd doesn't like "foo.exe ", so we need to ensure that if
                // args is empty, we just run "foo.exe"
                if (!string.IsNullOrEmpty(args))
                {
                    args = " " + args;
                }
                var cmd = executable + args;
                cmd = cmd.Replace("\"", "\\\"");
                args = $"/C \"{executable}{args}\"";
                executable = comSpec;
            }
            else
            {
                // Temporary, we're doing this so that redirecting the output works
                args = $"bash -c \"{executable} {args.Replace("\"", "\\\"")}\"";
                executable = "/usr/bin/env";
            }

            return new Command(executable, args);
        }

        public async Task<CommandResult> RunAsync()
        {
            ThrowIfRunning();
            _running = true;

            _process.OutputDataReceived += (sender, args) =>
                ProcessData(args.Data, _stdOutCapture, _stdOutForward, _stdOutHandler);

            _process.ErrorDataReceived += (sender, args) =>
                ProcessData(args.Data, _stdErrCapture, _stdErrForward, _stdErrHandler);

            _process.EnableRaisingEvents = true;

            _process.Exited += (sender, _) =>
                _processTcs.SetResult(_process.ExitCode);

#if DEBUG
            Console.WriteLine($"> {_process.StartInfo.FileName} {_process.StartInfo.Arguments}");
#endif
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            var exitCode = await _processTcs.Task;

            return new CommandResult(
                exitCode,
                _stdOutCapture?.GetStringBuilder()?.ToString(),
                _stdErrCapture?.GetStringBuilder()?.ToString());
        }

        public Command CaptureStdOut()
        {
            ThrowIfRunning();
            _stdOutCapture = new StringWriter();
            return this;
        }

        public Command CaptureStdErr()
        {
            ThrowIfRunning();
            _stdErrCapture = new StringWriter();
            return this;
        }

        public Command ForwardStdOut(TextWriter to = null)
        {
            ThrowIfRunning();
            _stdOutForward = to ?? Console.Out;
            return this;
        }

        public Command ForwardStdErr(TextWriter to = null)
        {
            ThrowIfRunning();
            _stdErrForward = to ?? Console.Error;
            return this;
        }

        public Command OnOutputLine(Action<string> handler)
        {
            ThrowIfRunning();
            if (_stdOutHandler != null)
            {
                throw new InvalidOperationException("Already handling stdout!");
            }
            _stdOutHandler = handler;
            return this;
        }

        public Command OnErrorLine(Action<string> handler)
        {
            ThrowIfRunning();
            if (_stdErrHandler != null)
            {
                throw new InvalidOperationException("Already handling stderr!");
            }
            _stdErrHandler = handler;
            return this;
        }

        private void ThrowIfRunning([CallerMemberName] string memberName = null)
        {
            if (_running)
            {
                throw new InvalidOperationException($"Unable to invoke {memberName} after the command has been run");
            }
        }

        private void ProcessData(string data, StringWriter capture, TextWriter forward, Action<string> handler)
        {
            if (data == null)
            {
                return;
            }

            if (capture != null)
            {
                capture.WriteLine(data);
            }

            if (forward != null)
            {
                forward.WriteLine(data);
            }

            if (handler != null)
            {
                handler(data);
            }
        }
    }
}
