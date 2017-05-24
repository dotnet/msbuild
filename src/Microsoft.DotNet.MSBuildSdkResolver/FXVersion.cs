// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    // Note: This is not SemVer (esp., in comparing pre-release part, fx_ver_t does not
    // compare multiple dot separated identifiers individually.) ex: 1.0.0-beta.2 vs. 1.0.0-beta.11
    // See the original version of this code here: https://github.com/dotnet/core-setup/blob/master/src/corehost/cli/fxr/fx_ver.cpp
    internal sealed class FXVersion
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string Pre { get; }
        public string Build { get; }

        public FXVersion(int major, int minor, int patch, string pre = "", string build = "")
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Pre = pre;
            Build = build;
        }

        public static int Compare(FXVersion s1, FXVersion s2)
        {
            if (s1.Major != s2.Major)
            {
                return s1.Major > s2.Major ? 1 : -1;
            }

            if (s1.Minor != s2.Minor)
            {
                return s1.Minor > s2.Minor ? 1 : -1;
            }

            if (s1.Patch != s2.Patch)
            {
                return s1.Patch > s2.Patch ? 1 : -1;
            }

            if (string.IsNullOrEmpty(s1.Pre) != string.IsNullOrEmpty(s2.Pre))
            {
                return string.IsNullOrEmpty(s1.Pre) ? 1 : -1;
            }

            int preCompare = string.CompareOrdinal(s1.Pre, s2.Pre);
            if (preCompare != 0)
            {
                return preCompare;
            }

            return string.CompareOrdinal(s1.Build, s2.Build);
        }

        public static bool TryParse(string fxVersionString, out FXVersion FXVersion)
        {
            FXVersion = null;
            if (string.IsNullOrEmpty(fxVersionString))
            {
                return false;
            }

            int majorSeparator = fxVersionString.IndexOf(".");
            if (majorSeparator == -1)
            {
                return false;
            }

            int major = 0;
            if (!int.TryParse(fxVersionString.Substring(0, majorSeparator), out major))
            {
                return false;
            }

            int minorStart = majorSeparator + 1;
            int minorSeparator = fxVersionString.IndexOf(".", minorStart);
            if (minorSeparator == -1)
            {
                return false;
            }

            int minor = 0;
            if (!int.TryParse(fxVersionString.Substring(minorStart, minorSeparator - minorStart), out minor))
            {
                return false;
            }

            int patch = 0;
            int patchStart = minorSeparator + 1;
            int patchSeparator = fxVersionString.FindFirstNotOf("0123456789", patchStart);
            if (patchSeparator == -1)
            {
                if (!int.TryParse(fxVersionString.Substring(patchStart), out patch))
                {
                    return false;
                }

                FXVersion = new FXVersion(major, minor, patch);
                return true;
            }

            if (!int.TryParse(fxVersionString.Substring(patchStart, patchSeparator - patchStart), out patch))
            {
                return false;
            }

            int preStart = patchSeparator;
            int preSeparator = fxVersionString.IndexOf("+", preStart);
            if (preSeparator == -1)
            {
                FXVersion = new FXVersion(major, minor, patch, fxVersionString.Substring(preStart));
            }
            else
            {
                int buildStart = preSeparator + 1;
                FXVersion = new FXVersion(
                    major,
                    minor,
                    patch,
                    fxVersionString.Substring(preStart, preSeparator - preStart),
                    fxVersionString.Substring(buildStart));
            }

            return true;
        }
    }
}