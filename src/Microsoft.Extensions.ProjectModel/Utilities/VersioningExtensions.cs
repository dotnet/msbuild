using System;

namespace NuGet.Versioning
{
    public static class VersioningExtensions
    {
        public static bool EqualsFloating(this VersionRange self, NuGetVersion version)
        {
            if(!self.IsFloating)
            {
                return Equals(self.MinVersion, version);
            }

            switch (self.Float.FloatBehavior)
            {
                case NuGetVersionFloatBehavior.Prerelease:
                    return self.MinVersion.Version == version.Version &&
                           version.Release.StartsWith(self.MinVersion.Release, StringComparison.OrdinalIgnoreCase);

                case NuGetVersionFloatBehavior.Revision:
                    return self.MinVersion.Major == version.Major &&
                           self.MinVersion.Minor == version.Minor &&
                           self.MinVersion.Patch == version.Patch &&
                           self.MinVersion.Revision == version.Revision;

                case NuGetVersionFloatBehavior.Patch:
                    return self.MinVersion.Major == version.Major &&
                           self.MinVersion.Minor == version.Minor &&
                           self.MinVersion.Patch == version.Patch;

                case NuGetVersionFloatBehavior.Minor:
                    return self.MinVersion.Major == version.Major &&
                           self.MinVersion.Minor == version.Minor;

                case NuGetVersionFloatBehavior.Major:
                    return self.MinVersion.Major == version.Major;

                case NuGetVersionFloatBehavior.None:
                    return self.MinVersion == version;
                default:
                    return false;
            }
        }
    }
}
