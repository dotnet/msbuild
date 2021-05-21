// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli
{
    public class HostSpecificTemplateData
    {
        private const string IsHiddenKey = "isHidden";
        private const string LongNameKey = "longName";
        private const string ShortNameKey = "shortName";
        private const string AlwaysShowKey = "alwaysShow";

        internal HostSpecificTemplateData(JObject? jObject)
        {
            var symbolsInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            if (jObject == null)
            {
                SymbolInfo = symbolsInfo;
                return;
            }

            if (jObject.GetValue(nameof(UsageExamples), StringComparison.OrdinalIgnoreCase) is JArray usagesArray)
            {
                UsageExamples = new List<string>(usagesArray.Values<string>());
            }

            if (jObject.GetValue(nameof(SymbolInfo), StringComparison.OrdinalIgnoreCase) is JObject symbols)
            {
                foreach (var symbolInfo in symbols.Properties())
                {
                    if (!(symbolInfo.Value is JObject symbol))
                    {
                        continue;
                    }

                    var symbolProperties = new Dictionary<string, string>();

                    foreach (var symbolProperty in symbol.Properties())
                    {
                        symbolProperties[symbolProperty.Name] = symbolProperty.Value.Value<string>();
                    }

                    symbolsInfo[symbolInfo.Name] = symbolProperties;
                }
            }
            SymbolInfo = symbolsInfo;

            IsHidden = jObject.Value<bool>(nameof(IsHidden));

        }

        internal HostSpecificTemplateData(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> symbolInfo)
        {
            SymbolInfo = symbolInfo;
        }

        public List<string>? UsageExamples { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SymbolInfo { get; }

        public bool IsHidden { get; }

        public HashSet<string> HiddenParameterNames
        {
            get
            {
                HashSet<string> hiddenNames = new HashSet<string>();
                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> paramInfo in SymbolInfo)
                {
                    if (paramInfo.Value.TryGetValue(IsHiddenKey, out string? hiddenStringValue)
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
                    if (paramInfo.Value.TryGetValue(AlwaysShowKey, out string? alwaysShowValue)
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
                    if (paramInfo.Value.TryGetValue(LongNameKey, out string? longNameOverride))
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
                    if (paramInfo.Value.TryGetValue(ShortNameKey, out string? shortNameOverride))
                    {
                        map.Add(paramInfo.Key, shortNameOverride);
                    }
                }

                return map;
            }
        }

        internal static HostSpecificTemplateData Default { get; } = new HostSpecificTemplateData((JObject?)null);

        internal string DisplayNameForParameter(string parameterName)
        {
            if (SymbolInfo.TryGetValue(parameterName, out IReadOnlyDictionary<string, string>? configForParam)
                && configForParam.TryGetValue(LongNameKey, out string? longName))
            {
                return longName;
            }

            return parameterName;
        }
    }
}
