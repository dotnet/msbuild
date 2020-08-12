// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Wraps the NuGet.Frameworks assembly, which is referenced by reflection.
    /// </summary>
    internal class NuGetFrameworkWrapper
    {
        /// <summary>
        /// NuGet Types
        /// </summary>
        private static MethodInfo ParseMethod;
        private static MethodInfo IsCompatibleMethod;
        private static object DefaultCompatibilityProvider;
        private static PropertyInfo FrameworkProperty;
        private static PropertyInfo VersionProperty;
        private static PropertyInfo PlatformProperty;
        private static PropertyInfo PlatformVersionProperty;

        public NuGetFrameworkWrapper()
        {
            // Resolve the location of the NuGet.Frameworks assembly
            var assemblyDirectory = BuildEnvironmentHelper.Instance.Mode == BuildEnvironmentMode.VisualStudio ?
                Path.Combine(BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory, "Common7", "IDE", "CommonExtensions", "Microsoft", "NuGet") :
                BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory;
            try
            {
                var NuGetAssembly = Assembly.LoadFile(Path.Combine(assemblyDirectory, "NuGet.Frameworks.dll"));
                var NuGetFramework = NuGetAssembly.GetType("NuGet.Frameworks.NuGetFramework");
                var NuGetFrameworkCompatibilityProvider = NuGetAssembly.GetType("NuGet.Frameworks.CompatibilityProvider");
                var NuGetFrameworkDefaultCompatibilityProvider = NuGetAssembly.GetType("NuGet.Frameworks.DefaultCompatibilityProvider");
                ParseMethod = NuGetFramework.GetMethod("Parse", new Type[] { typeof(string) });
                IsCompatibleMethod = NuGetFrameworkCompatibilityProvider.GetMethod("IsCompatible");
                DefaultCompatibilityProvider = NuGetFrameworkDefaultCompatibilityProvider.GetMethod("get_Instance").Invoke(null, new object[] { });
                FrameworkProperty = NuGetFramework.GetProperty("Framework");
                VersionProperty = NuGetFramework.GetProperty("Version");
                PlatformProperty = NuGetFramework.GetProperty("Platform");
                PlatformVersionProperty = NuGetFramework.GetProperty("PlatformVersion");
            }
            catch
            {
                throw new InternalErrorException(string.Format(AssemblyResources.GetString("NuGetAssemblyNotFound"), assemblyDirectory));
            }
        }

        private object Parse(string tfm)
        {
            return ParseMethod.Invoke(null, new object[] { tfm });
        }

        public string GetTargetFrameworkIdentifier(string tfm)
        {
            return FrameworkProperty.GetValue(Parse(tfm)) as string;
        }

        public string GetTargetFrameworkVersion(string tfm, int minVersionPartCount)
        {
            var version = VersionProperty.GetValue(Parse(tfm)) as Version;
            return GetNonZeroVersionParts(version, minVersionPartCount);
        }

        public string GetTargetPlatformIdentifier(string tfm)
        {
            return PlatformProperty.GetValue(Parse(tfm)) as string;
        }

        public string GetTargetPlatformVersion(string tfm, int minVersionPartCount)
        {
            var version = PlatformVersionProperty.GetValue(Parse(tfm)) as Version;
            return GetNonZeroVersionParts(version, minVersionPartCount);
        }

        public bool IsCompatible(string target, string candidate)
        {
            return Convert.ToBoolean(IsCompatibleMethod.Invoke(DefaultCompatibilityProvider, new object[] { Parse(target), Parse(candidate) }));
        }

        private string GetNonZeroVersionParts(Version version, int minVersionPartCount)
        {
            var nonZeroVersionParts = version.Revision == 0 ? version.Build == 0 ? version.Minor == 0 ? 1 : 2 : 3: 4;
            return version.ToString(Math.Max(nonZeroVersionParts, minVersionPartCount));
        }
    }
}
