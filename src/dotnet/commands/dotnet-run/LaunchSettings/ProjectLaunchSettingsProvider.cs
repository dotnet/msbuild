using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    internal class ProjectLaunchSettingsProvider : ILaunchSettingsProvider
    {
        public static readonly string CommandNameValue = "Project";

        public string CommandName => CommandNameValue;

        public LaunchSettingsApplyResult TryApplySettings(JsonElement document, JsonElement model, ref ICommand command)
        {
            var config = new ProjectLaunchSettingsModel();
            foreach (var property in model.EnumerateObject())
            {
                if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.CommandLineArgs), StringComparison.OrdinalIgnoreCase))
                {
                    config.CommandLineArgs = property.Value.GetString();
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.LaunchBrowser), StringComparison.OrdinalIgnoreCase))
                {
                    config.LaunchBrowser = property.Value.GetBoolean();
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.LaunchUrl), StringComparison.OrdinalIgnoreCase))
                {
                    config.LaunchUrl = property.Value.GetString();
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.ApplicationUrl), StringComparison.OrdinalIgnoreCase))
                {
                    config.ApplicationUrl = property.Value.GetString();
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.EnvironmentVariables), StringComparison.OrdinalIgnoreCase))
                {
                    foreach(var environmentVariable in property.Value.EnumerateObject())
                    {
                        config.EnvironmentVariables[environmentVariable.Name] = environmentVariable.Value.GetString();
                    }
                }
            }

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
                EnvironmentVariables = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            public string CommandLineArgs { get; set; }

            public bool LaunchBrowser { get; set; }

            public string LaunchUrl { get; set; }

            public string ApplicationUrl { get; set; }

            public Dictionary<string, string> EnvironmentVariables { get; }
        }
    }
}
