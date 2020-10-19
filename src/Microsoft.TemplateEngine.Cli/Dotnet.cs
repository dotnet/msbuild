using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.TemplateEngine.Cli
{
    public class Dotnet
    {
        private ProcessStartInfo _info;
        private DataReceivedEventHandler _errorDataReceived;
        private StringBuilder _stderr;
        private StringBuilder _stdout;
        private DataReceivedEventHandler _outputDataReceived;
        private bool _anyNonEmptyStderrWritten;

        public string Command => string.Concat(_info.FileName, " ", _info.Arguments);


        public static Dotnet Restore(params string[] args)
        {
            return new Dotnet
            {
                _info = new ProcessStartInfo("dotnet", ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(new[] { "restore" }.Concat(args)))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
        }

        public static Dotnet AddProjectToProjectReference(string projectFile, params string[] args)
        {
            return new Dotnet
            {
                _info = new ProcessStartInfo("dotnet", ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(new[] { "add", projectFile, "reference" }.Concat(args)))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
        }

        public static Dotnet AddPackageReference(string projectFile, string packageName, string version = null)
        {
            string argString;
            if (version == null)
            {
                argString = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(new[] { "add", projectFile, "package", packageName });
            }
            else
            {
                argString = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(new[] { "add", projectFile, "package", packageName, "--version", version });
            }

            return new Dotnet
            {
                _info = new ProcessStartInfo("dotnet", argString)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
        }

        public static Dotnet AddProjectsToSolution(string solutionFile, IReadOnlyList<string> projects, string solutionFolder = "")
        {
            List<string> allArgs = new List<string>()
            {
                "sln",
                solutionFile,
                "add"
            };

            if (!string.IsNullOrWhiteSpace(solutionFolder))
            {
                allArgs.Add("--solution-folder");
                allArgs.Add(solutionFolder);
            }

            allArgs.AddRange(projects);
            string argString = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(allArgs);

            return new Dotnet
            {
                _info = new ProcessStartInfo("dotnet", argString)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
        }

        public Dotnet ForwardStdErr()
        {
            _errorDataReceived = ForwardStreamStdErr;
            return this;
        }

        public Dotnet ForwardStdOut()
        {
            _outputDataReceived = ForwardStreamStdOut;
            return this;
        }

        private void ForwardStreamStdOut(object sender, DataReceivedEventArgs e)
        {
            Console.Out.WriteLine(e.Data);
        }

        private void ForwardStreamStdErr(object sender, DataReceivedEventArgs e)
        {
            if (!_anyNonEmptyStderrWritten)
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                {
                    return;
                }

                _anyNonEmptyStderrWritten = true;
            }

            Console.Error.WriteLine(e.Data);
        }

        public Dotnet CaptureStdOut()
        {
            _stdout = new StringBuilder();
            _outputDataReceived += CaptureStreamStdOut;
            return this;
        }

        private void CaptureStreamStdOut(object sender, DataReceivedEventArgs e)
        {
            _stdout.AppendLine(e.Data);
        }

        public Dotnet CaptureStdErr()
        {
            _stderr = new StringBuilder();
            _errorDataReceived += CaptureStreamStdErr;
            return this;
        }

        private void CaptureStreamStdErr(object sender, DataReceivedEventArgs e)
        {
            _stderr.AppendLine(e.Data);
        }

        public Result Execute()
        {
            Process p = Process.Start(_info);
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.ErrorDataReceived += OnErrorDataReceived;
            p.OutputDataReceived += OnOutputDataReceived;
            p.WaitForExit();

            return new Result(_stdout?.ToString(), _stderr?.ToString(), p.ExitCode);
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            _outputDataReceived?.Invoke(sender, e);
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            _errorDataReceived?.Invoke(sender, e);
        }

        public class Result
        {
            public Result(string stdout, string stderr, int exitCode)
            {
                StdErr = stderr;
                StdOut = stdout;
                ExitCode = exitCode;
            }

            public string StdErr { get; }

            public string StdOut { get; }

            public int ExitCode { get; }
        }

        public static Dotnet Version()
        {
            return new Dotnet
            {
                _info = new ProcessStartInfo("dotnet", "--version")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
        }
    }
}
