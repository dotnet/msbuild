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
        public static readonly Version Wave16_8 = new Version(16, 8);
        public static readonly Version Wave16_10 = new Version(16, 10);
        public static readonly Version Wave17_0 = new Version(17, 0);
        public static readonly Version[] AllWaves = { Wave16_8, Wave16_10, Wave17_0 };

        /// <summary>
        /// Special value indicating that all features behind change-waves should be enabled.
        /// </summary>
        internal static readonly Version EnableAllFeatures = new Version(999, 999);

        internal static Version LowestWave
        {
            get
            {
                return AllWaves[0];
            }
        }

        internal static Version HighestWave
        {
            get
            {
                return AllWaves[AllWaves.Length - 1];
            }
        }

        private static Version _cachedWave;
        public static Version DisabledWave
        {
            get
            {
                if (ConversionState == ChangeWaveConversionState.NotConvertedYet)
                {
                    ApplyChangeWave();
                }

                return _cachedWave;
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
        /// Read from environment variable MSBuildDisableFeaturesFromWave is and cache the version properly.
        /// </summary>
        internal static void ApplyChangeWave()
        {
            // Most common scenarios, no set change wave version or set to EnableAllFeatures
            if (string.IsNullOrEmpty(Traits.Instance.MSBuildDisableFeaturesFromVersion) || _cachedWave == EnableAllFeatures)
            {
                ConversionState = ChangeWaveConversionState.Valid;
                _cachedWave = ChangeWaves.EnableAllFeatures;
                return;
            }
            // If disabledwave hasn't been set yet, try to parse it
            else if (_cachedWave == null && !Version.TryParse(Traits.Instance.MSBuildDisableFeaturesFromVersion, out _cachedWave))
            {
                ConversionState = ChangeWaveConversionState.InvalidFormat;
                _cachedWave = ChangeWaves.EnableAllFeatures;
                return;
            }
            // If the version is out of rotation, log a warning and clamp the value.
            else if (_cachedWave != EnableAllFeatures && _cachedWave < LowestWave)
            {
                ConversionState = ChangeWaveConversionState.OutOfRotation;
                _cachedWave = LowestWave;
                return;
            }
            else if (_cachedWave != EnableAllFeatures && _cachedWave > HighestWave)
            {
                ConversionState = ChangeWaveConversionState.OutOfRotation;
                _cachedWave = HighestWave;
                return;
            }

            // Ensure it's set to an existing version within the current rotation
            if (!AllWaves.Contains(_cachedWave))
            {
                foreach (Version wave in AllWaves)
                {
                    if (wave > _cachedWave)
                    {
                        ConversionState = ChangeWaveConversionState.Valid;
                        _cachedWave = wave;
                        return;
                    }
                }
            }

            ConversionState = ChangeWaveConversionState.Valid;
        }

        /// <summary>
        /// Compares the passed wave to the MSBuildDisableFeaturesFromVersion environment variable.
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static bool AreFeaturesEnabled(Version wave)
        {
            if (_state == ChangeWaveConversionState.NotConvertedYet || DisabledWave == null)
            {
                ApplyChangeWave();
            }

            // This is opt out behavior, all waves are enabled by default.
            if (DisabledWave == EnableAllFeatures)
            {
                return true;
            }

            return wave < DisabledWave;
        }

        /// <summary>
        /// Resets the state and value of the currently disabled version.
        /// </summary>
        public static void ResetStateForTests()
        {
            _cachedWave = null;
            _state = ChangeWaveConversionState.NotConvertedYet;
        }
    }
}
