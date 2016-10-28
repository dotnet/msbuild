using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.DependencyInvoker
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var dotnetParams = new DotnetBaseParams("dotnet-dependency-tool-invoker", "DotNet Dependency Tool Invoker", "Invokes tools declared as NuGet dependencies of a project");
            
            dotnetParams.Parse(args);
            
            if (string.IsNullOrEmpty(dotnetParams.Command))
            {
                Console.WriteLine("A command name must be provided");
                
                return 1;
            }

            var commandFactory =
                new ProjectDependenciesCommandFactory(
                    dotnetParams.Framework,
                    dotnetParams.Config,
                    dotnetParams.Output,
                    dotnetParams.BuildBasePath,
                    dotnetParams.ProjectPath);

            var result = InvokeDependencyToolForMSBuild(commandFactory, dotnetParams);

            return result;
        }

        private static int InvokeDependencyToolForMSBuild(
            ProjectDependenciesCommandFactory commandFactory,
            DotnetBaseParams dotnetParams)
        {
            Console.WriteLine($"Invoking '{dotnetParams.Command}' for '{dotnetParams.Framework.GetShortFolderName()}'.");

            return InvokeDependencyTool(commandFactory, dotnetParams, dotnetParams.Framework);
        }

        private static int InvokeDependencyTool(
            ProjectDependenciesCommandFactory commandFactory,
            DotnetBaseParams dotnetParams,
            NuGetFramework framework)
        {
            try
            {
                var exitCode = commandFactory.Create(
                        $"dotnet-{dotnetParams.Command}",
                        dotnetParams.RemainingArguments,
                        framework,
                        dotnetParams.Config)
                    .ForwardStdErr()
                    .ForwardStdOut()
                    .Execute()
                    .ExitCode;

                Console.WriteLine($"Command returned {exitCode}");
            }
            catch (CommandUnknownException)
            {
                Console.WriteLine($"Command not found");
                return 1;
            }

            return 0;
        }
    }
}
