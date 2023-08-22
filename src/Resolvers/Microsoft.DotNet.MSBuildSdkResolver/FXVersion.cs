// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// dotnet doesn't have nullable enabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive


namespace Microsoft.DotNet.MSBuildSdkResolver
{
    // Note: This is SemVer 2.0.0 https://semver.org/spec/v2.0.0.html
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

        private static string GetId(string ids, int idStart)
        {
            int next = ids.IndexOf('.', idStart);

            return next == -1 ? ids.Substring(idStart) : ids.Substring(idStart, next - idStart);
        }

        public static int Compare(FXVersion s1, FXVersion s2)
        {
            // compare(u.v.w-p+b, x.y.z-q+c)
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

            if (string.IsNullOrEmpty(s1.Pre) || string.IsNullOrEmpty(s2.Pre))
            {
                // Empty (release) is higher precedence than prerelease
                return string.IsNullOrEmpty(s1.Pre) ? (string.IsNullOrEmpty(s2.Pre) ? 0 : 1) : -1;
            }

            // Both are non-empty (may be equal)

            // First character of pre is '-' when it is not empty

            // First idenitifier starts at position 1
            int idStart = 1;
            for (int i = idStart; true; ++i)
            {
                // C# strings are not null terminated. Pretend to make code similar to fx_ver.cpp
                char s1char = (s1.Pre.Length == i) ? '\0' : s1.Pre[i];
                char s2char = (s2.Pre.Length == i) ? '\0' : s2.Pre[i];
                if (s1char != s2char)
                {
                    // Found first character with a difference
                    if (s1char == '\0' && s2char == '.')
                    {
                        // identifiers both complete, b has an additional identifier
                        return -1;
                    }

                    if (s2char == '\0' && s1char == '.')
                    {
                        // identifiers both complete, a has an additional identifier
                        return 1;
                    }

                    // identifiers must not be empty
                    string id1 = GetId(s1.Pre, idStart);
                    string id2 = GetId(s2.Pre, idStart);

                    int id1num = 0;
                    bool id1IsNum = int.TryParse(id1, out id1num);
                    int id2num = 0;
                    bool id2IsNum = int.TryParse(id2, out id2num);

                    if (id1IsNum && id2IsNum)
                    {
                        // Numeric comparison
                        return (id1num > id2num) ? 1 : -1;
                    }
                    else if (id1IsNum || id2IsNum)
                    {
                        // Mixed compare.  Spec: Number < Text
                        return id2IsNum ? 1 : -1;
                    }
                    // Ascii compare
                    // Since we are using only ascii characters, unicode ordinal sort == ascii sort
                    return (s1char > s2char) ? 1 : -1;
                }
                else
                {
                    // s1char == s2char
                    if (s1char == '\0')
                    {
                        break;
                    }
                    if (s1char == '.')
                    {
                        idStart = i + 1;
                    }
                }
            }
            return 0;
        }

