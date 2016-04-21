using System;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    internal class AddSourceCommand
    {
        internal static void Configure(CommandLineApplication obj)
        {
            CommandArgument alias = obj.Argument("alias", "The alias template source.");
            CommandArgument location = obj.Argument("location", "The location of the template source.");
            CommandOption sourceName = obj.Option("-n|--name", "The name of the template source reader.", CommandOptionType.SingleValue);

            obj.OnExecute(() =>
            {
                if (sourceName.HasValue())
                {
                    Program.Broker.AddConfiguredSource(alias.Value, sourceName.Value(), location.Value);
                    return 0;
                }

                ITemplateSource source = null;
                foreach (ITemplateSource src in Program.Broker.ComponentRegistry.OfType<ITemplateSource>())
                {
                    if (src.CanHandle(location.Value))
                    {
                        source = src;
                    }
                }

                if (source == null)
                {
                    return -1;
                }

                Program.Broker.AddConfiguredSource(alias.Value, source.Name, location.Value);
                return 0;
            });
        }
    }
}