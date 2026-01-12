// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Telemetry;

#nullable enable

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Tracks and categorizes build errors for telemetry purposes.
    /// </summary>
    internal sealed class BuildErrorTelemetryTracker
    {
        // Use an enum internally for efficient tracking, convert to string only when needed
        private enum ErrorCategory
        {
            Compiler,
            MSBuildEngine,
            Tasks,
            SDK,
            NuGet,
            BuildCheck,
            Other,
        }

        /// <summary>
        /// Error counts by category index (using enum ordinal).
        /// </summary>
        private readonly int[] _errorCounts = new int[Enum.GetValues(typeof(ErrorCategory)).Length];

        /// <summary>
        /// Tracks the primary failure category (category with highest count).
        /// </summary>
        private ErrorCategory _primaryCategory;

        /// <summary>
        /// Tracks the highest error count for primary category determination.
        /// </summary>
        private int _primaryCategoryCount;

        /// <summary>
        /// Lock object for error tracking.
        /// </summary>
        private readonly LockType _errorTrackingLock = new();

        /// <summary>
        /// Tracks an error for telemetry purposes by categorizing it.
        /// </summary>
        /// <param name="errorCode">The error code from the BuildErrorEventArgs.</param>
        /// <param name="subcategory">The subcategory from the BuildErrorEventArgs.</param>
        public void TrackError(string? errorCode, string? subcategory)
        {
            lock (_errorTrackingLock)
            {
                // Categorize the error
                ErrorCategory category = CategorizeError(errorCode, subcategory);
                int categoryIndex = (int)category;

                // Increment the count for this category
                int newCount = ++_errorCounts[categoryIndex];

                // Update primary category if this one now has the highest count
                if (newCount > _primaryCategoryCount)
                {
                    _primaryCategoryCount = newCount;
                    _primaryCategory = category;
                }
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
                int compilerErrorCount = _errorCounts[(int)ErrorCategory.Compiler];
                if (compilerErrorCount > 0)
                {
                    buildTelemetry.CompilerErrorCount = compilerErrorCount;
                }

                int msbuildEngineErrorCount = _errorCounts[(int)ErrorCategory.MSBuildEngine];
                if (msbuildEngineErrorCount > 0)
                {
                    buildTelemetry.MSBuildEngineErrorCount = msbuildEngineErrorCount;
                }

                int taskErrorCount = _errorCounts[(int)ErrorCategory.Tasks];
                if (taskErrorCount > 0)
                {
                    buildTelemetry.TaskErrorCount = taskErrorCount;
                }

                int sdkErrorCount = _errorCounts[(int)ErrorCategory.SDK];
                if (sdkErrorCount > 0)
                {
                    buildTelemetry.SDKErrorCount = sdkErrorCount;
                }

                int nugetErrorCount = _errorCounts[(int)ErrorCategory.NuGet];
                if (nugetErrorCount > 0)
                {
                    buildTelemetry.NuGetErrorCount = nugetErrorCount;
                }

                int buildCheckErrorCount = _errorCounts[(int)ErrorCategory.BuildCheck];
                if (buildCheckErrorCount > 0)
                {
                    buildTelemetry.BuildCheckErrorCount = buildCheckErrorCount;
                }

                int otherErrorCount = _errorCounts[(int)ErrorCategory.Other];
                if (otherErrorCount > 0)
                {
                    buildTelemetry.OtherErrorCount = otherErrorCount;
                }
                if (_primaryCategoryCount > 0)
                {
                    buildTelemetry.FailureCategory = _primaryCategory.ToString();
                }
            }
        }

        /// <summary>
        /// Categorizes an error based on its error code and subcategory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ErrorCategory CategorizeError(string? errorCode, string? subcategory)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                return ErrorCategory.Other;
            }

            // Check subcategory for compiler errors (CS*, VBC*, FS*)
            if (!string.IsNullOrEmpty(subcategory) && IsCompilerPrefix(subcategory))
            {
                return ErrorCategory.Compiler;
            }

            // Check error code patterns - order by frequency for fast path
            if (IsCompilerPrefix(errorCode))
            {
                return ErrorCategory.Compiler;
            }

            // Use Span-based comparison to avoid allocations
            ReadOnlySpan<char> codeSpan = errorCode.AsSpan();

            if (codeSpan.Length >= 2)
            {
                char c0 = char.ToUpperInvariant(codeSpan[0]);
                char c1 = char.ToUpperInvariant(codeSpan[1]);

                // BC* -> BuildCheck
                if (c0 == 'B' && c1 == 'C')
                {
                    return ErrorCategory.BuildCheck;
                }

                // NU* -> NuGet
                if (c0 == 'N' && c1 == 'U')
                {
                    return ErrorCategory.NuGet;
                }

                // MSB* or NETSDK*
                if (c0 == 'M' && c1 == 'S' && codeSpan.Length >= 3 && char.ToUpperInvariant(codeSpan[2]) == 'B')
                {
                    return CategorizeMSBError(codeSpan);
                }

                if (c0 == 'N' && c1 == 'E' && codeSpan.Length >= 6 &&
                    char.ToUpperInvariant(codeSpan[2]) == 'T' &&
                    char.ToUpperInvariant(codeSpan[3]) == 'S' &&
                    char.ToUpperInvariant(codeSpan[4]) == 'D' &&
                    char.ToUpperInvariant(codeSpan[5]) == 'K')
                {
                    return ErrorCategory.SDK;
                }
            }

            return ErrorCategory.Other;
        }

        /// <summary>
        /// Checks if the string starts with a compiler error prefix (CS, VBC, FS).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCompilerPrefix(string value)
        {
            if (value.Length < 2)
            {
                return false;
            }

            char c0 = char.ToUpperInvariant(value[0]);
            char c1 = char.ToUpperInvariant(value[1]);

            // CS* -> C# compiler
            if (c0 == 'C' && c1 == 'S')
            {
                return true;
            }

            // FS* -> F# compiler
            if (c0 == 'F' && c1 == 'S')
            {
                return true;
            }

            // VBC* -> VB compiler (need 3 chars)
            if (c0 == 'V' && c1 == 'B' && value.Length >= 3 && char.ToUpperInvariant(value[2]) == 'C')
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Categorizes MSB error codes into MSBuildEngine, Tasks, or SDK.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ErrorCategory CategorizeMSBError(ReadOnlySpan<char> codeSpan)
        {
            // MSB error codes consist of 3-letter prefix + 4-digit number (e.g., MSB3026)
            const int MinimumMsbCodeLength = 7;

            if (codeSpan.Length < MinimumMsbCodeLength)
            {
                return ErrorCategory.Other;
            }

            // Check for MSB4236 (SDK error) - fast path for exact match
            if (codeSpan.Length == 7 && codeSpan[3] == '4' && codeSpan[4] == '2' && codeSpan[5] == '3' && codeSpan[6] == '6')
            {
                return ErrorCategory.SDK;
            }

            if (!TryParseErrorNumber(codeSpan, out int errorNumber))
            {
                return ErrorCategory.Other;
            }

            // MSB4xxx (except MSB4236, handled above as SDK) -> MSBuildEngine (evaluation and execution errors)
            if (errorNumber is >= 4001 and <= 4999)
            {
                return ErrorCategory.MSBuildEngine;
            }

            // MSB3xxx -> Tasks
            return errorNumber is >= 3001 and <= 3999 ? ErrorCategory.Tasks : ErrorCategory.Other;
        }

        /// <summary>
        /// Parses the 4-digit error number from an MSB error code span (starting at index 3).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseErrorNumber(ReadOnlySpan<char> codeSpan, out int errorNumber)
        {
            errorNumber = 0;

            // We need exactly 4 digits starting at position 3
            for (int i = 3; i < 7; i++)
            {
                char c = codeSpan[i];
                if (c < '0' || c > '9')
                {
                    return false;
                }

                errorNumber = (errorNumber * 10) + (c - '0');
            }

            return true;
        }
    }
}
