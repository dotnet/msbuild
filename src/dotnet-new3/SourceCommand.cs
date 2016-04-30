using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3
{
    internal class SourceCommand
    {
        internal static void Configure(CommandLineApplication app)
        {
            app.Command("add", AddSourceCommand.Configure);
            app.Command("list", ListSourceCommand.Configure);
            app.Command("remove", RemoveSourceCommand.Configure);
            app.Help();

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });
        }
    }
}