using System.IO;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public static class AppSettingsTransform
    {
        private const string ConnectionStringsId = "ConnectionStrings";

        public static string GenerateDefaultAppSettingsJsonFile()
        {
            string tempFileFullPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string defaultAppSettingsJsonString = JsonConvert.SerializeObject(new
            {
                ConnectionStrings = new
                {
                    DefaultConnection = string.Empty
                }
            }, Formatting.Indented);

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
            JObject appSettingsJsonObject = JObject.Parse(appSettingsJsonContent);
            if (!appSettingsJsonObject.TryGetValue(ConnectionStringsId, out _))
            {
                appSettingsJsonObject[ConnectionStringsId] = new JObject();
            }

            foreach (ITaskItem destinationConnectionString in destinationConnectionStrings)
            {
                string key = destinationConnectionString.ItemSpec;
                string Value = destinationConnectionString.GetMetadata("Value");
                var connectionStringsObject = appSettingsJsonObject[ConnectionStringsId];
                if (connectionStringsObject != null)
                {
                    connectionStringsObject[key] = Value;
                }
            }

            File.WriteAllText(destinationAppSettingsFilePath, appSettingsJsonObject.ToString());
            return true;
        }
    }
}
