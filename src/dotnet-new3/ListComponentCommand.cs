using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    internal class ListComponentCommand
    {
        internal static void Configure(CommandLineApplication app)
        {
            app.OnExecute(() =>
            {
                bool anyWritten = false;
                foreach (IComponent component in Program.Broker.ComponentRegistry.OfType<ITemplateSource>())
                {
                    anyWritten = true;
                    Reporter.Output.WriteLine($"{component.Name} - Template Source - {component.GetType().AssemblyQualifiedName}");
                }

                foreach (IComponent component in Program.Broker.ComponentRegistry.OfType<IGenerator>())
                {
                    anyWritten = true;
                    Reporter.Output.WriteLine($"{component.Name} - Generator - {component.GetType().AssemblyQualifiedName}");
                }

                if (!anyWritten)
                {
                    Reporter.Output.WriteLine("(No components installed)");
                }

                return 0;
            });

        }
    }
}