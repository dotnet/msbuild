// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;

#nullable disable

namespace Microsoft.Build.Framework
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
    /// See docs here: https://github.com/dotnet/msbuild/blob/main/documentation/wiki/ChangeWaves.md
    /// For dev docs: https://github.com/dotnet/msbuild/blob/main/documentation/wiki/ChangeWaves-Dev.md
    internal static class ChangeWaves
    {
        internal static readonly Version Wave17_4 = new Version(17, 4);
        internal static readonly Version Wave17_6 = new Version(17, 6);
        internal static readonly Version Wave17_8 = new Version(17, 8);
        internal static readonly Version Wave17_10 = new Version(17, 10);
        internal static readonly Version[] AllWaves = { Wave17_4, Wave17_6, Wave17_8, Wave17_10 };

        /// <summary>
        /// Special value indicating that all features behind all Change Waves should be enabled.
        /// </summary>
        internal static readonly Version EnableAllFeatures = new Version(999, 999);

#if DEBUG
        /// <summary>
        /// True if <see cref="ResetStateForTests"/> has been called.
        /// </summary>
        private static bool _runningTests = false;
#endif

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
        /// Read from environment variable `MSBUILDDISABLEFEATURESFROMVERSION`, correct it if required, cache it and its ConversionState.
        /// </summary>
        internal static void ApplyChangeWave()
        {
            // Once set, change wave should not need to be set again.
            if (!ShouldApplyChangeWave)
            {
                return;
            }

            string msbuildDisableFeaturesFromVersion = Environment.GetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION");

            // Most common case, `MSBUILDDISABLEFEATURESFROMVERSION` unset
            if (string.IsNullOrEmpty(msbuildDisableFeaturesFromVersion))
            {
                ConversionState = ChangeWaveConversionState.Valid;
                _cachedWave = ChangeWaves.EnableAllFeatures;
            }
            else if (!TryParseVersion(msbuildDisableFeaturesFromVersion, out _cachedWave))
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

#if DEBUG
            Debug.Assert(_runningTests || AllWaves.Contains(wave), $"Change wave version {wave} is invalid");
#endif

            return wave < _cachedWave;
        }

        /// <summary>
        /// Resets the state and value of the currently disabled version.
        /// Used for testing only.
        /// </summary>
        internal static void ResetStateForTests()
        {
#if DEBUG
            _runningTests = true;
#endif
            _cachedWave = null;
            _state = ChangeWaveConversionState.NotConvertedYet;
        }

        private static bool TryParseVersion(string stringVersion, out Version version)
        {
#if FEATURE_NET35_TASKHOST
            try
            {
                version = new Version(stringVersion);
                return true;
            }
            catch (Exception)
            {
                version = null;
                return false;
            }
#else
            return Version.TryParse(stringVersion, out version);
#endif
        }
    }
}
