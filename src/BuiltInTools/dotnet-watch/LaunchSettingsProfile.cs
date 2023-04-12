// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class LaunchSettingsProfile
    {
        public string? ApplicationUrl { get; set; }

        public string? CommandName { get; set; }

        public bool LaunchBrowser { get; set; }

        public string? LaunchUrl { get; set; }

        public IDictionary<string, string>? EnvironmentVariables { get; set; }

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
            return namedProfile;
        }

        private static LaunchSettingsProfile? ReadDefaultLaunchProfile(LaunchSettingsJson? launchSettings, IReporter reporter)
        {
            var defaultProfile = launchSettings?.Profiles?.FirstOrDefault(f => f.Value.CommandName == "Project").Value;

            if (defaultProfile is null)
            {
                reporter.Verbose("Unable to find default launch profile.");
            }

            return defaultProfile;
        }

        internal class LaunchSettingsJson
        {
            public Dictionary<string, LaunchSettingsProfile>? Profiles { get; set; }
        }
    }
}
