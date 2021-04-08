// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Cli
{
    public class HostSpecificTemplateData
    {
        private const string IsHiddenKey = "isHidden";
        private const string LongNameKey = "longName";
        private const string ShortNameKey = "shortName";
        private const string AlwaysShowKey = "alwaysShow";

        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _symbolInfo;

        internal HostSpecificTemplateData()
        {
            _symbolInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        }

        internal HostSpecificTemplateData(Dictionary<string, IReadOnlyDictionary<string, string>> symbolInfo)
        {
            _symbolInfo = symbolInfo;
        }

        [JsonProperty]
        public List<string>? UsageExamples { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SymbolInfo => _symbolInfo;

        [JsonProperty]
        public bool IsHidden { get; set; }

        public HashSet<string> HiddenParameterNames
        {
            get
            {
                HashSet<string> hiddenNames = new HashSet<string>();
                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> paramInfo in SymbolInfo)
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

        public HashSet<string> ParametersToAlwaysShow
        {
            get
            {
                HashSet<string> parametersToAlwaysShow = new HashSet<string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(AlwaysShowKey, out string alwaysShowValue)
                        && bool.TryParse(alwaysShowValue, out bool alwaysShowBoolValue)
                        && alwaysShowBoolValue)
                    {
                        parametersToAlwaysShow.Add(paramInfo.Key);
                    }
                }

                return parametersToAlwaysShow;
            }
        }

        public Dictionary<string, string> LongNameOverrides
        {
            get
            {
                Dictionary<string, string> map = new Dictionary<string, string>();

                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> paramInfo in SymbolInfo)
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

                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(ShortNameKey, out string shortNameOverride))
                    {
                        map.Add(paramInfo.Key, shortNameOverride);
                    }
                }

                return map;
            }
        }

        internal static HostSpecificTemplateData Default { get; } = new HostSpecificTemplateData();

        internal string DisplayNameForParameter(string parameterName)
        {
            if (SymbolInfo.TryGetValue(parameterName, out IReadOnlyDictionary<string, string> configForParam)
                && configForParam.TryGetValue(LongNameKey, out string longName))
            {
                return longName;
            }

            return parameterName;
        }
    }
}
