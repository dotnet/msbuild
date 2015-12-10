// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Command
    {
        private readonly Process _process;
        private readonly StreamForwarder _stdOut;
        private readonly StreamForwarder _stdErr;

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

            _stdOut = new StreamForwarder();
            _stdErr = new StreamForwarder();
        }

        public static Command Create(string executable, IEnumerable<string> args, NuGetFramework framework = null)
        {
            return Create(executable, string.Join(" ", args), framework);
        }

        public static Command Create(string executable, string args, NuGetFramework framework = null)
        {
            ResolveExecutablePath(ref executable, ref args, framework);

            return new Command(executable, args);
        }

        private static void ResolveExecutablePath(ref string executable, ref string args, NuGetFramework framework = null)
        {
            executable = 
                ResolveExecutablePathFromProject(executable, framework) ??
                ResolveExecutableFromPath(executable, ref args);
        }

        private static string ResolveExecutableFromPath(string executable, ref string args)
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
                // wrap 'executable' within quotes to deal woth space in its path.
                args = $"/S /C \"\"{executable}\" {args}\"";
                executable = comSpec;
            }

            return executable;
        }

        private static string ResolveExecutablePathFromProject(string executable, NuGetFramework framework)
        {
            if (framework == null) return null;

            var projectRootPath = Directory.GetCurrentDirectory();

            if (!File.Exists(Path.Combine(projectRootPath, Project.FileName))) return null;

            var commandName = Path.GetFileNameWithoutExtension(executable);

            var projectContext = ProjectContext.Create(projectRootPath, framework);

            var commandPackage = projectContext.LibraryManager.GetLibraries()
                .Where(l => l.GetType() == typeof (PackageDescription))
                .Select(l => l as PackageDescription)
                .FirstOrDefault(p =>
                {
                    var fileNames = p.Library.Files
                        .Select(Path.GetFileName)
                        .Where(n => Path.GetFileNameWithoutExtension(n) == commandName)
                        .ToList();

                    return fileNames.Contains(commandName + FileNameSuffixes.DotNet.Exe) &&
                           fileNames.Contains(commandName + FileNameSuffixes.DotNet.DynamicLib) &&
                           fileNames.Contains(commandName + FileNameSuffixes.DotNet.Deps);
                });

            if (commandPackage == null) return null;

            var commandPath = commandPackage.Library.Files
                .First(f => Path.GetFileName(f) == commandName + FileNameSuffixes.DotNet.Exe);

            return Path.Combine(projectContext.PackagesDirectory, commandPackage.Path, commandPath);
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

            _process.EnableRaisingEvents = true;

#if DEBUG
            var sw = Stopwatch.StartNew();
            Reporter.Verbose.WriteLine($"> {FormatProcessInfo(_process.StartInfo)}".White());
#endif
            _process.Start();

            Reporter.Verbose.WriteLine($"Process ID: {_process.Id}");

            var threadOut = _stdOut.BeginRead(_process.StandardOutput);
            var threadErr = _stdErr.BeginRead(_process.StandardError);

            _process.WaitForExit();
            threadOut.Join();
            threadErr.Join();

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
                _stdOut.GetCapturedOutput(),
                _stdErr.GetCapturedOutput());
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
            _stdOut.Capture();
            return this;
        }

        public Command CaptureStdErr()
        {
            ThrowIfRunning();
            _stdErr.Capture();
            return this;
        }

        public Command ForwardStdOut(TextWriter to = null, bool onlyIfVerbose = false)
        {
            ThrowIfRunning();
            if (!onlyIfVerbose || CommandContext.IsVerbose())
            {
                if (to == null)
                {
                    _stdOut.ForwardTo(write: Reporter.Output.Write, writeLine: Reporter.Output.WriteLine);
                }
                else
                {
                    _stdOut.ForwardTo(write: to.Write, writeLine: to.WriteLine);
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
                    _stdErr.ForwardTo(write: Reporter.Error.Write, writeLine: Reporter.Error.WriteLine);
                }
                else
                {
                    _stdErr.ForwardTo(write: to.Write, writeLine: to.WriteLine);
                }
            }
            return this;
        }

        public Command OnOutputLine(Action<string> handler)
        {
            ThrowIfRunning();
            _stdOut.ForwardTo(write: null, writeLine: handler);
            return this;
        }

        public Command OnErrorLine(Action<string> handler)
        {
            ThrowIfRunning();
            _stdErr.ForwardTo(write: null, writeLine: handler);
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
    }

    internal sealed class StreamForwarder
    {
        private const int DefaultBufferSize = 256;

        private readonly int _bufferSize;
        private StringBuilder _builder;
        private StringWriter _capture;
        private Action<string> _write;
        private Action<string> _writeLine;

        internal StreamForwarder(int bufferSize = DefaultBufferSize)
        {
            _bufferSize = bufferSize;
        }

        internal void Capture()
        {
            if (_capture != null)
            {
                throw new InvalidOperationException("Already capturing stream!");
            }
            _capture = new StringWriter();
        }

        internal string GetCapturedOutput()
        {
            return _capture?.GetStringBuilder()?.ToString();
        }

        internal void ForwardTo(Action<string> write, Action<string> writeLine)
        {
            if (writeLine == null)
            {
                throw new ArgumentNullException(nameof(writeLine));
            }
            if (_writeLine != null)
            {
                throw new InvalidOperationException("Already handling stream!");
            }
            _write = write;
            _writeLine = writeLine;
        }

        internal Thread BeginRead(TextReader reader)
        {
            var thread = new Thread(() => Read(reader)) { IsBackground = true };
            thread.Start();
            return thread;
        }

        internal void Read(TextReader reader)
        {
            _builder = new StringBuilder();
            var buffer = new char[_bufferSize];
            int n;
            while ((n = reader.Read(buffer, 0, _bufferSize)) > 0)
            {
                _builder.Append(buffer, 0, n);
                WriteBlocks();
            }
            WriteRemainder();
        }

        private void WriteBlocks()
        {
            int n = _builder.Length;
            if (n == 0)
            {
                return;
            }

            int offset = 0;
            bool sawReturn = false;
            for (int i = 0; i < n; i++)
            {
                char c = _builder[i];
                switch (c)
                {
                    case '\r':
                        sawReturn = true;
                        continue;
                    case '\n':
                        WriteLine(_builder.ToString(offset, i - offset - (sawReturn ? 1 : 0)));
                        offset = i + 1;
                        break;
                }
                sawReturn = false;
            }

            // If the buffer contains no line breaks and _write is
            // supported, send the buffer content.
            if (!sawReturn &&
                (offset == 0) &&
                ((_write != null) || (_writeLine == null)))
            {
                WriteRemainder();
            }
            else
            {
                _builder.Remove(0, offset);
            }
        }

        private void WriteRemainder()
        {
            if (_builder.Length == 0)
            {
                return;
            }
            Write(_builder.ToString());
            _builder.Clear();
        }

        private void WriteLine(string str)
        {
            if (_capture != null)
            {
                _capture.WriteLine(str);
            }
            // If _write is supported, so is _writeLine.
            if (_writeLine != null)
            {
                _writeLine(str);
            }
        }

        private void Write(string str)
        {
            if (_capture != null)
            {
                _capture.Write(str);
            }
            if (_write != null)
            {
                _write(str);
            }
            else if (_writeLine != null)
            {
                _writeLine(str);
            }
        }
    }
}
