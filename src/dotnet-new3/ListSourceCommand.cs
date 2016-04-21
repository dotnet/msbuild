using System;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    internal class ListSourceCommand
    {
        internal static void Configure(CommandLineApplication obj)
        {
            obj.OnExecute(() =>
            {
                Reporter.Output.WriteLine("Alias - Provider - Location");

                foreach(IConfiguredTemplateSource source in Program.Broker.GetConfiguredSources())
                {
                    Reporter.Output.WriteLine($"{source.Alias} - {source.Source.Name} - {source.Location}");
                }

                return 0;
            });
        }
    }
}