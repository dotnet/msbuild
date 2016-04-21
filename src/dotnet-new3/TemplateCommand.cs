using Microsoft.Extensions.CommandLineUtils;

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
}
