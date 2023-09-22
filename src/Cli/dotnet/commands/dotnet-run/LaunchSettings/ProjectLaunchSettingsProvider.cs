// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Text.Json;

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    internal class ProjectLaunchSettingsProvider : ILaunchSettingsProvider
    {
        public static readonly string CommandNameValue = "Project";

        public string CommandName => CommandNameValue;

        public LaunchSettingsApplyResult TryGetLaunchSettings(string? launchProfileName, JsonElement model)
        {
            var config = new ProjectLaunchSettingsModel
            {
                LaunchProfileName = launchProfileName
            };

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
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.DotNetRunMessages), StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetStringValue(property.Value, out var dotNetRunMessages))
                    {
                        return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.CouldNotConvertToString, property.Name));
                    }

                    config.DotNetRunMessages = dotNetRunMessages;
                }
                else if (string.Equals(property.Name, nameof(ProjectLaunchSettingsModel.EnvironmentVariables), StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                    {
                        return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.ValueMustBeAnObject, property.Name));
                    }

                    foreach (var environmentVariable in property.Value.EnumerateObject())
                    {
                        if (TryGetStringValue(environmentVariable.Value, out var environmentVariableValue))
                        {
                            config.EnvironmentVariables[environmentVariable.Name] = environmentVariableValue;
                        }
                    }
                }
            }

            return new LaunchSettingsApplyResult(true, null, config);
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

        private static bool TryGetStringValue(JsonElement element, out string? value)
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
    }
}