        private static bool ValidIdentifierCharSet(string id)
        {
            // ids must be of the set [0-9a-zA-Z-]

            // ASCII and Unicode ordering
            for (int i = 0; i < id.Length; ++i)
            {
                if (id[i] >= 'A')
                {
                    if ((id[i] > 'Z' && id[i] < 'a') || id[i] > 'z')
                    {
                        return false;
                    }
                }
                else
                {
                    if ((id[i] < '0' && id[i] != '-') || id[i] > '9')
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool ValidIdentifier(string id, bool buildMeta)
        {
            if (string.IsNullOrEmpty(id))
            {
                // Identifier must not be empty
                return false;
            }

            if (!ValidIdentifierCharSet(id))
            {
                // ids must be of the set [0-9a-zA-Z-]
                return false;
            }

            int ignored;
            if (!buildMeta && id[0] == '0' && id.Length > 1 && int.TryParse(id, out ignored))
            {
                // numeric identifiers must not be padded with 0s
                return false;
            }
            return true;
        }

        private static bool ValidIdentifiers(string ids)
        {
            if (string.IsNullOrEmpty(ids))
            {
                return true;
            }

            bool prerelease = ids[0] == '-';
            bool buildMeta = ids[0] == '+';

            if (!(prerelease || buildMeta))
            {
                // ids must start with '-' or '+' for prerelease & build respectively
                return false;
            }

            int idStart = 1;
            int nextId;
            while ((nextId = ids.IndexOf('.', idStart)) != -1)
            {
                if (!ValidIdentifier(ids.Substring(idStart, nextId - idStart), buildMeta))
                {
                    return false;
                }
                idStart = nextId + 1;
            }

            if (!ValidIdentifier(ids.Substring(idStart), buildMeta))
            {
                return false;
            }

            return true;
        }

        private static int IndexOfNonNumeric(string s, int startIndex)
        {
            for (int i = startIndex; i < s.Length; ++i)
            {
                if ((s[i] < '0') || (s[i] > '9'))
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool TryParse(string fxVersionString, out FXVersion FXVersion)
        {
            FXVersion = null;
            if (string.IsNullOrEmpty(fxVersionString))
            {
                return false;
            }

            int majorSeparator = fxVersionString.IndexOf(".", StringComparison.Ordinal);
            if (majorSeparator == -1)
            {
                return false;
            }

            int major = 0;
            if (!int.TryParse(fxVersionString.Substring(0, majorSeparator), out major))
            {
                return false;
            }
            if (majorSeparator > 1 && fxVersionString[0] == '0')
            {
                // if leading character is 0, and strlen > 1
                // then the numeric substring has leading zeroes which is prohibited by the specification.
                return false;
            }

            int minorStart = majorSeparator + 1;
            int minorSeparator = fxVersionString.IndexOf(".", minorStart, StringComparison.Ordinal);
            if (minorSeparator == -1)
            {
                return false;
            }

            int minor = 0;
            if (!int.TryParse(fxVersionString.Substring(minorStart, minorSeparator - minorStart), out minor))
            {
                return false;
            }
            if (minorSeparator - minorStart > 1 && fxVersionString[minorStart] == '0')
            {
                // if leading character is 0, and strlen > 1
                // then the numeric substring has leading zeroes which is prohibited by the specification.
                return false;
            }

            int patch = 0;
            int patchStart = minorSeparator + 1;
            int patchSeparator = IndexOfNonNumeric(fxVersionString, patchStart);
            if (patchSeparator == -1)
            {
                if (!int.TryParse(fxVersionString.Substring(patchStart), out patch))
                {
                    return false;
                }
                if (patchStart + 1 < fxVersionString.Length && fxVersionString[patchStart] == '0')
                {
                    // if leading character is 0, and strlen != 1
                    // then the numeric substring has leading zeroes which is prohibited by the specification.
                    return false;
                }

                FXVersion = new FXVersion(major, minor, patch);
                return true;
            }

            if (!int.TryParse(fxVersionString.Substring(patchStart, patchSeparator - patchStart), out patch))
            {
                return false;
            }
            if (patchSeparator - patchStart > 1 && fxVersionString[patchStart] == '0')
            {
                return false;
            }

            int preStart = patchSeparator;
            int preSeparator = fxVersionString.IndexOf("+", preStart, StringComparison.Ordinal);

            string pre = (preSeparator == -1) ? fxVersionString.Substring(preStart) : fxVersionString.Substring(preStart, preSeparator - preStart);

            if (!ValidIdentifiers(pre))
            {
                return false;
            }

            string build = "";
            if (preSeparator != -1)
            {
                build = fxVersionString.Substring(preSeparator);
                if (!ValidIdentifiers(build))
                {
                    return false;
                }
            }

            FXVersion = new FXVersion(major, minor, patch, pre, build);

            return true;
        }

        public override string ToString()
            => (!string.IsNullOrEmpty(Pre), !string.IsNullOrEmpty(Build)) switch
            {
                (false, false) => $"{Major}.{Minor}.{Patch}",
                (true, false) => $"{Major}.{Minor}.{Patch}{Pre}",
                (false, true) => $"{Major}.{Minor}.{Patch}{Build}",
                (true, true) => $"{Major}.{Minor}.{Patch}{Pre}{Build}",
            };
    }
}
