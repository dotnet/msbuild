// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Regression coverage for commit 56826722 (CsWin32 CLR metadata migration) and
// its follow-up fix. Scope: net472 only. The .NET Core RAR path does not use
// IMetaDataDispenser / RegMeta; it reads PE metadata through
// System.Reflection.Metadata.PEReader. This file is gated on
// !FEATURE_ASSEMBLYLOADCONTEXT so it compiles into Microsoft.Build.Tasks.UnitTests
// only when targeting net472.
#if !FEATURE_ASSEMBLYLOADCONTEXT

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    /// <summary>
    ///  Regression coverage for <see cref="AssemblyInformation"/> after #13853 migrated
    ///  the CLR metadata path from RCW + Marshal.ReleaseComObject to struct-based COM
    ///  through AgileComPointer / GIT. Two distinct concerns covered here:
    ///  <list type="number">
    ///   <item>
    ///    File-mapping lifetime — IMetaDataDispenser::OpenScope memory-maps the source
    ///    PE. After Dispose, both GIT cookies must be revoked, the underlying RegMeta
    ///    refcount must hit zero, and the mapping must be released so the caller can
    ///    delete or overwrite the file. This was the original suspicion behind the VC
    ///    regression; investigation falsified it, but the behavior is still required
    ///    and these tests guard against a future refcount or GIT-revocation leak.
    ///   </item>
    ///   <item>
    ///    Dispenser activation — IMetaDataDispenser must be activated through a path
    ///    that works in any host running managed code, including hosts that did not
    ///    enter the CLR via the mscoree.dll shim's startup path. The actual cause of
    ///    the VC P2PReferences.08 regression was that the commit switched activation
    ///    to raw CoCreateInstance, which loads the mscoree shim and fails with
    ///    CLR_E_SHIM_RUNTIMELOAD (0x80131700) in embedded-BuildManager hosts. The fix
    ///    calls clr.dll's exported DllGetClassObjectInternal directly via
    ///    <see cref="Windows.Win32.System.Com.ComClassFactory.TryCreateFromModule(string, System.Guid, string, out Windows.Win32.System.Com.ComClassFactory, out Windows.Win32.Foundation.HRESULT)"/>.
    ///   </item>
    ///  </list>
    /// </summary>
    public sealed class AssemblyInformation_Tests
    {
        private readonly ITestOutputHelper _output;

        public AssemblyInformation_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        ///  Copies a known-good managed PE into a TestEnvironment-scoped temp file so
        ///  delete / overwrite attempts don't trip on the test runner's own load of
        ///  the test assembly, and so cleanup is handled by the test infrastructure.
        /// </summary>
        private static string CopyTestAssemblyInto(TestEnvironment env)
        {
            string source = typeof(AssemblyInformation_Tests).Assembly.Location;
            TransientTestFile destFile = env.GetTempFile(".dll");
            File.Copy(source, destFile.Path, overwrite: true);
            return destFile.Path;
        }

        [WindowsOnlyFact]
        public void Dispose_ReleasesFileSoCallerCanDeleteImmediately()
        {
            // The RegMeta object opened by IMetaDataDispenser::OpenScope memory-maps the
            // PE. After Dispose, both AgileComPointer GIT cookies should be revoked, the
            // RegMeta refcount should hit zero, and the mapping should be released. We
            // verify by attempting a synchronous File.Delete on the same thread.
            using TestEnvironment env = TestEnvironment.Create(_output);
            string path = CopyTestAssemblyInto(env);

            using (var info = new AssemblyInformation(path))
            {
                // Touch Dependencies to force the metadata enum APIs to run, which is
                // what RAR does. Mere construction does not exercise every code path.
                _ = info.Dependencies;
                _ = info.FrameworkNameAttribute;
            }

            // If the mapping is still alive, Win32 returns ERROR_USER_MAPPED_FILE
            // (1224) which surfaces as IOException "The requested operation cannot
            // be performed on a file with a user-mapped section open." Plain
            // sharing locks surface as IOException with ERROR_SHARING_VIOLATION.
            Should.NotThrow(() => File.Delete(path));
        }

        [WindowsOnlyFact]
        public void Dispose_ReleasesFileSoCallerCanOverwriteImmediately()
        {
            // Mirror of the P2PReferences scenario: build emits a fresh copy of the
            // dependency DLL into a downstream output dir. If RAR (which constructed
            // AssemblyInformation over this DLL on an upstream pass) left the source
            // mapped, the Copy task on the downstream pass would fail.
            using TestEnvironment env = TestEnvironment.Create(_output);
            string path = CopyTestAssemblyInto(env);

            using (var info = new AssemblyInformation(path))
            {
                _ = info.Dependencies;
            }

            // Overwrite the file in place. ERROR_USER_MAPPED_FILE here is the
            // signature failure for the leaked-mapping hypothesis.
            Should.NotThrow(() =>
                File.WriteAllBytes(path, new byte[] { 0x4D, 0x5A }));
        }

        [WindowsOnlyFact]
        public void RepeatedConstructAndDispose_DoesNotAccumulateLocks()
        {
            // VC's CLR+CLR+CLR scenario walks A -> B -> C and re-opens each binary on
            // multiple RAR invocations. Make sure the GIT cookies issued on iteration N
            // are gone by iteration N+1 — a leak per iteration would push us past the
            // GIT entry cap eventually and lock the file the whole time.
            using TestEnvironment env = TestEnvironment.Create(_output);
            string path = CopyTestAssemblyInto(env);

            for (int i = 0; i < 25; i++)
            {
                using var info = new AssemblyInformation(path);
                _ = info.Dependencies;
                _ = info.FrameworkNameAttribute;
            }

            Should.NotThrow(() => File.Delete(path));
        }

        /// <summary>
        ///  Looks for an in-the-box .NET Framework assembly we can use as a non-test
        ///  assembly target. CustomMarshalers.dll ships under
        ///  %WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319 on any machine with .NET
        ///  Framework 4.x. Returns null if absent (clean source-build machine), in
        ///  which case the using test is skipped.
        /// </summary>
        private static string? TryGetCandidateFrameworkDll()  // null = absent
        {
            string windir = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            string archDir = IntPtr.Size == 8 ? "Framework64" : "Framework";
            string path = Path.Combine(windir, "Microsoft.NET", archDir, "v4.0.30319", "CustomMarshalers.dll");
            return File.Exists(path) ? path : null;
        }

        [WindowsOnlyFact]
        public void FrameworkDll_ReadAndDisposeReleasesFile()
        {
            // Run the full AssemblyInformation read path over a different file than
            // the test assembly itself (which is what every other lifetime test uses),
            // then attempt to delete it. ERROR_USER_MAPPED_FILE here would indicate
            // a leaked metadata mapping.
            string? source = TryGetCandidateFrameworkDll();
            if (source is null)
            {
                Assert.Skip("No candidate framework DLL available on this machine.");
            }

            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFile destFile = env.GetTempFile(".dll");
            File.Copy(source!, destFile.Path, overwrite: true);

            using (var info = new AssemblyInformation(destFile.Path))
            {
                _ = info.Dependencies;
                _ = info.FrameworkNameAttribute;
                _ = info.Files;
            }

            Should.NotThrow(() => File.Delete(destFile.Path));
        }

        [WindowsOnlyFact]
        public void FrameworkDll_OpenReadOpenAgain_DoesNotAccumulateState()
        {
            // Re-reading the same DLL across multiple RAR-shaped passes is the hot
            // path in the failing VC test. If any iteration leaves a mapping alive,
            // the N+1th attempt to overwrite the file would fail with
            // ERROR_USER_MAPPED_FILE (1224). Uses a non-test-assembly file so the
            // delete/overwrite attempt is not confounded by the test runner's own
            // load of the assembly.
            string? source = TryGetCandidateFrameworkDll();
            if (source is null)
            {
                Assert.Skip("No candidate framework DLL available on this machine.");
            }

            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFile destFile = env.GetTempFile(".dll");
            File.Copy(source!, destFile.Path, overwrite: true);

            for (int i = 0; i < 10; i++)
            {
                using var info = new AssemblyInformation(destFile.Path);
                _ = info.Dependencies;
                _ = info.FrameworkNameAttribute;
            }

            // Overwriting in place is the operation the Copy task performs when
            // an incremental build refreshes the dependency in the output dir.
            Should.NotThrow(() =>
                File.WriteAllBytes(destFile.Path, new byte[] { 0x4D, 0x5A }));
        }

        [WindowsOnlyFact]
        public void TwoLiveInstancesOnSameFile_BothReleaseAfterDispose()
        {
            // The new design wraps each AssemblyInformation's RegMeta with two
            // AgileComPointer entries (IMetaDataImport2 + IMetaDataAssemblyImport).
            // Two live instances against the same path therefore put four entries
            // through the GIT, backing two independent RegMeta objects over the same
            // file. Verify both can be disposed and the file can then be removed.
            using TestEnvironment env = TestEnvironment.Create(_output);
            string path = CopyTestAssemblyInto(env);

            var info1 = new AssemblyInformation(path);
            var info2 = new AssemblyInformation(path);
            _ = info1.Dependencies;
            _ = info2.Dependencies;

            info1.Dispose();
            info2.Dispose();

            Should.NotThrow(() => File.Delete(path));
        }

        /// <summary>
        ///  Regression for dotnet/msbuild #13853 fallout / VC P2PReferences.08:
        ///  IMetaDataDispenser must be activated through a path that works in any
        ///  host running managed code. The fix calls clr.dll's exported
        ///  DllGetClassObjectInternal directly via
        ///  <see cref="Windows.Win32.System.Com.ComClassFactory.TryCreateFromModule(string, System.Guid, string, out Windows.Win32.System.Com.ComClassFactory, out Windows.Win32.Foundation.HRESULT)"/>,
        ///  bypassing the mscoree.dll shim that raw <c>CoCreateInstance</c> on
        ///  CLSID_CorMetaDataDispenser would otherwise load. The shim fails with
        ///  CLR_E_SHIM_RUNTIMELOAD (0x80131700) in hosts whose bound-runtime state
        ///  was never set up (e.g., the VC cppxplatdev test harness embedding MSBuild
        ///  in-process via BuildManager).
        ///
        ///  This test sanity-checks the symptom: <see cref="AssemblyInformation"/>
        ///  must construct, read full metadata, and report transitive dependencies
        ///  for a normal managed assembly. The xunit test process itself enters the
        ///  CLR via the standard mscoree startup path, so a pre-fix CoCreateInstance
        ///  activation would still succeed here — this test cannot fully reproduce
        ///  the failing host condition. The defense-in-depth is the comment block in
        ///  AssemblyInformation.cs / MetadataReader.cs explaining why activation must
        ///  not change back to CoCreateInstance.
        /// </summary>
        [WindowsOnlyFact]
        public void Construction_ReportsExpectedDependencies()
        {
            string path = typeof(AssemblyInformation_Tests).Assembly.Location;

            using AssemblyInformation info = new(path);
            Microsoft.Build.Shared.AssemblyNameExtension[] deps = info.Dependencies;

            deps.ShouldNotBeNull();
            deps.Length.ShouldBeGreaterThan(0);

            // Every managed assembly transitively references mscorlib. If the dispenser
            // failed to activate, the metadata read path would have thrown a COMException
            // (the VC failure mode) and we never reach this assertion.
            bool seesMscorlib = false;
            foreach (Microsoft.Build.Shared.AssemblyNameExtension d in deps)
            {
                if (string.Equals(d.Name, "mscorlib", System.StringComparison.OrdinalIgnoreCase))
                {
                    seesMscorlib = true;
                    break;
                }
            }
            seesMscorlib.ShouldBeTrue("AssemblyInformation should resolve mscorlib as a dependency of any managed assembly.");
        }

        /// <summary>
        ///  Companion regression for #13853: <see cref="AssemblyInformation"/> must
        ///  remain functional across rapid construct-read-dispose cycles. Exercises
        ///  the module-based activation path on every iteration and would catch a
        ///  per-iteration leak in GIT cookies or in clr.dll's refcount on the
        ///  IClassFactory.
        /// </summary>
        [WindowsOnlyFact]
        public void RepeatedActivation_DoesNotDegrade()
        {
            string path = typeof(AssemblyInformation_Tests).Assembly.Location;
            for (int i = 0; i < 50; i++)
            {
                using AssemblyInformation info = new(path);
                _ = info.Dependencies;
            }
        }
    }
}

#endif
