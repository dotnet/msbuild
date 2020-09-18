using Microsoft.Build.Shared;
using System;

namespace Microsoft.Build.Utilities
{
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
        /// Compares the passed wave to the MSBuildDisableFeaturesFromVersion environment variable.
        /// Version MUST be of the format: "xx.yy".
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the feature behind a change wave is enabled.</returns>
        public static bool IsChangeWaveEnabled(string wave)
        {
            Version waveToCheck;

            // When a caller passes an invalid waveToCheck, fail the build.
            ErrorUtilities.VerifyThrow(Version.TryParse(wave.ToString(), out waveToCheck),
                                       $"Argument 'wave' passed with invalid format." +
                                       $"Please use pre-existing const strings or define one with format 'xx.yy");

            return IsChangeWaveEnabled(waveToCheck);
        }

        /// <summary>
        /// Compares the passed wave to the MSBuildDisableFeaturesFromVersion environment variable.
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static bool IsChangeWaveEnabled(Version wave)
        {
            // This is opt out behavior, all waves are enabled by default.
            if (string.IsNullOrEmpty(Traits.Instance.MSBuildDisableFeaturesFromVersion))
            {
                return true;
            }

            Version currentSetWave;

            // If we can't parse the environment variable, default to enabling features.
            if (!Version.TryParse(Traits.Instance.MSBuildDisableFeaturesFromVersion, out currentSetWave))
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
        public static bool IsChangeWaveOutOfRotation(Version v)
        {
            return v != EnableAllFeaturesVersion && (v < LowestWaveVersion || v > HighestWaveVersion);
        }
    }
}
