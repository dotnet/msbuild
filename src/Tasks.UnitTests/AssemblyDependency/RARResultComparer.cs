using Microsoft.Build.Framework;
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
                CollectionEquals(x.BuildErrorEvents?.Select(e => e.Message).ToList(), y.BuildErrorEvents?.Select(e => e.Message).ToList(), StringComparer.OrdinalIgnoreCase) &&
                CollectionEquals(x.BuildMessageEvents?.Select(e => e.Message).ToList(), y.BuildMessageEvents?.Select(e => e.Message).ToList(), StringComparer.OrdinalIgnoreCase) &&
                CollectionEquals(x.BuildWarningEvents?.Select(e => e.Message).ToList(), y.BuildWarningEvents?.Select(e => e.Message).ToList(), StringComparer.OrdinalIgnoreCase) &&
                CollectionEquals(x.CustomBuildEvents?.Select(e => e.Message).ToList(), y.CustomBuildEvents?.Select(e => e.Message).ToList(), StringComparer.OrdinalIgnoreCase) &&
                RARResponseComparer.Instance.Equals(x.Response, y.Response);
                
        }
    }
}
