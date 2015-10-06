using System;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace DotNet.Tools.DependencyResolver
{
    public class Program
    {
        public void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.HelpOption("-h|--help");
            app.Description = "Resolves the absolute path of all source files used by a project";

            var output = app.Option("-o|--output <OUTPUT_FILE>", "The path in which to write the output file (formatted as text with one line per dependency)", CommandOptionType.SingleValue);
            var project = app.Argument("PROJECT", "The project to resolve. A directory or a path to a project.json may be used. Defaults to the current directory");

            app.OnExecute(() =>
            {
                var path = project.Value ?? Directory.GetCurrentDirectory();
                if (!path.EndsWith("project.json"))
                {
                    path = Path.Combine(path, "project.json");
                }
                return Resolver.Execute(path, output.Value());
            });

            app.Execute(args);
        }
    }
}
