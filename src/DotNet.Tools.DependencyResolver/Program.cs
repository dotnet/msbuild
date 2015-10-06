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
            app.Description = "Resolves the absolute path of all dependencies for a project";

            var packages = app.Option("-p|--packages <PACKAGES_DIRECTORY>", "Path to the directories containing packages to resolve.", CommandOptionType.MultipleValue);
            var target = app.Option("-t|--target <TARGET_IDENTIFIER>", "The target to resolve dependencies for.", CommandOptionType.SingleValue);
            var output = app.Option("-o|--output <OUTPUT_FILE>", "The path in which to write the output file (formatted as text with one line per dependency)", CommandOptionType.SingleValue);
            var assetType = app.Option("-a|--assets <ASSET_TYPE>", "The type of assets to resolve (common values include: compile, runtime, native)", CommandOptionType.MultipleValue);
            var project = app.Argument("PROJECT", "The project to resolve. A directory or a path to a project.lock.json may be used. Defaults to the current directory");

            app.OnExecute(() =>
            {
                // Check required args
                if(!packages.HasValue())
                {
                    Console.Error.WriteLine("Missing required argument: --packages");
                    app.ShowHelp();
                    return 1;
                }
                if(!target.HasValue())
                {
                    Console.Error.WriteLine("Missing required argument: --target");
                    app.ShowHelp();
                    return 1;
                }
                if(!assetType.HasValue())
                {
                    Console.Error.WriteLine("Missing required argument: --assets");
                    app.ShowHelp();
                    return 1;
                }

                var path = project.Value ?? Directory.GetCurrentDirectory();
                if (!path.EndsWith("project.lock.json"))
                {
                    path = Path.Combine(path, "project.lock.json");
                }
                return Resolver.Execute(packages.Values, target.Value(), output.Value(), assetType.Values, path);
            });

            app.Execute(args);
        }
    }
}
