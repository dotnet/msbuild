using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;

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
            
            var projectContexts = 
                CreateProjectContexts(dotnetParams.ProjectPath)
                    .Where(p => dotnetParams.Framework == null ||
                                dotnetParams.Framework.GetShortFolderName()
                                .Equals(p.TargetFramework.GetShortFolderName()));
            
            var commandFactory =
                new ProjectDependenciesCommandFactory(
                    dotnetParams.Framework,
                    dotnetParams.Config,
                    dotnetParams.Output,
                    dotnetParams.BuildBasePath,
                    projectContexts.First().ProjectDirectory);
                    
            foreach (var projectContext in projectContexts)
            {
                Console.WriteLine($"Invoking '{dotnetParams.Command}' for '{projectContext.TargetFramework}'.");

                try
                {
                    var exitCode = commandFactory.Create(
                            $"dotnet-{dotnetParams.Command}",
                            dotnetParams.RemainingArguments,
                            projectContext.TargetFramework,
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
            }
            return 0;
        }

        private static IEnumerable<ProjectContext> CreateProjectContexts(string projectPath = null)
        {
            projectPath = projectPath ?? Directory.GetCurrentDirectory();

            if (!projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.Combine(projectPath, Project.FileName);
            }

            if (!File.Exists(projectPath))
            {
                throw new InvalidOperationException($"{projectPath} does not exist.");
            }

            return ProjectContext.CreateContextForEachFramework(projectPath);
        }
    }
}
