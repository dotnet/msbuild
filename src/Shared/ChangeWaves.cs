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
        /// Version MUST be of the format: "xx.yy.zz", else Version.TryParse will fail.
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
                // throw a warning or error stating the user set the environment variable
                // to an incorrectly formatted change wave

                return true;
            }

            Version waveToCheck;
            
            if (!Version.TryParse(wave.ToString(), out waveToCheck))
            {
                // throw a warning or error stating the caller input an incorrectly formatted change wave
                return true;
            }
            
            return waveToCheck < currentDisabledWave;
        }
    }
}
