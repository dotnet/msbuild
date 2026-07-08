// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        /// Determines whether a default string value is an already fully-qualified (absolute) path, recognizing
        /// both Windows and Unix absolute forms independently of the host OS. Only such values can be reproduced
        /// in a property initializer, because rooting a relative path requires <c>TaskEnvironment</c>, which the
        /// engine does not set until after the task is constructed. This mirrors the check
        /// <c>AbsolutePath</c>'s constructor performs (<see cref="System.IO.Path.IsPathFullyQualified(string)"/>),
        /// which is unavailable on netstandard2.0.
        /// </summary>
        public static bool IsFullyQualifiedPath(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }

            char first = value[0];

            // Unix absolute path: leading '/'. (Also covers "//..." device/UNC-style prefixes.)
            if (first == '/')
            {
                return true;
            }

            // Windows UNC / device path: two leading separators, e.g. "\\server\share" or "\\?\C:\".
            if (first == '\\')
            {
                return value.Length >= 2 && (value[1] == '\\' || value[1] == '/');
            }

            // Windows drive-absolute path: "X:\" or "X:/". A drive-relative form like "X:foo" is intentionally
            // rejected because it is not fully qualified (its meaning depends on the drive's current directory).
            return value.Length >= 3 &&
                ((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z')) &&
                value[1] == ':' &&
                (value[2] == '\\' || value[2] == '/');
        }

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
