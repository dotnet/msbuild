// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class LaunchSettingsProfile
    {
        [JsonIgnore]
        public string? LaunchProfileName { get; set; }
        public string? ApplicationUrl { get; init; }
        public string? CommandName { get; init; }
        public bool LaunchBrowser { get; init; }
        public string? LaunchUrl { get; init; }
        public string? CommandLineArgs { get; init; }
        public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

        internal static LaunchSettingsProfile? ReadLaunchProfile(string projectDirectory, string? launchProfileName, IReporter reporter)
        {
            var launchSettingsPath = Path.Combine(projectDirectory, "Properties", "launchSettings.json");
            if (!File.Exists(launchSettingsPath))
            {
                return null;
            }

            LaunchSettingsJson? launchSettings;
            try
            {
                launchSettings = JsonSerializer.Deserialize<LaunchSettingsJson>(
                    File.ReadAllText(launchSettingsPath),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (Exception ex)
            {
                reporter.Verbose($"Error reading launchSettings.json: {ex}.");
                return null;
            }

            if (string.IsNullOrEmpty(launchProfileName))
            {
                // Load the default (first) launch profile
                return ReadDefaultLaunchProfile(launchSettings, reporter);
            }

            // Load the specified launch profile
            var namedProfile = launchSettings?.Profiles?.FirstOrDefault(kvp =>
                string.Equals(kvp.Key, launchProfileName, StringComparison.Ordinal)).Value;

            if (namedProfile is null)
            {
                reporter.Warn($"Unable to find launch profile with name '{launchProfileName}'. Falling back to default profile.");

                // Check if a case-insensitive match exists
                var caseInsensitiveNamedProfile = launchSettings?.Profiles?.FirstOrDefault(kvp =>
                    string.Equals(kvp.Key, launchProfileName, StringComparison.OrdinalIgnoreCase)).Key;

                if (caseInsensitiveNamedProfile is not null)
                {
                    reporter.Warn($"Note: Launch profile names are case-sensitive. Did you mean '{caseInsensitiveNamedProfile}'?");
                }

                return ReadDefaultLaunchProfile(launchSettings, reporter);
            }

            reporter.Verbose($"Found named launch profile '{launchProfileName}'.");
            namedProfile.LaunchProfileName = launchProfileName;
            return namedProfile;
        }

        private static LaunchSettingsProfile? ReadDefaultLaunchProfile(LaunchSettingsJson? launchSettings, IReporter reporter)
        {
            if (launchSettings is null || launchSettings.Profiles is null)
            {
                reporter.Verbose("Unable to find default launch profile.");
                return null;
            }

            var defaultProfileKey = launchSettings.Profiles.FirstOrDefault(f => f.Value.CommandName == "Project").Key;
            var defaultProfile = launchSettings.Profiles[defaultProfileKey];
            defaultProfile.LaunchProfileName = defaultProfileKey;
            return defaultProfile;
        }

        internal class LaunchSettingsJson
        {
            public Dictionary<string, LaunchSettingsProfile>? Profiles { get; set; }
        }
    }
}
