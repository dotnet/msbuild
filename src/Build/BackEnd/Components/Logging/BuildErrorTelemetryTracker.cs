// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Telemetry;
using static Microsoft.Build.Framework.Telemetry.BuildInsights;

#nullable enable

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Tracks and categorizes build errors for telemetry purposes.
    /// </summary>
    internal sealed class BuildErrorTelemetryTracker
    {
        // Use an enum internally for efficient tracking, convert to string only when needed
        internal enum ErrorCategory
        {
            Compiler,
            MSBuildGeneral,
            MSBuildEvaluation,
            MSBuildExecution,
            MSBuildGraph,
            Tasks,
            SDKResolvers,
            NETSDK,
            NuGet,
            BuildCheck,
            NativeToolchain,
            CodeAnalysis,
            Razor,
            WPF,
            AspNet,
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
        /// Tracks an error for telemetry purposes by categorizing it.
        /// </summary>
        /// <param name="errorCode">The error code from the BuildErrorEventArgs.</param>
        /// <param name="subcategory">The subcategory from the BuildErrorEventArgs.</param>
        public void TrackError(string? errorCode, string? subcategory)
        {
            // Categorize the error
            ErrorCategory category = CategorizeError(errorCode, subcategory);
            int categoryIndex = (int)category;

            // Increment the count for this category using Interlocked for thread safety
            int newCount = System.Threading.Interlocked.Increment(ref _errorCounts[categoryIndex]);

            // Update primary category if this one now has the highest count
            // Use a simple compare-and-swap pattern for thread-safe update
            int currentMax = System.Threading.Interlocked.CompareExchange(ref _primaryCategoryCount, 0, 0);
            if (newCount > currentMax)
            {
                // Try to update both the count and category atomically
                if (System.Threading.Interlocked.CompareExchange(ref _primaryCategoryCount, newCount, currentMax) == currentMax)
                {
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
            buildTelemetry.ErrorCounts = new ErrorCountsInfo(
                Compiler: GetCountOrNull(ErrorCategory.Compiler),
                MsBuildGeneral: GetCountOrNull(ErrorCategory.MSBuildGeneral),
                MsBuildEvaluation: GetCountOrNull(ErrorCategory.MSBuildEvaluation),
                MsBuildExecution: GetCountOrNull(ErrorCategory.MSBuildExecution),
                MsBuildGraph: GetCountOrNull(ErrorCategory.MSBuildGraph),
                Task: GetCountOrNull(ErrorCategory.Tasks),
                SdkResolvers: GetCountOrNull(ErrorCategory.SDKResolvers),
                NetSdk: GetCountOrNull(ErrorCategory.NETSDK),
                NuGet: GetCountOrNull(ErrorCategory.NuGet),
                BuildCheck: GetCountOrNull(ErrorCategory.BuildCheck),
                NativeToolchain: GetCountOrNull(ErrorCategory.NativeToolchain),
                CodeAnalysis: GetCountOrNull(ErrorCategory.CodeAnalysis),
                Razor: GetCountOrNull(ErrorCategory.Razor),
                Wpf: GetCountOrNull(ErrorCategory.WPF),
                AspNet: GetCountOrNull(ErrorCategory.AspNet),
                Other: GetCountOrNull(ErrorCategory.Other));

            // Set the primary failure category
            if (_primaryCategoryCount > 0)
            {
                buildTelemetry.FailureCategory = _primaryCategory.ToString();
            }
        }

        /// <summary>
        /// Gets the error count for a category, returning null if zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int? GetCountOrNull(ErrorCategory category)
        {
            int count = System.Threading.Interlocked.CompareExchange(ref _errorCounts[(int)category], 0, 0);
            return count > 0 ? count : null;
        }

        /// <summary>
        /// Categorizes an error based on its error code and subcategory.
        /// Uses a two-level character switch for O(1) prefix matching.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ErrorCategory CategorizeError(string? errorCode, string? subcategory)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                return ErrorCategory.Other;
            }

            // Check subcategory for compiler errors (CS*, VBC*, FS*)
            if (!string.IsNullOrEmpty(subcategory) && IsCompilerPrefix(subcategory!))
            {
                return ErrorCategory.Compiler;
            }

            // Check error code patterns
            if (IsCompilerPrefix(errorCode!))
            {
                return ErrorCategory.Compiler;
            }

            if (errorCode!.Length < 2)
            {
                return ErrorCategory.Other;
            }

            // Two-level switch on first two characters for efficient prefix matching
            char c0 = char.ToUpperInvariant(errorCode[0]);
            char c1 = char.ToUpperInvariant(errorCode[1]);

            return (c0, c1) switch
            {
                // A*
                ('A', 'S') when StartsWithASP(errorCode) => ErrorCategory.AspNet,

                // B*
                ('B', 'C') => ErrorCategory.BuildCheck,
                ('B', 'L') => ErrorCategory.AspNet,  // Blazor

                // C* (careful: CS is handled by IsCompilerPrefix above)
                ('C', 'A') => ErrorCategory.CodeAnalysis,
                ('C', 'L') => ErrorCategory.NativeToolchain,
                ('C', 'V') when errorCode.Length >= 3 && char.ToUpperInvariant(errorCode[2]) == 'T' => ErrorCategory.NativeToolchain,  // CVT*
                ('C', >= '0' and <= '9') => ErrorCategory.NativeToolchain,  // C1*, C2*, C4* (C/C++ compiler)

                // I*
                ('I', 'D') when errorCode.Length >= 3 && char.ToUpperInvariant(errorCode[2]) == 'E' => ErrorCategory.CodeAnalysis,  // IDE*

                // L*
                ('L', 'N') when errorCode.Length >= 3 && char.ToUpperInvariant(errorCode[2]) == 'K' => ErrorCategory.NativeToolchain,  // LNK*

                // M*
                ('M', 'C') => ErrorCategory.WPF,  // MC* (Markup Compiler)
                ('M', 'S') when errorCode.Length >= 3 && char.ToUpperInvariant(errorCode[2]) == 'B' => CategorizeMSBError(errorCode.AsSpan()),
                ('M', 'T') => ErrorCategory.NativeToolchain,  // MT* (Manifest Tool)

                // N*
                ('N', 'E') when StartsWithNETSDK(errorCode) => ErrorCategory.NETSDK,
                ('N', 'U') => ErrorCategory.NuGet,

                // R*
                ('R', 'C') => ErrorCategory.NativeToolchain,  // RC* (Resource Compiler)
                ('R', 'Z') => ErrorCategory.Razor,

                // X*
                ('X', 'C') => ErrorCategory.WPF,  // XC* (XAML Compiler)

                _ => ErrorCategory.Other
            };
        }

        /// <summary>
        /// Checks if the error code starts with "ASP" (case-insensitive).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool StartsWithASP(string errorCode)
            => errorCode.Length >= 3 && char.ToUpperInvariant(errorCode[2]) == 'P';

        /// <summary>
        /// Checks if the error code starts with "NETSDK" (case-insensitive).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool StartsWithNETSDK(string errorCode)
            => errorCode.Length >= 6 &&
               char.ToUpperInvariant(errorCode[2]) == 'T' &&
               char.ToUpperInvariant(errorCode[3]) == 'S' &&
               char.ToUpperInvariant(errorCode[4]) == 'D' &&
               char.ToUpperInvariant(errorCode[5]) == 'K';

        /// <summary>
        /// Checks if the string starts with a compiler error prefix (CS, FS, VBC).
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

            return (c0, c1) switch
            {
                ('C', 'S') => true,  // CS* -> C# compiler
                ('F', 'S') => true,  // FS* -> F# compiler
                ('V', 'B') => value.Length >= 3 && char.ToUpperInvariant(value[2]) == 'C',  // VBC* -> VB compiler
                _ => false
            };
        }

        /// <summary>
        /// Categorizes MSB error codes into granular MSBuild categories.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ErrorCategory CategorizeMSBError(ReadOnlySpan<char> codeSpan)
        {
            // MSB error codes: 3-letter prefix + 4-digit number (e.g., MSB3026)
            if (codeSpan.Length < 7 || !TryParseErrorNumber(codeSpan, out int errorNumber))
            {
                return ErrorCategory.Other;
            }

            return errorNumber switch
            {
                >= 3001 and <= 3999 => ErrorCategory.Tasks,
                >= 4001 and <= 4099 => ErrorCategory.MSBuildGeneral,
                >= 4100 and <= 4199 => ErrorCategory.MSBuildEvaluation,
                >= 4200 and <= 4299 => ErrorCategory.SDKResolvers,
                >= 4300 and <= 4399 => ErrorCategory.MSBuildExecution,
                >= 4400 and <= 4499 => ErrorCategory.MSBuildGraph,
                >= 4500 and <= 4999 => ErrorCategory.MSBuildGeneral,
                >= 5001 and <= 5999 => ErrorCategory.MSBuildExecution,
                >= 6001 and <= 6999 => ErrorCategory.MSBuildExecution,
                _ => ErrorCategory.Other
            };
        }

        /// <summary>
        /// Parses the 4-digit error number from an MSB error code span (e.g., "MSB3026" -> 3026).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseErrorNumber(ReadOnlySpan<char> codeSpan, out int errorNumber)
        {
            // Extract digits after "MSB" prefix (positions 3-6)
            ReadOnlySpan<char> digits = codeSpan.Slice(3, 4);
#if NET
            return int.TryParse(digits, out errorNumber);
#else
            return int.TryParse(digits.ToString(), out errorNumber);
#endif
        }
    }
}
