using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    public class TemplateCommand
    {
        public static void Configure(CommandLineApplication app)
        {
            app.Command("list", ListTemplateCommand.Configure);

            var template = app.Argument("template", "The template to instantiate.");
            var name = app.Option("-n|--name", "The name for the output. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            var help = app.Option("-h|--help", "Indicates whether to display the help for the template's parameters instead of creating it.", CommandOptionType.NoValue);
            var parametersFiles = app.Option("-a|--args", "Adds a parameters file.", CommandOptionType.MultipleValue);
            var source = app.Option("-s|--source", "The specific template source to get the template from.", CommandOptionType.SingleValue);
            var parameters = app.Option("-p|--parameter", "The parameter name/value alternations to supply to the template.", CommandOptionType.MultipleValue);

            app.OnExecute(() => TemplateCreator.Instantiate(app, template, name, source, parametersFiles, help, parameters));
        }
    }

    public class ListTemplateCommand
    {
        public static void Configure(CommandLineApplication app)
        {
            var searchString = app.Argument("search", "The template name to search for.");
            var source = app.Option("-s|--source", "The specific template source to get the template from.", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
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
