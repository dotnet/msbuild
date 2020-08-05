// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Simple replacement for System.Version used to implement version
    /// comparison intrinic property functions.
    ///
    /// Allows major version only (e.g. "3" is 3.0.0.0), ignores leading 'v'
    /// (e.g. "v3.0" is 3.0.0.0).
    ///
    /// Ignores semver prerelease and metadata portions (e.g. "1.0.0-preview+info"
    /// is 1.0.0.0).
    ///
    /// Treats unspecified components as 0 (e.g. x == x.0 == x.0.0 == x.0.0.0).
    ///
    /// Unlike System.Version, does not tolerate whitespace, and '+' is ignored as
    /// semver metadata as described above, not tolerated as positive sign of integer
    /// component.
    /// </summary>
    /// <remarks>
    /// Tolerating leading 'v' allows using $(TargetFrameworkVersion) directly.
    ///
    /// Ignoring semver portions allows, for example, checking >= major.minor
    /// while still in development of that release.
    ///
    /// Implemented as a struct to avoid heap allocation. Parsing is done
    /// without heap allocation at all on .NET Core. However, on .NET Framework,
    /// the integer component substrings are allocated as there is no int.Parse
    /// on span there.
    /// </remarks>
    internal readonly struct SimpleVersion : IEquatable<SimpleVersion>, IComparable<SimpleVersion>
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Build;
        public readonly int Revision;

        public SimpleVersion(int major, int minor = 0, int build = 0, int revision = 0)
        {
            if (major < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(major));
            }

            if (minor < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minor));
            }

            if (build < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(build));
            }

            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            Major = major;
            Minor = minor;
            Build = build;
            Revision = revision;
        }

        public bool Equals(SimpleVersion other)
        {
            return Major == other.Major &&
                   Minor == other.Minor &&
                   Build == other.Build &&
                   Revision == other.Revision;
        }

        public int CompareTo(SimpleVersion other)
        {
            return Major != other.Major ? (Major > other.Major ? 1 : -1) :
                   Minor != other.Minor ? (Minor > other.Minor ? 1 : -1) :
                   Build != other.Build ? (Build > other.Build ? 1 : -1) :
                   Revision != other.Revision ? (Revision > other.Revision ? 1 : -1) :
                   0;
        }

        public override bool Equals(object obj) => obj is SimpleVersion v && Equals(v);
        public override int GetHashCode() => (Major, Minor, Build, Revision).GetHashCode();
        public override string ToString() => FormattableString.Invariant($"{Major}.{Minor}.{Build}.{Revision}");

        public static bool operator ==(SimpleVersion a, SimpleVersion b) => a.Equals(b);
        public static bool operator !=(SimpleVersion a, SimpleVersion b) => !a.Equals(b);
        public static bool operator <(SimpleVersion a, SimpleVersion b) => a.CompareTo(b) < 0;
        public static bool operator <=(SimpleVersion a, SimpleVersion b) => a.CompareTo(b) <= 0;
        public static bool operator >(SimpleVersion a, SimpleVersion b) => a.CompareTo(b) > 0;
        public static bool operator >=(SimpleVersion a, SimpleVersion b) => a.CompareTo(b) >= 0;

        public static SimpleVersion Parse(string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var span = RemoveTrivia(input);

            int minor = 0, build = 0, revision = 0;

            if (ParseComponent(ref span, out int major) &&
                ParseComponent(ref span, out minor) &&
                ParseComponent(ref span, out build) &&
                ParseComponent(ref span, out revision))
            {
                // More than 4 components (too many dots)
                throw InvalidVersionFormat();
            }

            return new SimpleVersion(major, minor, build, revision);
        }

        private static readonly char[] s_semverSeparators = new char[] { '-', '+' };

        private static ReadOnlySpan<char> RemoveTrivia(string input)
        {
            int startIndex = 0;
            int endIndex = input.Length;

            if (input.Length > 0 && (input[0] == 'v' || input[0] == 'V'))
            {
                startIndex = 1;
            }

            int separatorIndex = input.IndexOfAny(s_semverSeparators, startIndex);

            if (separatorIndex >= 0)
            {
                endIndex = separatorIndex;
            }

            return input.AsSpan().Slice(startIndex, endIndex - startIndex);
        }

        private static bool ParseComponent(ref ReadOnlySpan<char> span, out int value)
        {
            int dotIndex = span.IndexOf('.');
            if (dotIndex < 0)
            {
                value = ParseComponent(span);
                return false;
            }
            else
            {
                value = ParseComponent(span.Slice(0, dotIndex));
                span = span.Slice(dotIndex + 1);
                return true;
            }
        }

        private static int ParseComponent(ReadOnlySpan<char> span)
        {
        #if NETFRAMEWORK
            // Cannot parse int from span on .NET Framework, so allocate the substring
            var spanOrString = span.ToString();
        #else
            var spanOrString = span;
        #endif

            if (!int.TryParse(spanOrString, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            {
                throw InvalidVersionFormat();
            }

            // Cannot parse as negative using NumberStyles.None. Also, +/- would have
            // been stripped as semver trivia earlier.
            Debug.Assert(value >= 0);

            return value;
        }

        private static Exception InvalidVersionFormat()
        {
            return new FormatException(ResourceUtilities.GetResourceString(nameof(InvalidVersionFormat)));
        }
    }
}
