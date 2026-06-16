// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    ///  Baseline behavioural contract for <see cref="VisualStudioLocationHelper"/> — the helper
    ///  that queries the Visual Studio Setup Configuration COM API
    ///  (<c>Microsoft.VisualStudio.Setup.Configuration.Native.dll</c>) for installed VS
    ///  instances on Windows .NET Framework.
    /// </summary>
    /// <remarks>
    ///  These tests intentionally make no assumption about whether VS is installed on the
    ///  machine they run on — CI agents may have zero, one, or many VS instances. The contract
    ///  asserted here is:
    ///  <list type="bullet">
    ///   <item>The call itself never throws (any underlying <c>COMException</c> /
    ///         <c>DllNotFoundException</c> is swallowed by design).</item>
    ///   <item>The returned list is non-null.</item>
    ///   <item>Every returned <see cref="VisualStudioInstance"/> has a non-empty <c>Name</c>,
    ///         a non-empty <c>Path</c>, and a <c>Version</c> at major 15 or higher (the
    ///         <c>ISetupConfiguration</c> API does not surface VS &lt; 15 instances).</item>
    ///   <item>Repeated calls return an equivalent set of instances (the helper is a pure
    ///         read of installer state; there is no per-call side effect).</item>
    ///  </list>
    ///  Tests run only on .NET Framework with VS Setup Configuration enabled — the
    ///  <c>FEATURE_VISUALSTUDIOSETUP</c> gate inside the helper means the non-net472 build
    ///  returns an empty list unconditionally and there is no native interop to exercise.
    /// </remarks>
    public sealed class VisualStudioLocationHelper_Tests
    {
        [WindowsOnlyFact]
        public void GetInstances_ReturnsNonNullList()
        {
            // The helper's contract is that it never throws and never returns null. An empty
            // list is the legitimate "no VS installed" outcome — that's still a valid result,
            // not an error.
            var instances = VisualStudioLocationHelper.GetInstances();
            instances.ShouldNotBeNull();
        }

        [WindowsOnlyFact]
        public void GetInstances_DoesNotThrowOnRepeatedCalls()
        {
            // The underlying ISetupConfiguration COM API is reentrant. The helper must be
            // safe to invoke multiple times in the same process and should return the same
            // set of installation paths each time. We compare paths because the same physical
            // install produces a fresh COM enumeration object on each call.
            var first = VisualStudioLocationHelper.GetInstances();
            var second = VisualStudioLocationHelper.GetInstances();

            first.Select(i => i.Path).OrderBy(p => p)
                .ShouldBe(second.Select(i => i.Path).OrderBy(p => p));
        }

        [WindowsOnlyFact]
        public void GetInstances_AllResults_HaveValidShape()
        {
            // For every instance the helper surfaces, the three carried fields (Name, Path,
            // Version) must be populated. Path doesn't have to exist on disk in the tests's
            // sandbox (the user might have moved the install), but it must be a non-empty
            // string. Version must be >= 15.0 because ISetupConfiguration was introduced
            // for "VS 15" (Visual Studio 2017) and never reports earlier products.
            var instances = VisualStudioLocationHelper.GetInstances();

            foreach (var instance in instances)
            {
                instance.Name.ShouldNotBeNullOrEmpty();
                instance.Path.ShouldNotBeNullOrEmpty();
                instance.Version.ShouldNotBeNull();
                instance.Version.Major.ShouldBeGreaterThanOrEqualTo(15);
            }
        }

        [WindowsOnlyFact]
        public void GetInstances_PathsAreUnique()
        {
            // Each install lives at its own physical location. The helper must not duplicate
            // an instance — if the same path shows up twice that would indicate the
            // enumeration didn't terminate or the same ISetupInstance was added twice.
            var paths = VisualStudioLocationHelper.GetInstances().Select(i => i.Path).ToList();
            paths.ShouldBeUnique();
        }

        [WindowsOnlyFact]
        public void GetInstances_ExistingPaths_AreAbsoluteDirectories()
        {
            // Real installations report rooted directories. If the path actually exists on
            // disk it must be a directory (not a file). Paths that don't exist are still
            // allowed (the user may have manually deleted an install without uninstalling).
            foreach (var instance in VisualStudioLocationHelper.GetInstances())
            {
                Path.IsPathRooted(instance.Path).ShouldBeTrue($"VS install path '{instance.Path}' is not rooted");
                if (Directory.Exists(instance.Path))
                {
                    // Sanity: when present, it's a directory, not a file masquerading as one.
                    File.Exists(instance.Path).ShouldBeFalse();
                }
            }
        }

        [Fact]
        public void VisualStudioInstance_Constructor_AssignsAllProperties()
        {
            // Trivial data-shape check on the value-type wrapper. The constructor is the only
            // surface code on this class (no validation, no normalization) — verifying it
            // here guards against an accidental change to property order or copy-paste error.
            var v = new Version(17, 8, 0, 0);
            var instance = new VisualStudioInstance("VS 2022 Enterprise", @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise", v);

            instance.Name.ShouldBe("VS 2022 Enterprise");
            instance.Path.ShouldBe(@"C:\Program Files\Microsoft Visual Studio\2022\Enterprise");
            instance.Version.ShouldBe(v);
        }
    }
}
