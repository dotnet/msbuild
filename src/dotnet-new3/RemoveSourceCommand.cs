using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3
{
    internal class RemoveSourceCommand
    {
        internal static void Configure(CommandLineApplication app)
        {
            CommandArgument name = app.Argument("name", "The name of the template source to remove.");
            CommandOption help = app.Help();

            app.OnExecute(() =>
            {
                if (help.HasValue())
                {
                    app.ShowHelp();
                    return 0;
                }

                Program.Broker.RemoveConfiguredSource(name.Value);
                return 0;
            });
        }
    }
}