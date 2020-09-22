// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;

namespace Microsoft.Build.Utilities
{
    internal enum ChangeWaveConversionState
    {
        Valid,
        InvalidFormat,
        OutOfRotation
    }

    /// <summary>
    /// All waves are enabled by default, meaning all features behind change waves are enabled.
    /// </summary>
    public class ChangeWaves
    {
        public static readonly string[] AllWaves = { Wave16_8, Wave16_10, Wave17_0 };
        public const string Wave16_8 = "16.8";
        public const string Wave16_10 = "16.10";
        public const string Wave17_0 = "17.0";

        /// <summary>
        /// Special value indicating that all features behind change-waves should be enabled.
        /// </summary>
        public const string EnableAllFeatures = "999.999";

        internal static readonly Version LowestWaveVersion = new Version(AllWaves[0]);
        internal static readonly Version HighestWaveVersion = new Version(AllWaves[AllWaves.Length - 1]);
        internal static readonly Version EnableAllFeaturesVersion = new Version(EnableAllFeatures);

        internal static string LowestWave
        {
            get
            {
                return AllWaves[0];
            }
        }

        internal static string HighestWave
        {
            get
            {
                return AllWaves[AllWaves.Length - 1];
            }
        }

        private static string cachedWave = null;

        public static string DisabledWave
        {
            get
            {
                if (cachedWave == null)
                {
                    cachedWave = Traits.Instance.MSBuildDisableFeaturesFromVersion;
                }

                return cachedWave;
            }
            set
            {
                cachedWave = value;
            }
        }

        /// <summary>
        /// Ensure the the environment variable MSBuildDisableFeaturesFromWave is set to a proper value.
        /// </summary>
        /// <returns> String representation of the set change wave. "999.999" if unset or invalid, and the lowest version in rotation if out of bounds. </returns>
        internal static string ApplyChangeWave(out ChangeWaveConversionState result)
        {
            Version changeWave;

            // If unset, enable all features.
            if (string.IsNullOrEmpty(DisabledWave))
            {
                result = ChangeWaveConversionState.Valid;
                return DisabledWave = ChangeWaves.EnableAllFeatures;
            }

            // If the user-set change wave is of invalid format, log a warning and enable all features.
            if (!Version.TryParse(DisabledWave, out changeWave))
            {
                result = ChangeWaveConversionState.InvalidFormat;
                return DisabledWave = ChangeWaves.EnableAllFeatures;
            }

            // If the change wave is out of rotation, log a warning and disable all features.
            else if (ChangeWaves.IsVersionOutOfRotation(changeWave))
            {
                result = ChangeWaveConversionState.OutOfRotation;
                return DisabledWave = ChangeWaves.AllWaves[0];
            }

            result = ChangeWaveConversionState.Valid;
            return DisabledWave = changeWave.ToString();
        }

        /// <summary>
        /// Compares the passed wave to the MSBuildDisableFeaturesFromVersion environment variable.
        /// Version MUST be of the format: "xx.yy".
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the feature behind a change wave is enabled.</returns>
        public static bool IsFeatureEnabled(string wave)
        {
            Version waveToCheck;

            // When a caller passes an invalid wave, fail the build.
            ErrorUtilities.VerifyThrow(Version.TryParse(wave.ToString(), out waveToCheck),
                                       $"Argument 'wave' passed with invalid format." +
                                       $"Please use pre-existing const strings or define one with format 'xx.yy");

            return IsFeatureEnabled(waveToCheck);
        }

        /// <summary>
        /// Compares the passed wave to the MSBuildDisableFeaturesFromVersion environment variable.
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static bool IsFeatureEnabled(Version wave)
        {
            // This is opt out behavior, all waves are enabled by default.
            if (string.IsNullOrEmpty(DisabledWave))
            {
                return true;
            }

            Version currentSetWave;

            // If we can't parse the environment variable, default to enabling features.
            if (!Version.TryParse(DisabledWave, out currentSetWave))
            {
                return true;
            }

            return wave < currentSetWave;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool IsVersionOutOfRotation(Version v)
        {
            return v != EnableAllFeaturesVersion && (v < LowestWaveVersion || v > HighestWaveVersion);
        }
    }
}
