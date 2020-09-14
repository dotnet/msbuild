using Microsoft.Build.Shared;
using System;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// There may be some confusion between "enabling waves" and "enabling features".
    /// Enabling a wave DISABLES all features behind that wave.
    /// </summary>
    public class ChangeWaves
    {
        public const string Wave16_8 = "16.8";
        public const string Wave16_10 = "16.10";
        public const string Wave17_0 = "17.0";

        public const string LowestWave = Wave16_8;
        public const string HighestWave = Wave17_0;

        /// <summary>
        /// Special value indicating that all features behind change-waves should be enabled.
        /// </summary>
        public const string EnableAllFeaturesBehindChangeWaves = "999.999";

        internal static readonly Version LowestWaveVersion = new Version(LowestWave);
        internal static readonly Version HighestWaveVersion = new Version(HighestWave);
        internal static readonly Version EnableAllFeaturesVersion = new Version(EnableAllFeaturesBehindChangeWaves);

        /// <summary>
        /// Compares version against the MSBuildChangeWave environment variable.
        /// Version MUST be of the format: "xx.yy".
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static bool IsFeatureEnabled(string wave)
        {
            // This is opt out behavior, all waves are enabled by default.
            // If version is invalid, 
            if (string.IsNullOrEmpty(Traits.Instance.MSBuildDisableChangeWaveVersion))
            {
                return true;
            }

            Version currentDisabledWave;

            if (!Version.TryParse(Traits.Instance.MSBuildDisableChangeWaveVersion, out currentDisabledWave))
            {
                return true;
            }

            Version waveToCheck;

            // When a caller passes an invalid waveToCheck, fail the build.
            ErrorUtilities.VerifyThrow(Version.TryParse(wave.ToString(), out waveToCheck),
                                       $"Argument 'wave' passed with invalid format." +
                                       $"Please use the const strings or define one with format 'xx.yy");

            return waveToCheck < currentDisabledWave ? true : false;
        }

        public static bool IsChangeWaveOutOfRotation(Version v)
        {
            return v < LowestWaveVersion || v > HighestWaveVersion;
        }
    }
}
