using Microsoft.Build.Shared;
using System;

namespace Microsoft.Build.Utilities
{
    public class ChangeWaves
    {
        public const string Wave16_8 = "16.8";
        public const string Wave16_10 = "16.10";
        public const string Wave17_0 = "17.0";

        /// <summary>
        /// Compares version against the MSBuildChangeWave environment variable.
        /// Version MUST be of the format: "xx.yy".
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static bool IsChangeWaveEnabled(string wave)
        {
            // This is opt out behavior, all waves are enabled by default.
            // If version is invalid, 
            if (string.IsNullOrEmpty(Traits.Instance.MSBuildChangeWave))
            {
                return true;
            }

            Version currentDisabledWave;

            if (!Version.TryParse(Traits.Instance.MSBuildChangeWave, out currentDisabledWave))
            {
                // how do I throw a warning here?
                return true;
            }

            Version waveToCheck;

            // When a caller passes an invalid waveToCheck, fail the build.
            ErrorUtilities.VerifyThrow(Version.TryParse(wave.ToString(), out waveToCheck),
                                       $"Argument 'wave' passed with invalid format." +
                                       $"Please use the const strings or define one with format 'xx.yy");

            return waveToCheck < currentDisabledWave;
        }
    }
}
