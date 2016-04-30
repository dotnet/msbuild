using System.Collections.Generic;
using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3
{
    public class TemplateCommand
    {
        public static void Configure(CommandLineApplication app)
        {
            app.Command("list", ListTemplateCommand.Configure);

            CommandArgument template = app.Argument("template", "The template to instantiate.");
            CommandOption name = app.Option("-n|--name", "The name for the output. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            CommandOption dir = app.Option("-d|--dir", "Indicates whether to display create a directory for the generated content.", CommandOptionType.NoValue);
            CommandOption parametersFiles = app.Option("-a|--args", "Adds a parameters file.", CommandOptionType.MultipleValue);
            CommandOption source = app.Option("-s|--source", "The specific template source to get the template from.", CommandOptionType.SingleValue);
            CommandOption help = app.Help();

            IReadOnlyDictionary<string, string> parameters = app.ParseExtraArgs();
            app.OnExecute(() => TemplateCreator.Instantiate(app, template, name, dir, source, parametersFiles, help, parameters));
        }
    }
}
