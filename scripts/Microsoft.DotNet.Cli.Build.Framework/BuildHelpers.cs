using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Build.Framework
{
    public static class BuildHelpers
    {
        public static int ExecInSilent(string workingDirectory, string command, params string[] args) => ExecInSilent(workingDirectory, command, (IEnumerable<string>)args);
        public static int ExecInSilent(string workingDirectory, string command, IEnumerable<string> args) => ExecCore(command, args, workingDirectory, silent: true);

        public static int ExecIn(string workingDirectory, string command, params string[] args) => ExecIn(workingDirectory, command, (IEnumerable<string>)args);
        public static int ExecIn(string workingDirectory, string command, IEnumerable<string> args) => ExecCore(command, args, workingDirectory, silent: false);

        public static int ExecSilent(string command, params string[] args) => ExecSilent(command, (IEnumerable<string>)args);
        public static int ExecSilent(string command, IEnumerable<string> args) => ExecCore(command, args, workingDirectory: null, silent: true);

        public static int Exec(string command, params string[] args) => Exec(command, (IEnumerable<string>)args);
        public static int Exec(string command, IEnumerable<string> args) => ExecCore(command, args, workingDirectory: null, silent: false);

        public static Command Cmd(string command, params string[] args) => Cmd(command, (IEnumerable<string>)args);
        public static Command Cmd(string command, IEnumerable<string> args)
        {
            return Command.Create(command, args);
        }

        internal static int ExecCore(string command, IEnumerable<string> args, string workingDirectory, bool silent)
        {
            var cmd = Cmd(command, args);
            if(!string.IsNullOrEmpty(workingDirectory))
            {
                cmd.WorkingDirectory(workingDirectory);
            }
            if(silent)
            {
                cmd.CaptureStdErr().CaptureStdOut();
            }
            var result = cmd.Execute();

            result.EnsureSuccessful();
            return result.ExitCode;
        }
    }
}
