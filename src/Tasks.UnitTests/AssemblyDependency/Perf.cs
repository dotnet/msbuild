using System;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Test a few perf scenarios.
    /// </summary>
    public sealed class Perf : ResolveAssemblyReferenceTestFixture
    {
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void AutoUnifyUsesMinimumIO()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Perf.AutoUnifyUsesMinimumIO() test");

            // Manually instantiate a test fixture and run it.
            VersioningAndUnification.AutoUnify.StronglyNamedDependencyAutoUnify t = new VersioningAndUnification.AutoUnify.StronglyNamedDependencyAutoUnify();
            t.StartIOMonitoring();
            t.Exists();
            t.StopIOMonitoringAndAssert_Minimal_IOUse();
        }
    }
}