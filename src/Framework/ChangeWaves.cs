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
    internal class ChangeWaves
    {
        internal static readonly Version Wave16_8 = new Version(16, 8);
        internal static readonly Version Wave16_10 = new Version(16, 10);
        internal static readonly Version Wave17_0 = new Version(17, 0);
        internal static readonly Version[] AllWaves = { Wave16_8, Wave16_10, Wave17_0 };

        /// <summary>
        /// Special value indicating that all features behind all Change Waves should be enabled.
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
        /// The current disabled wave.
        /// </summary>
        internal static Version DisabledWave
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
        /// Read from environment variable `MSBuildDisableFeaturesFromVersion`, correct it if required, cache it and its ConversionState.
        /// </summary>
        internal static void ApplyChangeWave()
        {
            // Once set, change wave should not need to be set again.
            string mSBuildDisableFeaturesFromVersion = Environment.GetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION");
            if (!ShouldApplyChangeWave)
            {
                return;
            }
            // Most common case, `MSBuildDisableFeaturesFromVersion` unset
            else if (string.IsNullOrEmpty(mSBuildDisableFeaturesFromVersion))
            {
                ConversionState = ChangeWaveConversionState.Valid;
                _cachedWave = ChangeWaves.EnableAllFeatures;
            }
            else if (_cachedWave == null && !Version.TryParse(mSBuildDisableFeaturesFromVersion, out _cachedWave))
            {
                ConversionState = ChangeWaveConversionState.InvalidFormat;
                _cachedWave = ChangeWaves.EnableAllFeatures;
            }
            else if (_cachedWave == EnableAllFeatures || AllWaves.Contains(_cachedWave))
            {
                ConversionState = ChangeWaveConversionState.Valid;
            }
            else if (_cachedWave < LowestWave)
            {
                ConversionState = ChangeWaveConversionState.OutOfRotation;
                _cachedWave = LowestWave;
            }
            else if (_cachedWave > HighestWave)
            {
                ConversionState = ChangeWaveConversionState.OutOfRotation;
                _cachedWave = HighestWave;
            }
            // _cachedWave is somewhere between valid waves, find the next valid version.
            else
            {
                _cachedWave = AllWaves.First((x) => x > _cachedWave);
                ConversionState = ChangeWaveConversionState.Valid;
            }
        }

        /// <summary>
        /// Determines whether features behind the given wave are enabled.
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the change wave is enabled.</returns>
        internal static bool AreFeaturesEnabled(Version wave)
        {
            ApplyChangeWave();

            return wave < _cachedWave;
        }

        /// <summary>
        /// Resets the state and value of the currently disabled version.
        /// Used for testing only.
        /// </summary>
        internal static void ResetStateForTests()
        {
            _cachedWave = null;
            _state = ChangeWaveConversionState.NotConvertedYet;
        }
    }
}
