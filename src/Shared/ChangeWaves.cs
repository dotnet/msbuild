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
        /// <param name="version">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static bool IsChangeWaveEnabled(string version)
        {
            // This is opt out behavior, all waves are enabled by default.
            if (string.IsNullOrEmpty(Traits.Instance.MSBuildChangeWave))
            {
                return true;
            }

            Version currentEnabledWave;

            if (!Version.TryParse(Traits.Instance.MSBuildChangeWave, out currentEnabledWave))
            {
                // throw a warning or error stating the user set the environment variable
                // to an incorrectly formatted change wave

                return true;
            }

            Version versionToCheck;
            
            if (!Version.TryParse(version.ToString(), out versionToCheck))
            {
                // throw a warning or error stating the caller input an incorrectly formatted change wave

                return true;
            }

            bool isEnabled = versionToCheck.CompareTo(currentEnabledWave) <= 0;

            if (!isEnabled)
            {
                // Log some sort of message?
            }
            
            return isEnabled;
        }
    }
}
