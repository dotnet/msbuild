using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{
    internal class RARResultComparer : BaseComparer<ResolveAssemblyReferenceResult>
    {
        public static IEqualityComparer<ResolveAssemblyReferenceResult> Instance { get; } = new RARResultComparer();

        public override bool Equals(ResolveAssemblyReferenceResult x, ResolveAssemblyReferenceResult y)
        {
            if (x == y)
                return true;

            if (x == null || y == null)
                return false;

            return x.TaskResult == y.TaskResult &&
                CollectionEquals(x.BuildEventArgs?.Select(e => e.Message).ToList(), y.BuildEventArgs?.Select(e => e.Message).ToList(), StringComparer.OrdinalIgnoreCase) &&
                RARResponseComparer.Instance.Equals(x.Response, y.Response);
                
        }
    }
}
