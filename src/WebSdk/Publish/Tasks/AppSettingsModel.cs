// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
