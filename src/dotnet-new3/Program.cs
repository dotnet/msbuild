using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            //Console.ReadLine();
            Broker = new Broker();

            CommandLineApplication app = new CommandLineApplication(false)
            {
                Name = "dotnet new3",
                FullName = "Mutant Chicken Template Instantiation Commands for .NET Core CLI."
            };

            app.Command("source", SourceCommand.Configure, false);
            app.Command("template", TemplateCommand.Configure, false);
            app.Command("component", ComponentCommand.Configure, false);
            app.Command("reset", ResetCommand.Configure, false);

            CommandArgument template = app.Argument("template", "The template to instantiate.");
            CommandOption name = app.Option("-n|--name", "The name for the output. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            CommandOption listOnly = app.Option("-l|--list", "Lists templates with containing the specified name.", CommandOptionType.NoValue);
            CommandOption help = app.Option("-h|--help", "Indicates whether to display the help for the template's parameters instead of creating it.", CommandOptionType.NoValue);
            CommandOption dir = app.Option("-d|--dir", "Indicates whether to display create a directory for the generated content.", CommandOptionType.NoValue);
            CommandOption parametersFiles = app.Option("-a|--args", "Adds a parameters file.", CommandOptionType.MultipleValue);
            CommandOption source = app.Option("-s|--source", "The specific template source to get the template from.", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                if (listOnly.HasValue())
                {
                    IEnumerable<ITemplate> results = TemplateCreator.List(template, source);
                    Reporter.Output.WriteLine("Template Name - Source Name - Generator Name");

                    foreach (ITemplate res in results)
                    {
                        Reporter.Output.WriteLine($"{res.Name} - {res.Source.Alias} - {res.Generator.Name}");
                    }
                    return Task.FromResult(0);
                }

                IReadOnlyDictionary<string, string> parameters = app.ParseExtraArgs();
                return TemplateCreator.Instantiate(app, template, name, dir, source, parametersFiles, help, parameters);
            });

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

                while(ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    ax = ex as AggregateException;

                    while (ax != null && ax.InnerExceptions.Count == 1)
                    {
                        ex = ax.InnerException;
                        ax = ex as AggregateException;
                    }
                }

                Reporter.Error.WriteLine(ex.StackTrace.Bold().Red());
                result = 1;
            }

            return result;
        }
    }
}
