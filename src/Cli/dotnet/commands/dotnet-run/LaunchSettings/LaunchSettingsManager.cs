// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics.CodeAnalysis;
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

        public static LaunchSettingsApplyResult TryApplyLaunchSettings(string launchSettingsJsonContents, string? profileName = null)
        {
            try
            {
                var jsonDocumentOptions = new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                };

                using (var document = JsonDocument.Parse(launchSettingsJsonContents, jsonDocumentOptions))
                {
                    var model = document.RootElement;

                    if (model.ValueKind != JsonValueKind.Object || !model.TryGetProperty(ProfilesKey, out var profilesObject) || profilesObject.ValueKind != JsonValueKind.Object)
                    {
                        return new LaunchSettingsApplyResult(false, LocalizableStrings.LaunchProfilesCollectionIsNotAJsonObject);
                    }

                    var selectedProfileName = profileName;
                    JsonElement profileObject;
                    if (string.IsNullOrEmpty(profileName))
                    {
                        var firstProfileProperty = profilesObject.EnumerateObject().FirstOrDefault(IsDefaultProfileType);
                        selectedProfileName = firstProfileProperty.Value.ValueKind == JsonValueKind.Object ? firstProfileProperty.Name : null;
                        profileObject = firstProfileProperty.Value;
                    }
                    else // Find a profile match for the given profileName
                    {
                        IEnumerable<JsonProperty> caseInsensitiveProfileMatches = profilesObject
                            .EnumerateObject() // p.Name shouldn't fail, as profileObject enumerables here are only created from an existing JsonObject
                            .Where(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (caseInsensitiveProfileMatches.Count() > 1)
                        {
                            throw new GracefulException(LocalizableStrings.DuplicateCaseInsensitiveLaunchProfileNames,
                                String.Join(",\n", caseInsensitiveProfileMatches.Select(p => $"\t{p.Name}").ToArray()));
                        }
                        else if (!caseInsensitiveProfileMatches.Any())
                        {
                            return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.LaunchProfileDoesNotExist, profileName));
                        }
                        else
                        {
                            profileObject = profilesObject.GetProperty(caseInsensitiveProfileMatches.First().Name);
                        }

                        if (profileObject.ValueKind != JsonValueKind.Object)
                        {
                            return new LaunchSettingsApplyResult(false, LocalizableStrings.LaunchProfileIsNotAJsonObject);
                        }
                    }

                    if (profileObject.ValueKind == default)
                    {
                        foreach (var prop in profilesObject.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                if (prop.Value.TryGetProperty(CommandNameKey, out var commandNameElement) && commandNameElement.ValueKind == JsonValueKind.String)
                                {
                                    if (commandNameElement.GetString() is { } commandNameElementKey &&  _providers.ContainsKey(commandNameElementKey))
                                    {
                                        profileObject = prop.Value;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (profileObject.ValueKind == default)
                    {
                        return new LaunchSettingsApplyResult(false, LocalizableStrings.UsableLaunchProfileCannotBeLocated);
                    }

                    if (!profileObject.TryGetProperty(CommandNameKey, out var finalCommandNameElement)
                        || finalCommandNameElement.ValueKind != JsonValueKind.String)
                    {
                        return new LaunchSettingsApplyResult(false, LocalizableStrings.UsableLaunchProfileCannotBeLocated);
                    }

                    string? commandName = finalCommandNameElement.GetString();
                    if (!TryLocateHandler(commandName, out ILaunchSettingsProvider? provider))
                    {
                        return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.LaunchProfileHandlerCannotBeLocated, commandName));
                    }

                    return provider.TryGetLaunchSettings(selectedProfileName, profileObject);
                }
            }
            catch (JsonException ex)
            {
                return new LaunchSettingsApplyResult(false, string.Format(LocalizableStrings.DeserializationExceptionMessage, ex.Message));
            }
        }

        private static bool TryLocateHandler(string? commandName, [NotNullWhen(true)]out ILaunchSettingsProvider? provider)
        {
            if (commandName == null)
            {
                provider = null;
                return false;
            }

            return _providers.TryGetValue(commandName, out provider);
        }

        private static bool IsDefaultProfileType(JsonProperty profileProperty)
        {
            if (profileProperty.Value.ValueKind != JsonValueKind.Object
                || !profileProperty.Value.TryGetProperty(CommandNameKey, out var commandNameElement)
                || commandNameElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return string.Equals(commandNameElement.GetString(), DefaultProfileCommandName, StringComparison.Ordinal);
        }
    }
}
