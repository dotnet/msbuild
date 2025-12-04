// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;

#nullable disable

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Gets the <see cref="AssemblyFileVersionAttribute"/> of Microsoft.Build.dll.
    /// </summary>
    internal sealed class MSBuildAssemblyFileVersion
    {
        private static readonly Lazy<MSBuildAssemblyFileVersion> MSBuildAssemblyFileVersionLazy = new Lazy<MSBuildAssemblyFileVersion>(GetMSBuildAssemblyFileVersion, isThreadSafe: true);

        private MSBuildAssemblyFileVersion(string majorMinorBuild)
        {
            MajorMinorBuild = majorMinorBuild;
        }

        /// <summary>
        /// Gets a singleton instance of <see cref="MSBuildAssemblyFileVersion"/>.
        /// </summary>
        public static MSBuildAssemblyFileVersion Instance
        {
            get { return MSBuildAssemblyFileVersionLazy.Value; }
        }

        /// <summary>
        /// Gets the assembly file version in the format major.minor.
        /// </summary>
        public string MajorMinorBuild { get; set; }

        private static MSBuildAssemblyFileVersion GetMSBuildAssemblyFileVersion()
        {
            string versionString = typeof(MSBuildAssemblyFileVersion)
                .GetTypeInfo()
                ?.Assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()
                ?.Version;

            Version version;

            if (String.IsNullOrEmpty(versionString) || !Version.TryParse(versionString, out version))
            {
                // Fall back to the constant AssemblyVersion
                version = Version.Parse(Constants.AssemblyVersion);
            }

            return new MSBuildAssemblyFileVersion($"{version.Major}.{version.Minor}.{version.Build}");
        }
    }
}
