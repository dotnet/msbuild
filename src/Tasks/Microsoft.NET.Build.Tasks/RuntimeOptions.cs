// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Build.Tasks
{
    internal class RuntimeOptions
    {
        public string tfm { get; set; }

        public RuntimeConfigFramework Framework { get; set; }

        public List<RuntimeConfigFramework> Frameworks { get; set; }

        public List<string> AdditionalProbingPaths { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> RawOptions { get; } = new Dictionary<string, JToken>();

        public RuntimeOptions()
        {
        }
    }
}
