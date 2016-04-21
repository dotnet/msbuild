using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3
{
    internal class SourceCommand
    {
        internal static void Configure(CommandLineApplication obj)
        {
            obj.Command("add", AddSourceCommand.Configure);
            obj.Command("list", ListSourceCommand.Configure);
            obj.Command("remove", RemoveSourceCommand.Configure);

            obj.OnExecute(() =>
            {
                obj.ShowHelp();
                return 0;
            });
        }
    }
}