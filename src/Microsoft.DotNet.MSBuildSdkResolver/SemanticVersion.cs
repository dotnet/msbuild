// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    internal sealed class SemanticVersion
    {
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
        public string Pre { get; private set; }
        public string Build { get; private set; }

        public SemanticVersion(int major, int minor, int patch) : this(major, minor, patch, string.Empty, string.Empty)
        {
        }

        public SemanticVersion(int major, int minor, int patch, string pre) :
            this(major, minor, patch, pre, string.Empty)
        {
        }

        public SemanticVersion(int major, int minor, int patch, string pre, string build)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Pre = pre;
            Build = build;
        }

        public static bool operator ==(SemanticVersion s1, SemanticVersion s2)
        {
            return Compare(s1, s2) == 0;
        }

        public static bool operator !=(SemanticVersion s1, SemanticVersion s2)
        {
            return !(s1 == s2);
        }

        public static bool operator <(SemanticVersion s1, SemanticVersion s2)
        {
            return Compare(s1, s2) < 0;
        }

        public static bool operator >(SemanticVersion s1, SemanticVersion s2)
        {
            return Compare(s1, s2) > 0;
        }

        public static bool operator >=(SemanticVersion s1, SemanticVersion s2)
        {
            return Compare(s1, s2) >= 0;
        }

        public static bool operator <=(SemanticVersion s1, SemanticVersion s2)
        {
            return Compare(s1, s2) <= 0;
        }

        public static SemanticVersion Parse(string semanticVersionString)
        {
            int majorSeparator = semanticVersionString.IndexOf(".");
            if (majorSeparator == -1)
            {
                return null;
            }

            int major = 0;
            if (!int.TryParse(semanticVersionString.Substring(0, majorSeparator), out major))
            {
                return null;
            }

            int minorStart = majorSeparator + 1;
            int minorSeparator = semanticVersionString.IndexOf(".", minorStart);
            if (minorSeparator == -1)
            {
                return null;
            }

            int minor = 0;
            if (!int.TryParse(semanticVersionString.Substring(minorStart, minorSeparator - minorStart), out minor))
            {
                return null;
            }

            int patch = 0;
            int patchStart = minorSeparator + 1;
            int patchSeparator = semanticVersionString.FindFirstNotOf("0123456789", patchStart);
            if (patchSeparator == -1)
            {
                if (!int.TryParse(semanticVersionString.Substring(patchStart), out patch))
                {
                    return null;
                }

                return new SemanticVersion(major, minor, patch);
            }

            if (!int.TryParse(semanticVersionString.Substring(patchStart, patchSeparator - patchStart), out patch))
            {
                return null;
            }

            int preStart = patchSeparator;
            int preSeparator = semanticVersionString.IndexOf("+", preStart);
            if (preSeparator == -1)
            {
                return new SemanticVersion(major, minor, patch, semanticVersionString.Substring(preStart));
            }
            else
            {
                int buildStart = preSeparator + 1;
                return new SemanticVersion(
                    major,
                    minor,
                    patch,
                    semanticVersionString.Substring(preStart, preSeparator - preStart),
                    semanticVersionString.Substring(buildStart));
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            var other = obj as SemanticVersion;
            if (other == null)
            {
                return false;
            }

            return this == other;
        }

        public override int GetHashCode()
        {
            return Major.GetHashCode() ^
                   Minor.GetHashCode() ^
                   Patch.GetHashCode() ^
                   Pre.GetHashCode() ^
                   Build.GetHashCode();
        }

        private static int Compare(SemanticVersion s1, SemanticVersion s2)
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

            int preCompare = string.Compare(s1.Pre, s2.Pre);
            if (preCompare != 0)
            {
                return preCompare;
            }

            return string.Compare(s1.Build, s2.Build);
        }
    }
}