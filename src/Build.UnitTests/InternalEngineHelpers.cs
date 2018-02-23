// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

namespace Microsoft.Build.Unittest
{
    internal class ConfigurableMockSdkResolver : SdkResolver
    {
        private readonly Dictionary<string, SdkResult> _resultMap;

        public ConcurrentDictionary<string, int> ResolvedCalls { get; } = new ConcurrentDictionary<string, int>();

        public ConfigurableMockSdkResolver(SdkResult result)
        {
            _resultMap = new Dictionary<string, SdkResult> { [result.Sdk.Name] = result };
        }

        public ConfigurableMockSdkResolver(Dictionary<string, SdkResult> resultMap)
        {
            _resultMap = resultMap;
        }

        public override string Name => nameof(ConfigurableMockSdkResolver);

        public override int Priority => int.MaxValue;

        public override Framework.SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
            ResolvedCalls.AddOrUpdate(sdkReference.Name, k => 1, (k, c) => c + 1);

            return _resultMap.TryGetValue(sdkReference.Name, out var result)
                ? new SdkResult(sdkReference, result.Path, result.Version, null)
                : null;
        }
    }
}
