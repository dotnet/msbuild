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
        /// The feature is not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// The feature is available.
        /// </summary>
        Available,

        /// <summary>
        /// The feature is not available.
        /// </summary>
        NotAvailable,

        /// <summary>
        /// The feature is in preview.
        /// </summary>
        Preview,
    }

    /// <summary>
    /// This class is used to manage features.
    /// </summary>
    public static class Features
    {
        private static Dictionary<string, FeatureStatus> _featureStatusMap = new Dictionary<string, FeatureStatus>
        {
            // TODO: Fill in the dictionary with the features and their status
            { "Feature1", FeatureStatus.Available },
            { "Feature2", FeatureStatus.NotAvailable },
        };

        /// <summary>
        /// Checks if a feature is available or not.
        /// </summary>
        /// <param name="featureName">The name of the feature.</param>
        /// <returns>A feature status <see cref="FeatureStatus"/>.</returns>
        public static FeatureStatus CheckFeatureAvailability(string featureName)
        {
            return _featureStatusMap.TryGetValue(featureName, out FeatureStatus status) ?
                 status : FeatureStatus.NotFound;
        }
    }
}
