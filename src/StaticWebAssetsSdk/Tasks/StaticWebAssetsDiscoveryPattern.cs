// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class StaticWebAssetsDiscoveryPattern
    {
        public string Name { get; set; }

        public string Source { get; set; }

        public string ContentRoot { get; set; }

        public string BasePath { get; set; }

        public string Pattern { get; set; }

        public override bool Equals(object obj) =>
            obj is StaticWebAssetsDiscoveryPattern pattern
            && Name == pattern.Name
            && Source == pattern.Source
            && ContentRoot == pattern.ContentRoot
            && BasePath == pattern.BasePath
            && Pattern == pattern.Pattern;

        public override int GetHashCode()
        {
#if NET6_0_OR_GREATER
            return HashCode.Combine(Name, Source, ContentRoot, BasePath, Pattern);
#else
            int hashCode = 1513180540;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Source);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ContentRoot);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(BasePath);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Pattern);
            return hashCode;
#endif
        }

        public ITaskItem ToTaskItem()
        {
            var result = new TaskItem(Name);

            result.SetMetadata(nameof(ContentRoot), ContentRoot);
            result.SetMetadata(nameof(BasePath), BasePath);
            result.SetMetadata(nameof(Pattern), Pattern);
            result.SetMetadata(nameof(Source), Source);

            return result;
        }

        internal static bool HasSourceId(ITaskItem pattern, string source) =>
            HasSourceId(pattern.GetMetadata(nameof(Source)), source);

        internal static bool HasSourceId(string candidate, string source) =>
            string.Equals(candidate, source, StringComparison.Ordinal);

        internal bool HasSourceId(string source) => HasSourceId(Source, source);

        internal static StaticWebAssetsDiscoveryPattern FromTaskItem(ITaskItem pattern)
        {
            var result = new StaticWebAssetsDiscoveryPattern
            {
                Name = pattern.ItemSpec,
                Source = pattern.GetMetadata(nameof(Source)),
                BasePath = pattern.GetMetadata(nameof(BasePath)),
                ContentRoot = pattern.GetMetadata(nameof(ContentRoot)),
                Pattern = pattern.GetMetadata(nameof(Pattern))
            };

            return result;
        }

        public override string ToString() => string.Join(" - ", Name, Source, Pattern, BasePath, ContentRoot);

        private string GetDebuggerDisplay() => ToString();
    }
}
