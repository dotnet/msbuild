// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Test
{
    // !!! FEATURES MUST BE KEPT IN SYNC WITH https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.CoreUtilities/FeatureFlag/FeatureFlag.cs !!!
    internal class FeatureFlag
    {
        private const string Prefix = "VSTEST_FEATURE_";

        public Dictionary<string, bool> FeatureFlags { get; } = new();

        public static FeatureFlag Default { get; } = new FeatureFlag();

        public FeatureFlag()
        {
            FeatureFlags.Add(ARTIFACTS_POSTPROCESSING, false);
        }

        // Added for artifact porst-processing, it enable/disable the post processing.
        // Added in 17.2-preview 7.0-preview
        public const string ARTIFACTS_POSTPROCESSING = Prefix + "ARTIFACTS_POSTPROCESSING";

        // For now we're checking env var.
        // We could add it also to some section inside the runsettings.
        public bool IsEnabled(string featureName) =>
            int.TryParse(Environment.GetEnvironmentVariable(featureName), out int enabled)
                ? enabled == 1
                : FeatureFlags.TryGetValue(featureName, out bool isEnabled) && isEnabled;

        public void PrintFlagFeatureState()
        {
            if (VSTestTrace.TraceEnabled)
            {
                foreach (KeyValuePair<string, bool> flag in FeatureFlags)
                {
                    VSTestTrace.SafeWriteTrace(() => $"Feature {flag.Key}: {IsEnabled(flag.Key)}");
                }
            }
        }
    }
}
