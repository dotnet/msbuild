using System;

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Attribute added to the test assembly during build. 
    /// Captures the version of dotnet SDK the build is targeting.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class DotNetSdkVersionAttribute : Attribute
    {
        public string Version { get; }

        public DotNetSdkVersionAttribute(string version)
        {
            Version = version;
        }
    }
}
