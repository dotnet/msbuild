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

        public LaunchSettingsApplyResult TryApplySettings(JsonElement model, ref ICommand command)
        {
            var config = new ProjectLaunchSettingsModel();
            foreach (var property in model.EnumerateObject())
            {
                if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.CommandLineArgs), StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetStringValue(property.Value, out var commandLineArgsValue))
                    {
                        return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.CouldNotConvertToString, property.Name));
                    }

                    config.CommandLineArgs = commandLineArgsValue;
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.LaunchBrowser), StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetBooleanValue(property.Value, out var launchBrowserValue))
                    {
                        return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.CouldNotConvertToBoolean, property.Name));
                    }

                    config.LaunchBrowser = launchBrowserValue;
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.LaunchUrl), StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetStringValue(property.Value, out var launchUrlValue))
                    {
                        return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.CouldNotConvertToString, property.Name));
                    }

                    config.LaunchUrl = launchUrlValue;
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.ApplicationUrl), StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetStringValue(property.Value, out var applicationUrlValue))
                    {
                        return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.CouldNotConvertToString, property.Name));
                    }

                    config.ApplicationUrl = applicationUrlValue;
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.EnvironmentVariables), StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                    {
                        return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.ValueMustBeAnObject, property.Name));
                    }

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
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    value = true;
                    return true;
                case JsonValueKind.False:
                    value = false;
                    return true;
                case JsonValueKind.Number:
                    if (element.TryGetDouble(out var doubleValue))
                    {
                        value = doubleValue != 0;
                        return true;
                    }
                    value = false;
                    return false;
                case JsonValueKind.String:
                    return bool.TryParse(element.GetString(), out value);
                default:
                    value = false;
                    return false;
            }
        }

        private static bool TryGetStringValue(JsonElement element, out string value)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    value = bool.TrueString;
                    return true;
                case JsonValueKind.False:
                    value = bool.FalseString;
                    return true;
                case JsonValueKind.Null:
                    value = string.Empty;
                    return true;
                case JsonValueKind.Number:
                    value = element.GetRawText();
                    return false;
                case JsonValueKind.String:
                    value = element.GetString();
                    return true;
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
