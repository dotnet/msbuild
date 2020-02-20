using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    internal interface ILaunchSettingsProvider
    {
        string CommandName { get; }

        LaunchSettingsApplyResult TryApplySettings(JsonElement model, ref ICommand command);
    }

}
