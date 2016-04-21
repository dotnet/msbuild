using System;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    public class Program
    {
        internal static IBroker Broker { get; private set; }

        public static int Main(string[] args)
        {
            Broker = new Broker();

            CommandLineApplication app = new CommandLineApplication
            {
                Name = "dotnet new3",
                FullName = "Mutant Chicken Template Instantiation Commands for .NET Core CLI."
            };

            app.Command("source", SourceCommand.Configure);
            app.Command("template", TemplateCommand.Configure);
            app.Command("component", ComponentCommand.Configure);
            app.Command("reset", ResetCommand.Configure);

            CommandArgument template = app.Argument("template", "The template to instantiate.");
            CommandOption name = app.Option("-n|--name", "The name for the output. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            CommandOption help = app.Option("-h|--help", "Indicates whether to display the help for the template's parameters instead of creating it.", CommandOptionType.NoValue);
            CommandOption parametersFiles = app.Option("-a|--args", "Adds a parameters file.", CommandOptionType.MultipleValue);
            CommandOption source = app.Option("-s|--source", "The specific template source to get the template from.", CommandOptionType.SingleValue);
            CommandOption parameters = app.Option("-p|--parameter", "The parameter name/value alternations to supply to the template.", CommandOptionType.MultipleValue);

            app.OnExecute(() => TemplateCreator.Instantiate(app, template, name, source, parametersFiles, help, parameters));

            int result;
            try
            {
                result = app.Execute(args);
            }
            catch (Exception ex)
            {
                AggregateException ax = ex as AggregateException;

                while (ax != null && ax.InnerExceptions.Count == 1)
                {
                    ex = ax.InnerException;
                    ax = ex as AggregateException;
                }

                Reporter.Error.WriteLine(ex.Message.Bold().Red());
                Reporter.Error.WriteLine(ex.StackTrace.Bold().Red());
                result = 1;
            }

            return result;
        }
    }
}
