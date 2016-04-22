// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Versioning;

namespace NuGet.Frameworks
{
    public static class FrameworksExtensions
    {
        // HACK(anurse): NuGet.Frameworks turns "dnxcore50" into "dnxcore5" :(
        public static string GetTwoDigitShortFolderName(this NuGetFramework self)
        {
            var original = self.GetShortFolderName();
            var index = 0;
            for (; index < original.Length; index++)
            {
                if (char.IsDigit(original[index]))
                {
                    break;
                }
            }

            var versionPart = original.Substring(index);
            if (versionPart.Length >= 2)
            {
                return original;
            }

            // Assume if the version part was preserved then leave it alone
            if (versionPart.IndexOf('.') != -1)
            {
                return original;
            }

            var name = original.Substring(0, index);
            var version = self.Version.ToString(2);

            if (self.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.NetPlatform))
            {
                return name + version;
            }

            return name + version.Replace(".", string.Empty);
        }

        // NuGet.Frameworks doesn't have the equivalent of the old VersionUtility.GetFrameworkString
        // which is relevant for building packages
        public static string GetFrameworkString(this NuGetFramework self)
        {
            var frameworkName = new FrameworkName(self.DotNetFrameworkName);
            string name = frameworkName.Identifier + frameworkName.Version;
            if (string.IsNullOrEmpty(frameworkName.Profile))
            {
                return name;
            }
            return name + "-" + frameworkName.Profile;
        }

        internal static bool IsPackageBased(this NuGetFramework self)
        {
            return self.IsPackageBased ||
                    (self.Framework == FrameworkConstants.FrameworkIdentifiers.NetCore &&
                     self.Version >= new Version(5, 0, 0, 0));
        }
    }
}
