using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    internal interface ILaunchSettingsProvider
    {
        LaunchSettingsApplyResult TryGetLaunchSettings(JsonElement model);
    }

}
