// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    /// Coupled together with the MSBUILDDISABLEFEATURESFROMVERSION environment variable,
    /// this class acts as a way to make risky changes while giving customers an opt-out.
    /// </summary>
    /// See docs here: https://github.com/dotnet/msbuild/blob/master/documentation/wiki/ChangeWaves.md
    /// For dev docs: https://github.com/dotnet/msbuild/blob/master/documentation/wiki/ChangeWaves-Dev.md
    public class ChangeWaves
    {
        public static readonly Version Wave16_8 = new Version(16, 8);
        public static readonly Version Wave16_10 = new Version(16, 10);
        public static readonly Version Wave17_0 = new Version(17, 0);
        public static readonly Version[] AllWaves = { Wave16_8, Wave16_10, Wave17_0 };

        /// <summary>
        /// Special value indicating that all features behind Change Waves should be enabled.
        /// </summary>
        internal static readonly Version EnableAllFeatures = new Version(999, 999);

        /// <summary>
        /// The lowest wave in the current rotation of Change Waves.
        /// </summary>
        internal static Version LowestWave
        {
            get
            {
                return AllWaves[0];
            }
        }

        /// <summary>
        /// The highest wave in the current rotation of Change Waves.
        /// </summary>
        internal static Version HighestWave
        {
            get
            {
                return AllWaves[AllWaves.Length - 1];
            }
        }

        /// <summary>
        /// Checks the conditions for whether or not we want ApplyChangeWave to be called again.
        /// </summary>
        private static bool ShouldApplyChangeWave
        {
            get
            {
                return ConversionState == ChangeWaveConversionState.NotConvertedYet || _cachedWave == null;
            }
        }

        private static Version _cachedWave;

        /// <summary>
        /// The current disabled wave, typically set via the MSBUILDDISABLEFEATURESFROMVERSION environment variable.
        /// If MSBUILDDISABLEFEATURESFROMVERSION is unset, DisabledWave will default to '999.999', a special value indicating
        /// there is no disabled wave.
        /// </summary>
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

        /// <summary>
        /// The status of how the disabled wave was set.
        /// </summary>
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
        /// Read from environment variable MSBUILDDISABLEFEATURESFROMVERSION, cache it, and cache the conversion state.
        /// </summary>
        internal static void ApplyChangeWave()
        {
            // Once set, change wave should not need to be set again.
            if (!ShouldApplyChangeWave)
            {
                return;
            }

            // Is `MSBUILDDISABLEFEATURESFROMVERSION` not set?
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
            // Are we enabling everything, or do we have a valid wave?
            else if (_cachedWave == EnableAllFeatures || AllWaves.Contains(_cachedWave))
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
        /// Determines whether features behind the given wave are enabled.
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the change wave is enabled.</returns>
        public static bool AreFeaturesEnabled(Version wave)
        {
            if (ShouldApplyChangeWave)
            {
                ApplyChangeWave();
            }

            // Check if we cached the special value to enable all features behind change waves.
            if (_cachedWave == EnableAllFeatures)
            {
                return true;
            }

            return wave < _cachedWave;
        }

        /// <summary>
        /// Resets the state and value of the currently disabled version.
        /// Used for testing only.
        /// </summary>
        public static void ResetStateForTests()
        {
            _cachedWave = null;
            _state = ChangeWaveConversionState.NotConvertedYet;
        }
    }
}
