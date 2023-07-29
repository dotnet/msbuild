// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public static class AppSettingsTransform
    {
        public static string GenerateDefaultAppSettingsJsonFile()
        {
            string tempFileFullPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            string defaultAppSettingsJsonString = ToJson(new
            {
                ConnectionStrings = new
                {
                    DefaultConnection = string.Empty
                }
            });

            File.WriteAllText(tempFileFullPath, defaultAppSettingsJsonString);

            return tempFileFullPath;
        }

        public static bool UpdateDestinationConnectionStringEntries(string destinationAppSettingsFilePath, ITaskItem[] destinationConnectionStrings)
        {
            if (!File.Exists(destinationAppSettingsFilePath))
            {
                return false;
            }

            if (destinationConnectionStrings == null || destinationConnectionStrings.Length == 0)
            {
                return false;
            }

            string appSettingsJsonContent = File.ReadAllText(destinationAppSettingsFilePath);
            var appSettingsModel = FromJson<AppSettingsModel>(appSettingsJsonContent);
            if (appSettingsModel.ConnectionStrings == null)
            {
                appSettingsModel.ConnectionStrings = new Dictionary<string, string>();
            }

            foreach (ITaskItem destinationConnectionString in destinationConnectionStrings)
            {
                string key = destinationConnectionString.ItemSpec;
                string Value = destinationConnectionString.GetMetadata("Value");
                appSettingsModel.ConnectionStrings[key] = Value;
            }

            File.WriteAllText(destinationAppSettingsFilePath, ToJson(appSettingsModel));

            return true;
        }

        private static string ToJson<T>(T obj)
        {
            return JsonSerializer.Serialize(obj,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }

        private static T FromJson<T>(string jsonString)
        {
            return JsonSerializer.Deserialize<T>(jsonString,
                new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
        }
    }
}
