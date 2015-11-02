using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Run
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet run";
            app.FullName = ".NET Executor";
            app.Description = "Runner for the .NET Platform";
            app.HelpOption("-h|--help");

            var framework = app.Option("-f|--framework <FRAMEWORK>", "Compile a specific framework", CommandOptionType.MultipleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var preserveTemporaryOutput = app.Option("-t|--preserve-temporary", "Keep the output's temporary directory around", CommandOptionType.NoValue);
            var project = app.Option("-p|--project <PROJECT_PATH>", "The path to the project to run (defaults to the current directory)", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                // Locate the project and get the name and full path
                var path = Directory.GetCurrentDirectory();
                if(project.HasValue())
                {
                    path = project.Value();
                }

                var contexts = ProjectContext.CreateContextForEachFramework(path);
                if (!framework.HasValue())
                {
                    return Run(contexts.First(), configuration.Value() ?? Constants.DefaultConfiguration, app.RemainingArguments, preserveTemporaryOutput.HasValue());
                }
                else
                {
                    var context = contexts.FirstOrDefault(c => c.TargetFramework.Equals(NuGetFramework.Parse(framework.Value())));
                    return Run(context, configuration.Value() ?? Constants.DefaultConfiguration, app.RemainingArguments, preserveTemporaryOutput.HasValue());
                }
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }

        private static int Run(ProjectContext context, string configuration, List<string> remainingArguments, bool preserveTemporaryOutput)
        {
            // Create a temporary directory
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            // Compile to that directory
            var result = Command.Create($"dotnet-compile", $"--output \"{tempDir}\" --framework \"{context.TargetFramework}\" --configuration \"{configuration}\" {context.ProjectFile.ProjectDirectory}")
                .ForwardStdOut(onlyIfVerbose: true)
                .ForwardStdErr()
                .Execute();

            if (result.ExitCode != 0)
            {
                return result.ExitCode;
            }

            // Now launch the output and give it the results
            var outputName = Path.Combine(tempDir, context.ProjectFile.Name + Constants.ExeSuffix);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (context.TargetFramework.IsDesktop())
                {
                    // Run mono if we're running a desktop target on non windows
                    remainingArguments.Insert(0, outputName + ".exe");

                    if (string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase))
                    {
                        // If we're compiling for the debug configuration then add the --debug flag
                        // other options may be passed using the MONO_OPTIONS env var
                        remainingArguments.Insert(0, "--debug");
                    }

                    outputName = "mono";
                }
            }

            result = Command.Create(outputName, string.Join(" ", remainingArguments))
                .ForwardStdOut()
                .ForwardStdErr()
                .EnvironmentVariable("CLRHOST_CLR_PATH", AppContext.BaseDirectory)
                .Execute();

            // Clean up
            if (!preserveTemporaryOutput)
            {
                Directory.Delete(tempDir, recursive: true);
            }

            return result.ExitCode;
        }
    }
}
