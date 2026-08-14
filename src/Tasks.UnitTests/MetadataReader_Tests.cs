// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    /// <summary>
    ///  Covers <see cref="MetadataReader"/>, the internal helper used by the ManifestUtil layer
    ///  to extract identity attributes (Name / Version / Culture / PublicKeyToken / ProcessorArchitecture)
    ///  and to test for assembly-level custom attributes by name.
    /// </summary>
    /// <remarks>
    ///  Two implementations exist behind <c>#if RUNTIME_TYPE_NETCORE</c>: a managed
    ///  <c>System.Reflection.Metadata.PEReader</c>-based reader on .NET, and a CLR metadata
    ///  COM-based reader (<c>IMetaDataDispenser</c> + <c>IMetaDataImport2</c> +
    ///  <c>IMetaDataAssemblyImport</c>) on .NET Framework. Both must expose the same public
    ///  contract; the tests run on both target frameworks via the project's TFM multitarget.
    /// </remarks>
    public sealed class MetadataReader_Tests
    {
        private readonly ITestOutputHelper _output;

        public MetadataReader_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>Path of this test assembly — guaranteed to exist and be a valid managed PE.</summary>
        private static string ThisAssemblyPath => typeof(MetadataReader_Tests).Assembly.Location;

        /// <summary>Compile-time assembly identity for this test assembly, used as the baseline for property comparisons.</summary>
        private static AssemblyName ThisAssemblyName => typeof(MetadataReader_Tests).Assembly.GetName();

        [Fact]
        public void Create_ValidAssembly_ReturnsReaderWithExpectedIdentity()
        {
            using MetadataReader reader = MetadataReader.Create(ThisAssemblyPath);

            reader.ShouldNotBeNull();
            reader.Name.ShouldBe(ThisAssemblyName.Name);
            reader.Version.ShouldBe(ThisAssemblyName.Version.ToString());

            // ProcessorArchitecture is one of: msil, x86, amd64, ia64, arm, arm64. "unknown" indicates failure.
            reader.ProcessorArchitecture.ShouldNotBeNullOrEmpty();
            reader.ProcessorArchitecture.ShouldNotBe("unknown");
        }

        [Fact]
        public void Create_NonExistentPath_ReturnsNull()
        {
            // The path must not exist. Use a clearly bogus directory so we get the file-not-found
            // path inside MetadataReader rather than a permission-denied or sharing failure.
            string bogusPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll");

            using MetadataReader reader = MetadataReader.Create(bogusPath);
            reader.ShouldBeNull();
        }

        [Fact]
        public void Create_NotAnAssembly_ReturnsNull()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFile textFile = env.CreateFile("not-an-assembly.txt", "This is plain text, not a PE.");

            using MetadataReader reader = MetadataReader.Create(textFile.Path);
            reader.ShouldBeNull();
        }

        [Fact]
        public void HasAssemblyAttribute_TargetFrameworkAttribute_ReturnsTrue()
        {
            // Modern SDK-built assemblies always carry [TargetFrameworkAttribute(...)] on the assembly.
            using MetadataReader reader = MetadataReader.Create(ThisAssemblyPath);
            reader.ShouldNotBeNull();

            reader.HasAssemblyAttribute("System.Runtime.Versioning.TargetFrameworkAttribute").ShouldBeTrue();
        }

        [Fact]
        public void HasAssemblyAttribute_UnknownAttribute_ReturnsFalse()
        {
            using MetadataReader reader = MetadataReader.Create(ThisAssemblyPath);
            reader.ShouldNotBeNull();

            reader.HasAssemblyAttribute("ThisAttribute.Definitely.DoesNotExist.OnAnyAssembly.Ever").ShouldBeFalse();
        }

        [Fact]
        public void HasAssemblyAttribute_CaseSensitiveFullName()
        {
            // The lookup uses the full type name and is case-sensitive — verify the mismatch path.
            using MetadataReader reader = MetadataReader.Create(ThisAssemblyPath);
            reader.ShouldNotBeNull();

            reader.HasAssemblyAttribute("system.runtime.versioning.targetframeworkattribute").ShouldBeFalse();
        }

        [Fact]
        public void Properties_AreCachedAndIdempotent()
        {
            // Properties are populated lazily under a lock; second access must return the same value
            // and must not re-enter the underlying metadata APIs in a way that breaks state.
            using MetadataReader reader = MetadataReader.Create(ThisAssemblyPath);
            reader.ShouldNotBeNull();

            string nameFirst = reader.Name;
            string versionFirst = reader.Version;

            reader.Name.ShouldBe(nameFirst);
            reader.Version.ShouldBe(versionFirst);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            // Calling Close()/Dispose() any number of times must be a no-op after the first.
            // The `using` ensures the third (implicit) Dispose at scope exit is also a no-op.
            using MetadataReader reader = MetadataReader.Create(ThisAssemblyPath);
            reader.ShouldNotBeNull();

            reader.Close();
            Should.NotThrow(() => ((IDisposable)reader).Dispose());
            Should.NotThrow(reader.Close);
        }

        [Fact]
        public void HasAssemblyAttribute_AfterCachingProperties_StillWorks()
        {
            // The two lookup paths (custom-attribute cache on .NETCORE / IMetaData* on netfx) and the
            // identity-property cache are independent. Touching one must not invalidate the other.
            using MetadataReader reader = MetadataReader.Create(ThisAssemblyPath);
            reader.ShouldNotBeNull();

            // Warm the identity cache first, then exercise HasAssemblyAttribute.
            _ = reader.Name;
            _ = reader.Version;

            reader.HasAssemblyAttribute("System.Runtime.Versioning.TargetFrameworkAttribute").ShouldBeTrue();
            reader.HasAssemblyAttribute("Nonexistent.AttributeName").ShouldBeFalse();
        }

        /// <summary>
        ///  Resolves the path to a well-known .NET Framework 4 assembly on the system. Returns
        ///  null when the framework is not installed (e.g. clean CI machine without legacy
        ///  Windows components). Callers must be Windows-only.
        /// </summary>
        private static string TryGetFramework4AccessibilityDllPath()
        {
            string windir = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            string archDir = IntPtr.Size == 8 ? "Framework64" : "Framework";
            string path = Path.Combine(windir, "Microsoft.NET", archDir, "v4.0.30319", "Accessibility.dll");
            return File.Exists(path) ? path : null;
        }

        [WindowsOnlyFact]
        public void Create_FrameworkAssembly_HasExpectedWellKnownIdentity()
        {
            // Opens a known .NET Framework 4 assembly from the system path and validates every
            // identity property against its compile-time-known values. This is the cross-cutting
            // regression test for both implementations:
            //   - On .NETCore (PEReader-based), validates the System.Reflection.Metadata path.
            //   - On net472 (CLR metadata COM), validates CoCreateInstance -> OpenScope(IMetaDataImport2)
            //     -> QueryInterface(IMetaDataAssemblyImport) + IReferenceIdentity.GetAttribute.
            // Both must agree on the same well-known assembly, by name / version / PKT / arch.
            string accessibilityPath = TryGetFramework4AccessibilityDllPath();
            if (accessibilityPath is null)
            {
                Assert.Skip(".NET Framework 4 Accessibility.dll not present on this machine.");
            }

            using MetadataReader reader = MetadataReader.Create(accessibilityPath);
            reader.ShouldNotBeNull();

            reader.Name.ShouldBe("Accessibility");
            reader.Version.ShouldBe("4.0.0.0");
            // Well-known ECMA public key token shared by all .NET Framework reference assemblies.
            reader.PublicKeyToken.ShouldBe("B03F5F7F11D50A3A");
            // Pure-managed reference assembly — should be MSIL on every architecture.
            reader.ProcessorArchitecture.ShouldBe("msil");

            // Accessibility.dll carries [AssemblyDescription("...")] in its manifest.
            reader.HasAssemblyAttribute("System.Reflection.AssemblyDescriptionAttribute").ShouldBeTrue();
        }

        [WindowsOnlyFact]
        public void MultipleConcurrentReaders_DoNotShareState()
        {
            // Opens readers against two different assemblies in the same process and verifies the
            // results don't get mixed up. On net472 this exercises the AgileComPointer GIT-based
            // agility — each instance must hold its own independent registration / cookie.
            string accessibilityPath = TryGetFramework4AccessibilityDllPath();
            if (accessibilityPath is null)
            {
                Assert.Skip(".NET Framework 4 Accessibility.dll not present on this machine.");
            }

            using MetadataReader r1 = MetadataReader.Create(accessibilityPath);
            using MetadataReader r2 = MetadataReader.Create(ThisAssemblyPath);

            r1.ShouldNotBeNull();
            r2.ShouldNotBeNull();

            r1.Name.ShouldBe("Accessibility");
            r2.Name.ShouldBe(ThisAssemblyName.Name);

            // Re-read the first one after the second was consumed — caches and pointers should still be intact.
            r1.Version.ShouldBe("4.0.0.0");
            r1.PublicKeyToken.ShouldBe("B03F5F7F11D50A3A");
            r2.Version.ShouldBe(ThisAssemblyName.Version.ToString());
        }

        [Fact]
        public void ConcurrentPropertyAccess_OnSameInstance_IsThreadSafe()
        {
            // The Name/Version/Culture/PKT/ProcessorArchitecture properties and the custom-attribute
            // cache are lazy-initialized under `lock (this)`. Hammer both code paths from multiple
            // threads to make sure the double-checked init doesn't race.
            using MetadataReader reader = MetadataReader.Create(ThisAssemblyPath);
            reader.ShouldNotBeNull();

            string expectedName = ThisAssemblyName.Name;
            string expectedVersion = ThisAssemblyName.Version.ToString();
            const int iterationsPerTask = 200;

            Task[] tasks =
            [
                Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerTask; i++)
                    {
                        reader.Name.ShouldBe(expectedName);
                        reader.Version.ShouldBe(expectedVersion);
                    }
                }),
                Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerTask; i++)
                    {
                        reader.HasAssemblyAttribute("System.Runtime.Versioning.TargetFrameworkAttribute").ShouldBeTrue();
                        reader.HasAssemblyAttribute("Nonexistent.Attribute").ShouldBeFalse();
                    }
                }),
                Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerTask; i++)
                    {
                        // ProcessorArchitecture and PublicKeyToken go through the same Attributes cache as Name.
                        reader.ProcessorArchitecture.ShouldNotBeNullOrEmpty();
                        _ = reader.PublicKeyToken;
                        _ = reader.Culture;
                    }
                }),
            ];

            Task.WaitAll(tasks);
        }

        [Fact]
        public void HasAssemblyAttributes_Batch_AgreesWithSingular()
        {
            // The batch overload was added to share a single GIT acquisition + GetAssemblyFromScope
            // across multiple probes on the net472 path. It must return the same per-name result
            // the singular `HasAssemblyAttribute(name)` overload returns. Mixes a known-present
            // attribute (every SDK-built assembly has TargetFrameworkAttribute) with two clearly
            // bogus names so both true and false outcomes are exercised in one call.
            using MetadataReader reader = MetadataReader.Create(ThisAssemblyPath);
            reader.ShouldNotBeNull();

            string[] names =
            [
                "System.Runtime.Versioning.TargetFrameworkAttribute",
                "Definitely.Not.A.Real.Attribute",
                "Another.Missing.AttributeName",
            ];

            bool[] batch = new bool[names.Length];
            reader.HasAssemblyAttributes(names, batch);

            for (int i = 0; i < names.Length; i++)
            {
                batch[i].ShouldBe(reader.HasAssemblyAttribute(names[i]), $"mismatch on '{names[i]}'");
            }

            // Sanity: the well-known one must be present and the bogus ones must be absent.
            batch[0].ShouldBeTrue();
            batch[1].ShouldBeFalse();
            batch[2].ShouldBeFalse();
        }
    }
}
