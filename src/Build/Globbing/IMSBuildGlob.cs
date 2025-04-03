// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Globbing
{
    /// <summary>
    ///     Exposes the globbing semantics of the MSBuild engine.
    /// </summary>
    public interface IMSBuildGlob
    {
        /// <summary>
        ///     Matches the given <paramref name="stringToMatch" /> against the glob.
        ///     Matching is path aware:
        ///     - slashes are normalized
        ///     - arguments representing relative paths are normalized against the glob's root.
        ///     For example, the glob **/*.cs does not match ../a.cs, since ../a.cs points outside of the glob root.
        ///
        ///     Returns false if <paramref name="stringToMatch" /> contains invalid path or file characters.
        /// </summary>
        /// <param name="stringToMatch">The string to match. If the string represents a relative path, it will get normalized against the glob's root. Cannot be null.</param>
        /// <returns></returns>
        bool IsMatch(string stringToMatch);
    }
}
