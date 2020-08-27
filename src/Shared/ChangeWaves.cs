using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    public enum MSBuildChangeWaveVersion
    {
        //ensure these enums maps to _mapping
        v16_8 = 0,
        v16_10,
        v17_0
    }
    public class ChangeWaves
    {
        private static float[] _mapping =
        {
            16.8f,
            16.10f,
            17.0f
        };

        public static bool IsChangeWaveEnabled(MSBuildChangeWaveVersion version)
        {
            if(string.IsNullOrEmpty(Traits.Instance.MSBuildChangeWave))
            {
                return false;
            }

            bool isEnabled = _mapping[(int)version] <= float.Parse(Traits.Instance.MSBuildChangeWave);

            if(!isEnabled)
            {
                // Log some sort of message?
            }
            
            return isEnabled;
        }
    }
}
