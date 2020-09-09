using Microsoft.Build.Shared;
using System;

namespace Microsoft.Build.Utilities
{

    public enum ChangeWaveReturnType
    {
        Invalid = 0,
        VersionOutOfRotation,
        FeatureEnabled,
        FeatureDisabled
    }
    /// <summary>
    /// There may be some confusion between "enabling waves" and "enabling features".
    /// Enabling a wave DISABLES all features behind that wave.
    /// </summary>
    public class ChangeWaves
    {
        public const string LowestWave = Wave16_8, Wave16_8 = "16.8";
        public const string Wave16_10 = "16.10";
        public const string Wave17_0 = "17.0";

        /// <summary>
        /// Special value indicating that all features behind change-waves should be enabled.
        /// </summary>
        public const string EnableAllFeaturesBehindChangeWaves = "999.999";

        /// <summary>
        /// Compares version against the MSBuildChangeWave environment variable.
        /// Version MUST be of the format: "xx.yy".
        /// </summary>
        /// <param name="wave">The version to compare.</param>
        /// <returns>A bool indicating whether the version is enabled.</returns>
        public static ChangeWaveReturnType IsChangeWaveEnabled(string wave)
        {
            // This is opt out behavior, all waves are enabled by default.
            // If version is invalid, 
            if (string.IsNullOrEmpty(Traits.Instance.MSBuildChangeWave))
            {
                return ChangeWaveReturnType.FeatureEnabled;
            }

            Version currentDisabledWave;

            if (!Version.TryParse(Traits.Instance.MSBuildChangeWave, out currentDisabledWave))
            {
                // should still enable the features behind this wave.
                // not sure I like that a dev would have to hide their feature behind if(returntype == invalid || returntype == featureenabled)
                return ChangeWaveReturnType.Invalid;
            }

            // User-set change wave is valid. let's verify it's in rotation
            HandleOutOfRotationWaves(currentDisabledWave);



            Version waveToCheck;

            // When a caller passes an invalid waveToCheck, fail the build.
            ErrorUtilities.VerifyThrow(Version.TryParse(wave.ToString(), out waveToCheck),
                                       $"Argument 'wave' passed with invalid format." +
                                       $"Please use the const strings or define one with format 'xx.yy");

            return waveToCheck < currentDisabledWave ? ChangeWaveReturnType.FeatureEnabled : ChangeWaveReturnType.FeatureDisabled;
        }

        public static void HandleOutOfRotationWaves(Version wave)
        {
            //Ideas: how do we check reliably what our current lowest wave it?
        }
    }
}
