// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Test
{
    // !!! USED FEATURE NAMES MUST BE KEPT IN SYNC WITH https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.CoreUtilities/FeatureFlag/FeatureFlag.cs !!!
    internal class FeatureFlag
    {
        private readonly ConcurrentDictionary<string, bool> _cache = new();

        public static FeatureFlag Instance { get; } = new FeatureFlag();

        private FeatureFlag() { }

        private const string VSTEST_ = nameof(VSTEST_);

        // Only check the env variable once, when it is not set or is set to 0, consider it unset. When it is anything else, consider it set.
        public bool IsSet(string featureFlag) => _cache.GetOrAdd(featureFlag, f => (Environment.GetEnvironmentVariable(f)?.Trim() ?? "0") != "0");

        public void PrintFlagFeatureState()
        {
            if (VSTestTrace.TraceEnabled)
            {
                foreach (KeyValuePair<string, bool> flag in _cache)
                {
                    VSTestTrace.SafeWriteTrace(() => $"Feature {flag.Key}: {IsSet(flag.Key)}");
                }
            }
        }

        // Added in TP 17.2-preview, .NET 7.0-preview, disables additional artifact post-processing,
        // such as combining code coverage files into one file.
        public const string DISABLE_ARTIFACTS_POSTPROCESSING = VSTEST_ + nameof(DISABLE_ARTIFACTS_POSTPROCESSING);
    }
}
