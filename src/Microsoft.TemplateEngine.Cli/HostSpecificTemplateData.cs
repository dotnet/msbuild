// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Cli
{
    public class HostSpecificTemplateData
    {
        private static readonly string IsHiddenKey = "isHidden";
        private static readonly string LongNameKey = "longName";
        private static readonly string ShortNameKey = "shortName";

        public static HostSpecificTemplateData Default { get; } = new HostSpecificTemplateData();

        public HostSpecificTemplateData()
        {
            SymbolInfo = new Dictionary<string, Dictionary<string, string>>();
        }

        [JsonProperty]
        public List<string> UsageExamples { get; set; }

        [JsonProperty]
        public Dictionary<string, Dictionary<string, string>> SymbolInfo { get; }

        [JsonProperty]
        public bool IsHidden { get; set; }

        public HashSet<string> HiddenParameterNames
        {
            get
            {
                HashSet<string> hiddenNames = new HashSet<string>();
                foreach (KeyValuePair<string, Dictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(IsHiddenKey, out string hiddenStringValue)
                        && bool.TryParse(hiddenStringValue, out bool hiddenBoolValue)
                        && hiddenBoolValue)
                    {
                        hiddenNames.Add(paramInfo.Key);
                    }
                }

                return hiddenNames;
            }
        }

        public Dictionary<string, string> LongNameOverrides
        {
            get
            {
                Dictionary<string, string> map = new Dictionary<string, string>();

                foreach (KeyValuePair<string, Dictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(LongNameKey, out string longNameOverride))
                    {
                        map.Add(paramInfo.Key, longNameOverride);
                    }
                }

                return map;
            }
        }

        public Dictionary<string, string> ShortNameOverrides
        {
            get
            {
                Dictionary<string, string> map = new Dictionary<string, string>();

                foreach (KeyValuePair<string, Dictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(ShortNameKey, out string shortNameOverride))
                    {
                        map.Add(paramInfo.Key, shortNameOverride);
                    }
                }

                return map;
            }
        }

        public string DisplayNameForParameter(string parameterName)
        {
            if (SymbolInfo.TryGetValue(parameterName, out Dictionary<string, string> configForParam)
                && configForParam.TryGetValue(LongNameKey, out string longName))
            {
                return longName;
            }

            return parameterName;
        }
    }
}
