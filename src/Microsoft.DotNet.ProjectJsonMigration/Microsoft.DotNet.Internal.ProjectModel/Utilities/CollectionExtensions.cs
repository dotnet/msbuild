using Microsoft.DotNet.Internal.ProjectModel.Compilation;
using System.Linq;

namespace System.Collections.Generic
{
    internal static class CollectionExtensions
    {
        public static LibraryAssetGroup GetDefaultGroup(this IEnumerable<LibraryAssetGroup> self) => GetGroup(self, string.Empty);
        public static LibraryAssetGroup GetRuntimeGroup(this IEnumerable<LibraryAssetGroup> self, string runtime)
        {
            if(string.IsNullOrEmpty(runtime))
            {
                throw new ArgumentNullException(nameof(runtime));
            }
            return GetGroup(self, runtime);
        }

        private static LibraryAssetGroup GetGroup(IEnumerable<LibraryAssetGroup> groups, string runtime)
        {
            return groups.FirstOrDefault(g => g.Runtime == runtime);
        }

        public static IEnumerable<LibraryAsset> GetDefaultAssets(this IEnumerable<LibraryAssetGroup> self) => GetAssets(self, string.Empty);
        public static IEnumerable<LibraryAsset> GetRuntimeAssets(this IEnumerable<LibraryAssetGroup> self, string runtime)
        {
            if(string.IsNullOrEmpty(runtime))
            {
                throw new ArgumentNullException(nameof(runtime));
            }
            return GetAssets(self, runtime);
        }

        private static IEnumerable<LibraryAsset> GetAssets(IEnumerable<LibraryAssetGroup> self, string runtime)
        {
            return self
                .Where(a => string.Equals(a.Runtime, runtime, StringComparison.Ordinal))
                .SelectMany(a => a.Assets);
        }
    }
}
