using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    public class ProjectLaunchSettingsProvider : ILaunchSettingsProvider
    {
        public static readonly string CommandNameValue = "Project";

        public string CommandName => CommandNameValue;

        public LaunchSettingsApplyResult TryApplySettings(JObject document, JObject model, ref ICommand command)
        {
            var config = model.ToObject<ProjectLaunchSettingsModel>();

            if (!string.IsNullOrEmpty(config.ApplicationUrl))
            {
                command.EnvironmentVariable("ASPNETCORE_URLS", config.ApplicationUrl);
            }

            //For now, ignore everything but the environment variables section

            foreach (var entry in config.EnvironmentVariables)
            {
                string value = Environment.ExpandEnvironmentVariables(entry.Value);
                //NOTE: MSBuild variables are not expanded like they are in VS
                command.EnvironmentVariable(entry.Key, value);
            }

            return new LaunchSettingsApplyResult(true, null, config.LaunchUrl);
        }

        private class ProjectLaunchSettingsModel
        {
            public ProjectLaunchSettingsModel()
            {
                EnvironmentVariables = new Dictionary<string, string>();
            }

            public string CommandLineArgs { get; set; }

            public bool LaunchBrowser { get; set; }

            public string LaunchUrl { get; set; }

            public string ApplicationUrl { get; set; }

            public Dictionary<string, string> EnvironmentVariables { get; }
        }
    }
}
