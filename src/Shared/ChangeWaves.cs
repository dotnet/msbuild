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

        private static bool ShouldApplyChangeWave
        {
            get
            {
                return ConversionState == ChangeWaveConversionState.NotConvertedYet || _cachedWave == null;
            }
        }

        private static Version _cachedWave;
        public static Version DisabledWave
        {
            get
            {
                if (ShouldApplyChangeWave)
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
            // Once set, change wave should not need to be set again.
            if (!ShouldApplyChangeWave)
            {
                return;
            }

            // Is `MSBuildDisableFeaturesFromVersion` not set?
            if (string.IsNullOrEmpty(Traits.Instance.MSBuildDisableFeaturesFromVersion))
            {
                ConversionState = ChangeWaveConversionState.Valid;
                _cachedWave = ChangeWaves.EnableAllFeatures;
                return;
            }
            // If _cachedWave hasn't been set yet, try to parse it
            else if (_cachedWave == null && !Version.TryParse(Traits.Instance.MSBuildDisableFeaturesFromVersion, out _cachedWave))
            {
                ConversionState = ChangeWaveConversionState.InvalidFormat;
                _cachedWave = ChangeWaves.EnableAllFeatures;
                return;
            }
            // Are we enabling everything?
            else if (_cachedWave == EnableAllFeatures)
            {
                ConversionState = ChangeWaveConversionState.Valid;
                return;
            }
            // Do we have a pre-existing wave?
            else if (AllWaves.Contains(_cachedWave))
            {
                ConversionState = ChangeWaveConversionState.Valid;
                return;
            }
            // If the version is out of rotation, log a warning and clamp the value.
            else if (_cachedWave < LowestWave)
            {
                ConversionState = ChangeWaveConversionState.OutOfRotation;
                _cachedWave = LowestWave;
                return;
            }
            else if (_cachedWave > HighestWave)
            {
                ConversionState = ChangeWaveConversionState.OutOfRotation;
                _cachedWave = HighestWave;
                return;
            }

            // What we know about _cachedWave at this point:
            // 1. It's a valid wave
            // 2. It's within the bounds of the lowest and highest
            // 3. Is not a pre-existing wave
            // Therefore:
            // There is guaranteed to be *at least* one wave larger than whatever _cachedWave is.
            _cachedWave = AllWaves.Where((x) => x > _cachedWave).First();
            ConversionState = ChangeWaveConversionState.Valid;
        }

        /// <summary>
        /// Compares the passed wave to the MSBuildDisableFeaturesFromVersion environment variable.
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static bool AreFeaturesEnabled(Version wave)
        {
            if (ShouldApplyChangeWave)
            {
                ApplyChangeWave();
            }

            // This is opt out behavior, all waves are enabled by default.
            if (_cachedWave == EnableAllFeatures)
            {
                return true;
            }

            return wave < _cachedWave;
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
