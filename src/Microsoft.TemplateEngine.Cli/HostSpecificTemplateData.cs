// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Cli
{
    internal class HostSpecificTemplateData
    {
        public static HostSpecificTemplateData Default { get; } = new HostSpecificTemplateData();

        public HostSpecificTemplateData()
        {
            HiddenParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ParameterMap = new Dictionary<string, string>();
        }

        [JsonProperty]
        public HashSet<string> HiddenParameters { get; }

        [JsonProperty]
        public Dictionary<string, string> ParameterMap { get; }
    }
}
