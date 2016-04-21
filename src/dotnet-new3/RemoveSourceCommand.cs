using System;
using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3
{
    internal class RemoveSourceCommand
    {
        internal static void Configure(CommandLineApplication obj)
        {
            var name = obj.Argument("name", "The name of the template source to remove.");

            obj.OnExecute(() =>
            {
                Program.Broker.RemoveConfiguredSource(name.Value);
                return 0;
            });
        }
    }
}