using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{
    internal class RARResponseComparer : BaseComparer<ResolveAssemblyReferenceResponse>
    {
        public static IEqualityComparer<ResolveAssemblyReferenceResponse> Instance { get; } = new RARResponseComparer();

        public override bool Equals(ResolveAssemblyReferenceResponse x, ResolveAssemblyReferenceResponse y)
        {
            if (x == y)
                return true;

            if (x == null || y == null)
                return false;

            return y != null &&
               CollectionEquals(x.CopyLocalFiles, y.CopyLocalFiles, ReadOnlyTaskItemComparer.Instance) &&
               x.DependsOnNETStandard == y.DependsOnNETStandard &&
               x.DependsOnSystemRuntime == y.DependsOnSystemRuntime &&
               CollectionEquals(x.FilesWritten, y.FilesWritten, ReadOnlyTaskItemComparer.Instance) &&
               CollectionEquals(x.RelatedFiles, y.RelatedFiles, ReadOnlyTaskItemComparer.Instance) &&
               CollectionEquals(x.ResolvedDependencyFiles, y.ResolvedDependencyFiles, ReadOnlyTaskItemComparer.Instance) &&
               CollectionEquals(x.ResolvedFiles, y.ResolvedFiles, ReadOnlyTaskItemComparer.Instance) &&
               CollectionEquals(x.SatelliteFiles, y.SatelliteFiles, ReadOnlyTaskItemComparer.Instance) &&
               CollectionEquals(x.ScatterFiles, y.ScatterFiles, ReadOnlyTaskItemComparer.Instance) &&
               CollectionEquals(x.SerializationAssemblyFiles, y.SerializationAssemblyFiles, ReadOnlyTaskItemComparer.Instance) &&
               CollectionEquals(x.SuggestedRedirects, y.SuggestedRedirects, ReadOnlyTaskItemComparer.Instance);
        }
    }
}
