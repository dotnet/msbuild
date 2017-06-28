using System.IO;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public static class AppSettingsTransform
    {
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

            if (destinationConnectionStrings == null || destinationAppSettingsFilePath.Length == 0)
            {
                return false;
            }

            string appSettingsJsonContent = File.ReadAllText(destinationAppSettingsFilePath);
            JObject appSettingsJsonObject = JObject.Parse(appSettingsJsonContent);

            foreach (ITaskItem destinationConnectionString in destinationConnectionStrings)
            {
                string key = destinationConnectionString.ItemSpec;
                string Value = destinationConnectionString.GetMetadata("Value");
                appSettingsJsonObject["ConnectionStrings"][key] = Value;
            }

            File.WriteAllText(destinationAppSettingsFilePath, appSettingsJsonObject.ToString());
            return true;
        }
    }
}
