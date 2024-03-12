// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private static readonly Dictionary<string, FeatureStatus> _featureStatusMap = new Dictionary<string, FeatureStatus>
        {
            // Fill in the dictionary with the features and their status
            { "BuildCop.Beta", FeatureStatus.Preview }
        };

        /// <summary>
        /// Checks if a feature is available or not.
        /// </summary>
        /// <param name="featureName">The name of the feature.</param>
        /// <returns>A feature status <see cref="FeatureStatus"/>.</returns>
        public static FeatureStatus CheckFeatureAvailability(string featureName)
        {
            return _featureStatusMap.TryGetValue(featureName, out FeatureStatus status) ?
                 status : FeatureStatus.Undefined;
        }
    }
}
