using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public partial class AppSettingsModel
    {
        [JsonPropertyName("ConnectionStrings")]
        public IDictionary<string, string> ConnectionStrings { get; set; }

        [JsonExtensionDataAttribute]
        public IDictionary<string, object> ExtensionData { get; set; }
    }
}
