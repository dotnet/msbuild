// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Linq;

namespace Microsoft.Build.Utilities
{
    internal enum ChangeWaveConversionState
    {
        NotConvertedYet,
        Valid,
        InvalidFormat,
        OutOfRotation
    }

    /// <summary>
    /// All waves are enabled by default, meaning all features behind change wave versions are enabled.
    /// </summary>
    public class ChangeWaves
    {
        public static readonly string[] AllWaves = { Wave16_8, Wave16_10, Wave17_0 };
        public static readonly Version[] AllWavesAsVersion = Array.ConvertAll<string, Version>(AllWaves, Version.Parse);
        public const string Wave16_8 = "16.8";
        public const string Wave16_10 = "16.10";
        public const string Wave17_0 = "17.0";

        /// <summary>
        /// Special value indicating that all features behind change-waves should be enabled.
        /// </summary>
        public const string EnableAllFeatures = "999.999";

        internal static readonly Version LowestWaveAsVersion = new Version(AllWaves[0]);
        internal static readonly Version HighestWaveAsVersion = new Version(AllWaves[AllWaves.Length - 1]);
        internal static readonly Version EnableAllFeaturesAsVersion = new Version(EnableAllFeatures);

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
                    cachedWave = Traits.Instance.MSBuildDisableFeaturesFromVersion ?? "";
                }

                return cachedWave;
            }
            set
            {
                cachedWave = value;
            }
        }

        private static ChangeWaveConversionState _state;
        internal static ChangeWaveConversionState ConversionState
        {
            get
            {
                return _state;
            }
            set
            {
                // Keep state persistent.
                if (_state == ChangeWaveConversionState.NotConvertedYet)
                {
                    _state = value;
                }
            }
        }

        /// <summary>
        /// Ensure the the environment variable MSBuildDisableFeaturesFromWave is set to a proper value.
        /// </summary>
        /// <returns> String representation of the set change wave. "999.999" if unset or invalid, and clamped if out of bounds. </returns>
        internal static void ApplyChangeWave()
        {
            Version changeWave;

            // If unset, enable all features.
            if (DisabledWave.Length == 0 || DisabledWave.Equals(EnableAllFeatures, StringComparison.OrdinalIgnoreCase))
            {
                ConversionState = ChangeWaveConversionState.Valid;
                DisabledWave = ChangeWaves.EnableAllFeatures;
                return;
            }

            // If the version is of invalid format, log a warning and enable all features.
            if (!Version.TryParse(DisabledWave, out changeWave))
            {
                ConversionState = ChangeWaveConversionState.InvalidFormat;
                DisabledWave = ChangeWaves.EnableAllFeatures;
                return;
            }
            // If the version is 999.999, we're done.
            else if (changeWave == EnableAllFeaturesAsVersion)
            {
                ConversionState = ChangeWaveConversionState.Valid;
                DisabledWave = changeWave.ToString();
                return;
            }
            // If the version is out of rotation, log a warning and clamp the value.
            else if (changeWave < LowestWaveAsVersion)
            {
                ConversionState = ChangeWaveConversionState.OutOfRotation;
                DisabledWave = LowestWave;
                return;
            }
            else if (changeWave > HighestWaveAsVersion)
            {
                ConversionState = ChangeWaveConversionState.OutOfRotation;
                DisabledWave = HighestWave;
                return;
            }

            // Ensure it's set to an existing version within the current rotation
            if (!AllWavesAsVersion.Contains(changeWave))
            {
                foreach (Version wave in AllWavesAsVersion)
                {
                    if (wave > changeWave)
                    {
                        ConversionState = ChangeWaveConversionState.Valid;
                        DisabledWave = wave.ToString();
                        return;
                    }
                }
            }

            ConversionState = ChangeWaveConversionState.Valid;
            DisabledWave = changeWave.ToString();
        }

        /// <summary>
        /// Compares the passed wave to the MSBuildDisableFeaturesFromVersion environment variable.
        /// Version MUST be of the format: "xx.yy".
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the feature behind a version is enabled.</returns>
        public static bool AreFeaturesEnabled(string wave)
        {
            Version waveToCheck;

            // When a caller passes an invalid wave, fail the build.
            ErrorUtilities.VerifyThrow(Version.TryParse(wave.ToString(), out waveToCheck),
                                       $"Argument 'wave' passed with invalid format." +
                                       $"Please use pre-existing const strings or define one with format 'xx.yy");

            return AreFeaturesEnabled(waveToCheck);
        }

        /// <summary>
        /// Compares the passed wave to the MSBuildDisableFeaturesFromVersion environment variable.
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static bool AreFeaturesEnabled(Version wave)
        {
            if (_state == ChangeWaveConversionState.NotConvertedYet)
            {
                ApplyChangeWave();
            }

            // This is opt out behavior, all waves are enabled by default.
            if (DisabledWave.Length == 0 || DisabledWave.Equals(EnableAllFeatures, StringComparison.OrdinalIgnoreCase))
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
        /// Resets the state and value of the currently disabled version.
        /// </summary>
        public static void ResetStateForTests()
        {
            DisabledWave = null;
            _state = ChangeWaveConversionState.NotConvertedYet;
        }
    }
}
