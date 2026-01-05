// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// The status of a feature.
    /// </summary>
    public enum FeatureStatus
    {
        /// <summary>
        /// The feature availability is not determined.
        /// </summary>
        Undefined,

        /// <summary>
        /// The feature is available.
        /// </summary>
        Available,

        /// <summary>
        /// The feature is not available.
        /// </summary>
        NotAvailable,

        /// <summary>
        /// The feature is in preview, subject to change API or behavior between releases.
        /// </summary>
        Preview,
    }

    /// <summary>
    /// This class is used to manage features.
    /// </summary>
    public static class Features
    {
        private static readonly Dictionary<string, FeatureStatus> _featureStatusMap = CreateDefaultFeatureStatusMap();

        private static Dictionary<string, FeatureStatus> CreateDefaultFeatureStatusMap()
        {
            return new Dictionary<string, FeatureStatus>
            {
                { "BuildCheck.Beta", FeatureStatus.Preview },
                { "EvaluationContext_SharedSDKCachePolicy", FeatureStatus.Available }, // EvaluationContext supports the SharingPolicy.SharedSDKCache flag.
                { "TerminalLogger_MultiLineHandler", FeatureStatus.Available }, // TerminalLogger has better explicit support for rendering multi-line messages
                // Add more features here.
            };
        }

        /// <summary>
        /// Checks if a feature is available or not.
        /// </summary>
        /// <param name="featureName">The name of the feature.</param>
        /// <returns>A feature status <see cref="FeatureStatus"/>.</returns>
        public static FeatureStatus CheckFeatureAvailability(string featureName)
        {
            // Special handling for CachePlugins feature - controlled via environment variable
            // This allows remote feature flags in VS to disable cache plugins by setting the environment variable
            if (featureName == "CachePlugins")
            {
                string? envValue = System.Environment.GetEnvironmentVariable("MSBUILD_CACHEPLUGINS_DISABLED");
                if (!string.IsNullOrEmpty(envValue) && 
                    (envValue.Equals("1", System.StringComparison.OrdinalIgnoreCase) || 
                     envValue.Equals("true", System.StringComparison.OrdinalIgnoreCase)))
                {
                    return FeatureStatus.NotAvailable;
                }
                return FeatureStatus.Available;
            }

            return _featureStatusMap.TryGetValue(featureName, out FeatureStatus status) ?
                 status : FeatureStatus.Undefined;
        }

#if DEBUG
        /// <summary>
        /// Sets the status of a feature. Used for testing only.
        /// </summary>
        /// <param name="featureName">The name of the feature.</param>
        /// <param name="status">The status to set.</param>
        internal static void SetFeatureAvailability(string featureName, FeatureStatus status)
        {
            _featureStatusMap[featureName] = status;
        }

        /// <summary>
        /// Resets the feature status map to its default state. Used for testing only.
        /// </summary>
        internal static void ResetFeatureStatusForTests()
        {
            _featureStatusMap.Clear();
            foreach (var kvp in CreateDefaultFeatureStatusMap())
            {
                _featureStatusMap[kvp.Key] = kvp.Value;
            }
        }
#endif
    }
}
