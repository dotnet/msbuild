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
                if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.CommandLineArgs), StringComparison.OrdinalIgnoreCase) 
                    && TryGetStringValue(property.Value, out var commandLineArgsValue))
                {
                    config.CommandLineArgs = commandLineArgsValue;
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.LaunchBrowser), StringComparison.OrdinalIgnoreCase) 
                    && TryGetBooleanValue(property.Value, out var launchBrowserValue))
                {
                    config.LaunchBrowser = launchBrowserValue;
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.LaunchUrl), StringComparison.OrdinalIgnoreCase) 
                    && TryGetStringValue(property.Value, out var launchUrlValue))
                {
                    config.LaunchUrl = launchUrlValue;
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.ApplicationUrl), StringComparison.OrdinalIgnoreCase) 
                    && TryGetStringValue(property.Value, out var applicationUrlValue))
                {
                    config.ApplicationUrl = applicationUrlValue;
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.EnvironmentVariables), StringComparison.OrdinalIgnoreCase) 
                    && property.Value.Type == JsonValueType.Object)
                {
                    foreach(var environmentVariable in property.Value.EnumerateObject())
                    {
                        if (TryGetStringValue(environmentVariable.Value, out var environmentVariableValue))
                        {
                            config.EnvironmentVariables[environmentVariable.Name] = environmentVariableValue;
                        }
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

        private static bool TryGetBooleanValue(JsonElement element, out bool value)
        {
            switch (element.Type)
            {
                case JsonValueType.True:
                    value = true;
                    return true;
                case JsonValueType.False:
                    value = false;
                    return true;
                case JsonValueType.Number:
                    if (element.TryGetDouble(out var doubleValue))
                    {
                        value = doubleValue != 0;
                        return true;
                    }
                    value = false;
                    return false;
                case JsonValueType.String:
                    return bool.TryParse(element.GetString(), out value);
                default:
                    value = false;
                    return false;
            }
        }

        private static bool TryGetStringValue(JsonElement element, out string value)
        {
            switch (element.Type)
            {
                case JsonValueType.True:
                    value = "true";
                    return true;
                case JsonValueType.False:
                    value = "false";
                    return true;
                case JsonValueType.Null:
                    value = null;
                    return true;
                case JsonValueType.Number:
                    if (element.TryGetDouble(out var doubleValue))
                    {
                        value = doubleValue.ToString();
                        return true;
                    }
                    value = null;
                    return false;
                case JsonValueType.String:
                    try
                    {
                        value = element.GetString();
                        return true;
                    }
                    catch(InvalidOperationException)
                    {
                        value = null;
                        return false;
                    }
                default:
                    value = null;
                    return false;
            }
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
