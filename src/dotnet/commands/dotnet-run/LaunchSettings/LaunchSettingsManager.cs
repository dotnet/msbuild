using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    internal class LaunchSettingsManager
    {
        private const string ProfilesKey = "profiles";
        private const string CommandNameKey = "commandName";
        private const string DefaultProfileCommandName = "Project";
        private static IReadOnlyDictionary<string, ILaunchSettingsProvider> _providers;

        static LaunchSettingsManager()
        {
            _providers = new Dictionary<string, ILaunchSettingsProvider>
            {
                { ProjectLaunchSettingsProvider.CommandNameValue, new ProjectLaunchSettingsProvider() }
            };
        }

        public static LaunchSettingsApplyResult TryApplyLaunchSettings(string launchSettingsJsonContents, ref ICommand command, string profileName = null)
        {
            try
            {
                using (var document = JsonDocument.Parse(launchSettingsJsonContents))
                {
                    var model = document.RootElement;

                    if (model.Type != JsonValueType.Object || !model.TryGetProperty(ProfilesKey, out var profilesObject) || profilesObject.Type != JsonValueType.Object)
                    {
                        return new LaunchSettingsApplyResult(false, LocalizableStrings.LaunchProfilesCollectionIsNotAJsonObject);
                    }

                    JsonElement profileObject;
                    if (profileName == null)
                    {
                        profileObject = profilesObject
                            .EnumerateObject()
                            .FirstOrDefault(IsDefaultProfileType).Value;
                    }
                    else
                    {
                        if (!profilesObject.TryGetProperty(profileName, out profileObject) || profileObject.Type != JsonValueType.Object)
                        {
                            return new LaunchSettingsApplyResult(false, LocalizableStrings.LaunchProfileIsNotAJsonObject);
                        }
                    }

                    if (profileObject.Type == default)
                    {
                        foreach (var prop in profilesObject.EnumerateObject())
                        {
                            if (prop.Value.Type == JsonValueType.Object)
                            {
                                if (prop.Value.TryGetProperty(CommandNameKey, out var commandNameElement) && commandNameElement.Type == JsonValueType.String)
                                {
                                    if (_providers.ContainsKey(commandNameElement.GetString()))
                                    {
                                        profileObject = prop.Value;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (profileObject.Type == default)
                    {
                        return new LaunchSettingsApplyResult(false, LocalizableStrings.UsableLaunchProfileCannotBeLocated);
                    }

                    if (!profileObject.TryGetProperty(CommandNameKey, out var finalCommandNameElement)
                        || finalCommandNameElement.Type != JsonValueType.String)
                    {
                        return new LaunchSettingsApplyResult(false, LocalizableStrings.UsableLaunchProfileCannotBeLocated);
                    }

                    string commandName = finalCommandNameElement.GetString();
                    if (!TryLocateHandler(commandName, out ILaunchSettingsProvider provider))
                    {
                        return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.LaunchProfileHandlerCannotBeLocated, commandName));
                    }

                    return provider.TryApplySettings(profileObject, ref command);
                }
            }
            catch (JsonException ex)
            {
                return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.DeserializationExceptionMessage, ex.Message));
            }
        }

        private static bool TryLocateHandler(string commandName, out ILaunchSettingsProvider provider)
        {
            return _providers.TryGetValue(commandName, out provider);
        }

        private static bool IsDefaultProfileType(JsonProperty profileProperty)
        {
            if (profileProperty.Value.Type != JsonValueType.Object
                || !profileProperty.Value.TryGetProperty(CommandNameKey, out var commandNameElement)
                || commandNameElement.Type != JsonValueType.String)
            {
                return false;
            }

            return string.Equals(commandNameElement.GetString(), DefaultProfileCommandName, StringComparison.Ordinal);
        }
    }
}
