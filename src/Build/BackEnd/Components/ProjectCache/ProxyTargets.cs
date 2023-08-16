// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    ///     A cache hit can use this to instruct MSBuild to build the cheaper version of the targets that the plugin avoided
    ///     running.
    ///     For example, GetTargetPath is the cheaper version of Build.
    ///
    ///     MSBuild will build the proxy targets and assign their target results to the real targets the mapping points to.
    ///     The proxy targets are left in the build result (i.e., both GetTargetPath and Build will appear in the build result).
    ///     Real targets can be committed in which case msbuild only keeps the proxy target in the build result.
    /// </summary>
    public class ProxyTargets : ITranslatable
    {
        private Dictionary<string, string> _proxyTargetToRealTargetMap = null!;

        /// <summary>
        /// Mapping from proxy targets to real targets. Case insensitive.
        /// </summary>
        public IReadOnlyDictionary<string, string> ProxyTargetToRealTargetMap => _proxyTargetToRealTargetMap;

        internal IReadOnlyDictionary<string, string> RealTargetToProxyTargetMap
        {
            get
            {
                // The ProxyTargetToRealTargetMap is "backwards" from how most users would want to use it and doesn't provide as much flexibility as it could if reversed.
                // Unfortunately this is part of a public API so cannot easily change at this point.
                Dictionary<string, string> realTargetsToProxyTargets = new(ProxyTargetToRealTargetMap.Count, StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> kvp in ProxyTargetToRealTargetMap)
                {
                    // In the case of multiple proxy targets pointing to the same real target, the last one wins. Another awkwardness of ProxyTargetToRealTargetMap being "backwards".
                    realTargetsToProxyTargets[kvp.Value] = kvp.Key;
                }

                return realTargetsToProxyTargets;
            }
        }

        private ProxyTargets()
        {
        }

        public ProxyTargets(IReadOnlyDictionary<string, string> proxyTargetToRealTargetMap)
        {
            ErrorUtilities.VerifyThrowArgumentLength(proxyTargetToRealTargetMap, nameof(proxyTargetToRealTargetMap));

            _proxyTargetToRealTargetMap = proxyTargetToRealTargetMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.TranslateDictionary(ref _proxyTargetToRealTargetMap, StringComparer.OrdinalIgnoreCase);
        }

        internal static ProxyTargets FactoryForDeserialization(ITranslator translator)
        {
            var instance = new ProxyTargets();
            ((ITranslatable)instance).Translate(translator);

            return instance;
        }
    }
}
