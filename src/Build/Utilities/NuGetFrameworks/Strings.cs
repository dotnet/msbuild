// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.Build.NuGetFrameworks
{
    /// <summary>
    /// Minimal stand-in for NuGet.Frameworks' generated <c>Strings</c> resource accessor.
    /// Only the messages referenced by the vendored closure are included; the English text is
    /// copied verbatim from NuGet.Client's <c>Strings.resx</c> at the vendored commit. Vendoring
    /// just these literals avoids dragging in resx/satellite-assembly plumbing for the net472 build.
    /// </summary>
    internal static class Strings
    {
        public const string ArgumentCannotBeNullOrEmpty = "The argument cannot be null or empty.";

        public const string FrameworkDoesNotSupportProfiles = ".NET 5.0 and above does not support profiles.";

        public const string FrameworkMismatch = "Frameworks must have the same identifier, profile, and platform.";

        public const string InvalidFrameworkIdentifier = "Invalid framework identifier '{0}'.";

        public const string InvalidFrameworkVersion = "Invalid framework version '{0}'.";

        public const string InvalidPlatformVersion = "Invalid platform version '{0}'.";

        public const string InvalidPortableFrameworksDueToHyphen = "Invalid portable frameworks '{0}'. A hyphen may not be in any of the portable framework names.";

        public const string MissingPortableFrameworks = "Invalid portable frameworks for '{0}'. A portable framework must have at least one framework in the profile.";
    }
}
