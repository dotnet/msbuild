using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3
{
    public class ComponentCommand
    {
        internal static void Configure(CommandLineApplication app)
        {
            app.Command("add", AddComponentCommand.Configure);
            app.Command("list", ListComponentCommand.Configure);
            app.Command("remove", RemoveComponentCommand.Configure);
            app.Help();

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });
        }
    }
}
