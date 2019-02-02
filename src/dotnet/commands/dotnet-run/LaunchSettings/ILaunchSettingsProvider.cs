using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    internal interface ILaunchSettingsProvider
    {
        string CommandName { get; }

        LaunchSettingsApplyResult TryApplySettings(JObject document, JObject model, ref ICommand command);
    }

}
