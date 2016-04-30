using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    public class ListTemplateCommand
    {
        public static void Configure(CommandLineApplication app)
        {
            CommandArgument searchString = app.Argument("search", "The template name to search for.");
            CommandOption source = app.Option("-s|--source", "The specific template source to get the template from.", CommandOptionType.SingleValue);
            CommandOption help = app.Help();

            app.OnExecute(() =>
            {
                if (help.HasValue())
                {
                    app.ShowHelp();
                    return 0;
                }

                IEnumerable<ITemplate> results = TemplateCreator.List(searchString, source);
                Reporter.Output.WriteLine("Template Name - Source Name - Generator Name");

                foreach (ITemplate result in results)
                {
                    Reporter.Output.WriteLine($"{result.Name} - {result.Source.Alias} - {result.Generator.Name}");
                }

                return 0;
            });
        }
    }
}