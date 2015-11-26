// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Command
    {
        private Process _process;

        private StringWriter _stdOutCapture;
        private StringWriter _stdErrCapture;

        private Action<string> _stdOutForward;
        private Action<string> _stdErrForward;

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

            _process = new Process()
            {
                StartInfo = psi
            };
        }

        public static Command Create(string executable, IEnumerable<string> args)
        {
            return Create(executable, string.Join(" ", args));
        }

        public static Command Create(string executable, string args)
        {
            ResolveExecutablePath(ref executable, ref args);

            return new Command(executable, args);
        }

        private static void ResolveExecutablePath(ref string executable, ref string args)
        {
            foreach (string suffix in Constants.RunnableSuffixes)
            {
                var fullExecutable = Path.GetFullPath(Path.Combine(
                                        AppContext.BaseDirectory, executable + suffix));

                if (File.Exists(fullExecutable))
                {
                    executable = fullExecutable;

                    // In priority order we've found the best runnable extension, so break.
                    break;
                }
            }

            // On Windows, we want to avoid using "cmd" if possible (it mangles the colors, and a bunch of other things)
            // So, do a quick path search to see if we can just directly invoke it
            var useCmd = ShouldUseCmd(executable);

            if (useCmd)
            {
                var comSpec = Environment.GetEnvironmentVariable("ComSpec");

                // cmd doesn't like "foo.exe ", so we need to ensure that if
                // args is empty, we just run "foo.exe"
                if (!string.IsNullOrEmpty(args))
                {
                    executable = (executable + " " + args).Replace("\"", "\\\"");
                }
                args = $"/C \"{executable}\"";
                executable = comSpec;
            }
        }

        private static bool ShouldUseCmd(string executable)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var extension = Path.GetExtension(executable);
                if (!string.IsNullOrEmpty(extension))
                {
                    return !string.Equals(extension, ".exe", StringComparison.Ordinal);
                }
                else if (executable.Contains(Path.DirectorySeparatorChar))
                {
                    // It's a relative path without an extension
                    if (File.Exists(executable + ".exe"))
                    {
                        // It refers to an exe!
                        return false;
                    }
                }
                else
                {
                    // Search the path to see if we can find it 
                    foreach (var path in Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator))
                    {
                        var candidate = Path.Combine(path, executable + ".exe");
                        if (File.Exists(candidate))
                        {
                            // We found an exe!
                            return false;
                        }
                    }
                }

                // It's a non-exe :(
                return true;
            }

            // Non-windows never uses cmd
            return false;
        }

        public CommandResult Execute()
        {
            Reporter.Verbose.WriteLine($"Running {_process.StartInfo.FileName} {_process.StartInfo.Arguments}");

            ThrowIfRunning();
            _running = true;

            _process.OutputDataReceived += (sender, args) =>
            {
                ProcessData(args.Data, _stdOutCapture, _stdOutForward, _stdOutHandler);
            };

            _process.ErrorDataReceived += (sender, args) =>
            {
                ProcessData(args.Data, _stdErrCapture, _stdErrForward, _stdErrHandler);
            };

            _process.EnableRaisingEvents = true;

#if DEBUG
            var sw = Stopwatch.StartNew();
            Reporter.Verbose.WriteLine($"> {FormatProcessInfo(_process.StartInfo)}".White());
#endif
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _process.WaitForExit();

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
                exitCode,
                _stdOutCapture?.GetStringBuilder()?.ToString(),
                _stdErrCapture?.GetStringBuilder()?.ToString());
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
            _stdOutCapture = new StringWriter();
            return this;
        }

        public Command CaptureStdErr()
        {
            ThrowIfRunning();
            _stdErrCapture = new StringWriter();
            return this;
        }

        public Command ForwardStdOut(TextWriter to = null, bool onlyIfVerbose = false)
        {
            ThrowIfRunning();
            if (!onlyIfVerbose || CommandContext.IsVerbose())
            {
                if (to == null)
                {
                    _stdOutForward = Reporter.Output.WriteLine;
                }
                else
                {
                    _stdOutForward = to.WriteLine;
                }
            }
            return this;
        }

        public Command ForwardStdErr(TextWriter to = null, bool onlyIfVerbose = false)
        {
            ThrowIfRunning();
            if (!onlyIfVerbose || CommandContext.IsVerbose())
            {
                if (to == null)
                {
                    _stdErrForward = Reporter.Error.WriteLine;
                }
                else
                {
                    _stdErrForward = to.WriteLine;
                }
            }
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

        private string FormatProcessInfo(ProcessStartInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.Arguments))
            {
                return info.FileName;
            }

            return info.FileName + " " + info.Arguments;
        }

        private void ThrowIfRunning([CallerMemberName] string memberName = null)
        {
            if (_running)
            {
                throw new InvalidOperationException($"Unable to invoke {memberName} after the command has been run");
            }
        }

        private void ProcessData(string data, StringWriter capture, Action<string> forward, Action<string> handler)
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
                forward(data);
            }

            if (handler != null)
            {
                handler(data);
            }
        }
    }
}
