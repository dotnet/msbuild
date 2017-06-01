using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                var model = JObject.Parse(launchSettingsJsonContents);
                var profilesObject = model[ProfilesKey] as JObject;

                if (profilesObject == null)
                {
                    return new LaunchSettingsApplyResult(false, LocalizableStrings.LaunchProfilesCollectionIsNotAJsonObject);
                }

                JObject profileObject;
                if (profileName == null)
                {
                    profileObject = profilesObject
                        .Properties()
                        .FirstOrDefault(IsDefaultProfileType)?.Value as JObject;
                }
                else
                {
                    profileObject = profilesObject[profileName] as JObject;

                    if (profileObject == null)
                    {
                        return new LaunchSettingsApplyResult(false, LocalizableStrings.LaunchProfileIsNotAJsonObject);
                    }
                }

                if (profileObject == null)
                {
                    foreach (var prop in profilesObject.Properties())
                    {
                        var profile = prop.Value as JObject;

                        if (profile != null)
                        {
                            var cmdName = profile[CommandNameKey]?.Value<string>();
                            if (_providers.ContainsKey(cmdName))
                            {
                                profileObject = profile;
                                break;
                            }
                        }
                    }
                }

                var commandName = profileObject?[CommandNameKey]?.Value<string>();

                if (profileObject == null)
                {
                    return new LaunchSettingsApplyResult(false, LocalizableStrings.UsableLaunchProfileCannotBeLocated);
                }

                if (!TryLocateHandler(commandName, out ILaunchSettingsProvider provider))
                {
                    return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.LaunchProfileHandlerCannotBeLocated, commandName));
                }

                return provider.TryApplySettings(model, profileObject, ref command);
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

        private static bool IsDefaultProfileType(JProperty profileProperty)
        {
            JObject profile = profileProperty.Value as JObject;
            var commandName = profile?[CommandNameKey]?.Value<string>();
            return string.Equals(commandName, DefaultProfileCommandName, StringComparison.Ordinal);
        }
    }
}
