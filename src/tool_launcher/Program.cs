using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Launcher
{
    /// <summary>
    /// The app is simple shim into launching arbitrary command line processes.
    /// It is configured via app settings .NET config file. (See app.config).
    /// </summary>
    /// <remarks>
    /// Launching new processes using cmd.exe and .cmd files causes issues for long-running process
    /// because CTRL+C always hangs on interrupt with the prompt "Terminate Y/N". This can lead to
    /// orphaned processes.
    /// </remarks>
    class Program
    {
        private const string TRACE = "DOTNET_LAUNCHER_TRACE";
        private const int ERR_FAILED = -1;
        private static bool _trace;

        public static int Main(string[] argsToForward)
        {
            bool.TryParse(Environment.GetEnvironmentVariable(TRACE), out _trace);

            try
            {
                var appSettings = ConfigurationManager.AppSettings;

                var entryPoint = appSettings["entryPoint"];
                if (string.IsNullOrEmpty(entryPoint))
                {
                    LogError("The launcher must specify a non-empty appSetting value for 'entryPoint'.");
                    return ERR_FAILED;
                }

                var exePath = entryPoint;
                var runner = appSettings["runner"];

                var args = new List<string>();

                if (!string.IsNullOrEmpty(runner))
                {
                    args.Add(entryPoint);
                    exePath = runner;
                }

                args.AddRange(argsToForward);

                var argString = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);

                using (var process = new Process
                {
                    StartInfo =
                    {
                        FileName = exePath,
                        Arguments = argString,
                        CreateNoWindow = false,
                        UseShellExecute = false,
                    }
                })
                {
                    LogTrace("Starting a new process.");
                    LogTrace("filename = " + process.StartInfo.FileName);
                    LogTrace("args = " + process.StartInfo.Arguments);
                    LogTrace("cwd = " + process.StartInfo.WorkingDirectory);

                    try
                    {
                        process.Start();
                    }
                    catch (Win32Exception ex)
                    {
                        LogTrace(ex.ToString());
                        LogError($"Failed to start '{process.StartInfo.FileName}'. " + ex.Message);
                        return ERR_FAILED;
                    }

                    process.WaitForExit();

                    LogTrace("Exited code " + process.ExitCode);

                    return process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                LogError("Unexpected error launching a new process. Run with the environment variable " + TRACE + "='true' for details.");
                LogTrace(ex.ToString());
                return ERR_FAILED;
            }
        }

        private static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Error.WriteLine("ERROR: " + message);
            Console.ResetColor();
        }

        private static void LogTrace(string message)
        {
            if (!_trace)
                return;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.WriteLine("[dotnet-launcher] " + message);
            Console.ResetColor();
        }
    }
}
