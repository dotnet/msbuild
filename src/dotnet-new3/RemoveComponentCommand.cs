using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3
{
    internal class RemoveComponentCommand
    {
        internal static void Configure(CommandLineApplication app)
        {
            CommandArgument assemblyName = app.Argument("assembly", "The assembly containing components to remove.");
            CommandOption help = app.Help();

            app.OnExecute(() =>
            {
                if (help.HasValue())
                {
                    app.ShowHelp();
                    return 0;
                }

                Assembly asm = Assembly.Load(new AssemblyName(assemblyName.Value));
                Program.Broker.ComponentRegistry.RemoveAll(asm);
                return 0;
            });
        }
    }
}