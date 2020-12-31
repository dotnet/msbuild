// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
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
    public class ProxyTargets: ITranslatable
    {
        private Dictionary<string, string> _proxyTargetToRealTargetMap = null!;

        /// <summary>
        /// Mapping from proxy targets to real targets. Case insensitive.
        /// </summary>
        public IReadOnlyDictionary<string, string> ProxyTargetToRealTargetMap => _proxyTargetToRealTargetMap;

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
            ((ITranslatable) instance).Translate(translator);

            return instance;
        }
    }
}
