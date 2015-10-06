using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "dotnet";
            app.Description = "The .NET CLI";

            // Most commonly used commands
            app.Command("init", c =>
            {
                c.Description = "Scaffold a basic .NET application";

                c.OnExecute(() => Exec("dotnet-init", c.RemainingArguments));
            });

            app.Command("compile", c =>
            {
                c.Description = "Produce assemblies for the project in given directory";

                c.OnExecute(() => Exec("dotnet-compile", c.RemainingArguments));
            });

            app.Command("restore", c =>
            {
                c.Description = "Restore packages";

                c.OnExecute(() => Exec("dotnet-restore", c.RemainingArguments));
            });

            app.Command("pack", c =>
            {
                c.Description = "Produce a NuGet package";

                c.OnExecute(() => Exec("dotnet-pack", c.RemainingArguments));
            });

            app.Command("publish", c =>
            {
                c.Description = "Produce deployable assets";

                c.OnExecute(() => Exec("dotnet-publish", c.RemainingArguments));
            });
            
            // TODO: Handle unknown commands

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 2;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return 1;
        }

        private static int Exec(string executable, IList<string> args)
        {
            var comSpec = Environment.GetEnvironmentVariable("ComSpec");
            if (!string.IsNullOrEmpty(comSpec))
            {
                args =
                    new[] { "/C", "\"", executable }
                    .Concat(args)
                    .Concat(new[] { "\"" })
                    .ToArray();
                executable = comSpec;
            }
            else  
            {
                // Temporary, we're doing this so that redirecting the output works
                args = new[] { "bash", "-c", "\"", executable }
                        .Concat(args.Select(argument => argument.Replace("\"", "\\\"")))
                        .Concat(new[] { "\"" })
                        .ToArray();
                executable = "/usr/bin/env";
            }

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var process = Process.Start(psi);
            process.ErrorDataReceived += OnProcessErrorDataReceived;
            process.OutputDataReceived += OnProcessOutputDataReceived;

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit();

            return process.ExitCode;
        }

        private static void OnProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private static void OnProcessErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.Error.WriteLine(e.Data);
        }
    }
}
