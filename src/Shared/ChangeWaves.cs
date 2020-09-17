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

        /// <summary>
        /// Special value indicating that all features behind change-waves should be enabled.
        /// </summary>
        public const string EnableAllFeaturesBehindChangeWaves = "999.999";

        public static readonly string[] AllWaves = { Wave16_8, Wave16_10, Wave17_0 };

        internal static readonly Version LowestWaveVersion = new Version(AllWaves[0]);
        internal static readonly Version HighestWaveVersion = new Version(AllWaves[AllWaves.Length - 1]);
        internal static readonly Version EnableAllFeaturesVersion = new Version(EnableAllFeaturesBehindChangeWaves);

        public static string LowestWave
        {
            get
            {
                return AllWaves[0];
            }
        }

        public static string HighestWave
        {
            get
            {
                return AllWaves[AllWaves.Length - 1];
            }
        }





        /// <summary>
        /// Compares version against the MSBuildChangeWave environment variable.
        /// Version MUST be of the format: "xx.yy".
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static bool IsFeatureEnabled(string wave)
        {
            // This is opt out behavior, all waves are enabled by default.
            if (string.IsNullOrEmpty(Traits.Instance.MSBuildDisableChangeWaveVersion))
            {
                return true;
            }

            Version currentDisabledWave;

            // If we can't parse the environment variable, default to enabling features.
            if (!Version.TryParse(Traits.Instance.MSBuildDisableChangeWaveVersion, out currentDisabledWave))
            {
                return true;
            }

            Version waveToCheck;

            // When a caller passes an invalid waveToCheck, fail the build.
            ErrorUtilities.VerifyThrow(Version.TryParse(wave.ToString(), out waveToCheck),
                                       $"Argument 'wave' passed with invalid format." +
                                       $"Please use pre-existing const strings or define one with format 'xx.yy");

            return waveToCheck < currentDisabledWave ? true : false;
        }

        public static bool IsChangeWaveOutOfRotation(Version v)
        {
            return v != EnableAllFeaturesVersion && (v < LowestWaveVersion || v > HighestWaveVersion);
        }
    }
}
