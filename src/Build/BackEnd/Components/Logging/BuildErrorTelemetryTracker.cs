// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework.Telemetry;

#nullable disable

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Tracks and categorizes build errors for telemetry purposes.
    /// </summary>
    internal class BuildErrorTelemetryTracker
    {
        /// <summary>
        /// Tracks error counts by category for telemetry purposes.
        /// </summary>
        private readonly Dictionary<string, int> _errorCountsByCategory = new Dictionary<string, int>();

        /// <summary>
        /// Tracks the first error code encountered for telemetry purposes.
        /// </summary>
        private string _firstErrorCode;

        /// <summary>
        /// Lock object for error tracking.
        /// </summary>
        private readonly object _errorTrackingLock = new();

        /// <summary>
        /// Tracks an error for telemetry purposes by categorizing it.
        /// </summary>
        /// <param name="errorCode">The error code from the BuildErrorEventArgs.</param>
        /// <param name="subcategory">The subcategory from the BuildErrorEventArgs.</param>
        public void TrackError(string errorCode, string subcategory)
        {
            lock (_errorTrackingLock)
            {
                // Track the first error code encountered
                _firstErrorCode ??= errorCode;

                // Categorize the error
                string category = CategorizeError(errorCode, subcategory);

                // Increment the count for this category
                if (!_errorCountsByCategory.ContainsKey(category))
                {
                    _errorCountsByCategory[category] = 0;
                }
                _errorCountsByCategory[category]++;
            }
        }

        /// <summary>
        /// Populates build telemetry with error categorization data.
        /// </summary>
        /// <param name="buildTelemetry">The BuildTelemetry object to populate with error data.</param>
        public void PopulateBuildTelemetry(BuildTelemetry buildTelemetry)
        {
            lock (_errorTrackingLock)
            {
                buildTelemetry.FirstErrorCode = _firstErrorCode;

                if (_errorCountsByCategory.TryGetValue("Compiler", out int compilerCount))
                {
                    buildTelemetry.CompilerErrorCount = compilerCount;
                }

                if (_errorCountsByCategory.TryGetValue("MSBuildEngine", out int msbuildEngineCount))
                {
                    buildTelemetry.MSBuildEngineErrorCount = msbuildEngineCount;
                }

                if (_errorCountsByCategory.TryGetValue("Tasks", out int tasksCount))
                {
                    buildTelemetry.TaskErrorCount = tasksCount;
                }

                if (_errorCountsByCategory.TryGetValue("SDK", out int sdkCount))
                {
                    buildTelemetry.SDKErrorCount = sdkCount;
                }

                if (_errorCountsByCategory.TryGetValue("NuGet", out int nugetCount))
                {
                    buildTelemetry.NuGetErrorCount = nugetCount;
                }

                if (_errorCountsByCategory.TryGetValue("BuildCheck", out int buildCheckCount))
                {
                    buildTelemetry.BuildCheckErrorCount = buildCheckCount;
                }

                if (_errorCountsByCategory.TryGetValue("Other", out int otherCount))
                {
                    buildTelemetry.OtherErrorCount = otherCount;
                }

                // Set the primary failure category to the category with the highest error count
                if (_errorCountsByCategory.Count > 0)
                {
                    var primaryCategory = _errorCountsByCategory.OrderByDescending(kvp => kvp.Value).First().Key;
                    buildTelemetry.FailureCategory = primaryCategory;
                }
            }
        }

        /// <summary>
        /// Categorizes an error based on its error code and subcategory.
        /// </summary>
        private static string CategorizeError(string errorCode, string subcategory)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                return "Other";
            }

            // Check subcategory for compiler errors (CS*, VBC*, FS*)
            if (!string.IsNullOrEmpty(subcategory))
            {
                if (subcategory.StartsWith("CS", StringComparison.OrdinalIgnoreCase) ||
                    subcategory.StartsWith("VBC", StringComparison.OrdinalIgnoreCase) ||
                    subcategory.StartsWith("FS", StringComparison.OrdinalIgnoreCase))
                {
                    return "Compiler";
                }
            }

            // Check error code patterns
            if (errorCode.StartsWith("CS", StringComparison.OrdinalIgnoreCase) ||
                errorCode.StartsWith("VBC", StringComparison.OrdinalIgnoreCase) ||
                errorCode.StartsWith("FS", StringComparison.OrdinalIgnoreCase))
            {
                return "Compiler";
            }

            if (errorCode.StartsWith("BC", StringComparison.OrdinalIgnoreCase))
            {
                return "BuildCheck";
            }

            if (errorCode.StartsWith("NU", StringComparison.OrdinalIgnoreCase))
            {
                return "NuGet";
            }

            if (errorCode.StartsWith("NETSDK", StringComparison.OrdinalIgnoreCase))
            {
                return "SDK";
            }

            if (errorCode.StartsWith("MSB", StringComparison.OrdinalIgnoreCase))
            {
                // Check for specific SDK error first
                if (errorCode.Equals("MSB4236", StringComparison.OrdinalIgnoreCase))
                {
                    return "SDK";
                }

                // MSB error codes consist of 3-letter prefix + 4-digit number (e.g., MSB3026)
                const int MinimumMsbCodeLength = 7;

                // Extract the numeric part
                if (errorCode.Length >= MinimumMsbCodeLength && int.TryParse(errorCode.Substring(3, 4), out int errorNumber))
                {
                    if (errorNumber >= 4001 && errorNumber <= 4999)
                    {
                        return "MSBuildEngine";
                    }
                    else if (errorNumber >= 3001 && errorNumber <= 3999)
                    {
                        return "Tasks";
                    }
                }
            }

            return "Other";
        }
    }
}
