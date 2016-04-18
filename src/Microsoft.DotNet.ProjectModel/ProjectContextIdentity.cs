using Microsoft.Extensions.Internal;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public struct ProjectContextIdentity
    {
        public ProjectContextIdentity(string path, NuGetFramework targetFramework)
        {
            Path = path;
            TargetFramework = targetFramework;
        }

        public string Path { get; }
        public NuGetFramework TargetFramework { get; }

        public bool Equals(ProjectContextIdentity other)
        {
            return string.Equals(Path, other.Path) && Equals(TargetFramework, other.TargetFramework);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ProjectContextIdentity && Equals((ProjectContextIdentity) obj);
        }

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();
            combiner.Add(Path);
            combiner.Add(TargetFramework);
            return combiner.CombinedHash;
        }
    }
}