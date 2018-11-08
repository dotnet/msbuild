using System.Collections.Generic;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain
{
    public partial class ResolveAssemblyReferenceResponse
    {
        internal List<string> TrackedFiles { get; set; }

        internal List<string> TrackedDirectories { get; set; }
    }
}
