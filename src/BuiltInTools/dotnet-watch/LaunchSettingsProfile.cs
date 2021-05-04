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

        internal static LaunchSettingsProfile? ReadDefaultProfile(string projectDirectory, IReporter reporter)
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

            var defaultProfile = launchSettings?.Profiles?.FirstOrDefault(f => f.Value.CommandName == "Project").Value;
            if (defaultProfile is null)
            {
                reporter.Verbose("Unable to find default launchSettings profile.");
                return null;
            }

            return defaultProfile;
        }

        internal class LaunchSettingsJson
        {
            public Dictionary<string, LaunchSettingsProfile>? Profiles { get; set; }
        }
    }
}
