// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Classifies string default values for path-typed task properties. Shared by the analyzer (to decide
    /// whether a default is a relative path that must be moved into <c>Execute()</c>) and the code fixer (to
    /// decide whether a default can be reproduced directly in a property initializer).
    /// </summary>
    internal static class PathDefaultClassifier
    {
        /// <summary>
        /// Determines whether a default string value is an already fully-qualified (absolute) path. Only such
        /// values can be reproduced in a property initializer, because rooting a relative path requires
        /// <c>TaskEnvironment</c>, which the engine does not set until after the task is constructed.
        /// <para>
        /// This mirrors what <c>AbsolutePath</c> does at runtime (<see cref="System.IO.Path.IsPathFullyQualified(string)"/>,
        /// which is unavailable on netstandard2.0) by polyfilling the runtime's OS-specific
        /// <c>PathInternal.IsPartiallyQualified</c> logic. The check is OS-specific on purpose: on Unix only a
        /// path rooted at <c>'/'</c> is fully qualified, so a drive-style value like <c>"C:/x"</c> is treated as
        /// relative there — matching how <c>AbsolutePath</c> would root it.
        /// </para>
        /// </summary>
        public static bool IsFullyQualifiedPath(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }

            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? IsFullyQualifiedWindows(value)
                : IsFullyQualifiedUnix(value);
        }

        /// <summary>
        /// Unix rule: a path is fully qualified only if it is rooted at <c>'/'</c>. Anything else (including
        /// Windows drive-relative or drive-absolute forms, which are meaningless on Unix) is relative.
        /// </summary>
        private static bool IsFullyQualifiedUnix(string value) => value[0] == '/';

        /// <summary>
        /// Windows rule, mirroring the negation of <c>PathInternal.IsPartiallyQualified</c>: a leading pair of
        /// separators (UNC/device, e.g. <c>\\server\share</c> or <c>\\?\</c>) or the drive-absolute
        /// <c>X:\</c>/<c>X:/</c> form is fully qualified. A single leading separator (<c>\foo</c>) is
        /// drive-relative and a drive-relative <c>X:foo</c> is not fully qualified.
        /// </summary>
        private static bool IsFullyQualifiedWindows(string value)
        {
            if (value.Length < 2)
            {
                return false;
            }

            if (IsDirectorySeparator(value[0]))
            {
                // Two leading separators, or "\?" (device-path prefix), are fully qualified.
                return value[1] == '?' || IsDirectorySeparator(value[1]);
            }

            // Drive-absolute "X:\" or "X:/" with a valid drive letter.
            return value.Length >= 3 &&
                value[1] == ':' &&
                IsDirectorySeparator(value[2]) &&
                IsValidDriveChar(value[0]);
        }

        private static bool IsDirectorySeparator(char c) => c == '\\' || c == '/';

        private static bool IsValidDriveChar(char c) =>
            (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

        /// <summary>
        /// Determines whether a default string value is a plausible <em>relative</em> path: non-empty, free of
        /// characters that are never valid in a path, and not already fully qualified. These are the defaults
        /// that need rooting through <c>TaskEnvironment</c> inside <c>Execute()</c> (MSBuildTask0008).
        /// </summary>
        public static bool IsRelativePathDefault(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOfAny(System.IO.Path.GetInvalidPathChars()) < 0 &&
                !IsFullyQualifiedPath(value);
        }
    }
}
