// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Build.Tasks
{
    internal class RuntimeOptions
    {
        public string Tfm { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string RollForward { get; set; }

        public RuntimeConfigFramework Framework { get; set; }

        public List<RuntimeConfigFramework> Frameworks { get; set; }

        public List<RuntimeConfigFramework> IncludedFrameworks { get; set; }

        public List<string> AdditionalProbingPaths { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> RawOptions { get; } = new Dictionary<string, JToken>();

        public RuntimeOptions()
        {
        }
    }
}
