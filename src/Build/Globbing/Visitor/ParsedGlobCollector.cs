using System.Collections.Immutable;

#nullable disable

namespace Microsoft.Build.Globbing.Visitor
{
    internal class ParsedGlobCollector : GlobVisitor
    {
        private readonly ImmutableList<MSBuildGlob>.Builder _collectedGlobs = ImmutableList.CreateBuilder<MSBuildGlob>();
        public ImmutableList<MSBuildGlob> CollectedGlobs => _collectedGlobs.ToImmutable();

        protected override void VisitMSBuildGlob(MSBuildGlob msbuildGlob)
        {
            _collectedGlobs.Add(msbuildGlob);
        }
    }
}
