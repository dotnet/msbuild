using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Build.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class BuildPlatformsAttribute : TargetConditionAttribute
    {
        private IEnumerable<BuildPlatform> _buildPlatforms;

        public BuildPlatformsAttribute(params BuildPlatform[] platforms)
        {
            if (platforms == null)
            {
                throw new ArgumentNullException("platforms");
            }

            _buildPlatforms = platforms;
        }

        public override bool EvaluateCondition()
        {
            var currentPlatform = CurrentPlatform.Current;

            if (currentPlatform == default(BuildPlatform))
            {
                throw new Exception("Unrecognized Platform.");
            }

            foreach (var platform in _buildPlatforms)
            {
                if (platform == currentPlatform)
                {
                    return true;
                }
            }

            return false;
        }
    }
}