// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class StrongNameUtils_Tests
    {
        /// <summary>
        /// The BCL (mscorlib) is always a fully signed managed assembly.
        /// </summary>
        [Fact]
        public void GetAssemblyStrongNameLevel_FullySignedAssembly_ReturnsFullySigned()
        {
            string bclPath = typeof(string).Assembly.Location;
            bclPath.ShouldNotBeNullOrEmpty();
            File.Exists(bclPath).ShouldBeTrue();

            StrongNameUtils.GetAssemblyStrongNameLevel(bclPath).ShouldBe(StrongNameLevel.FullySigned);
        }

        /// <summary>
        /// A managed assembly with a strong-name signature directory present but with the
        /// <c>COMIMAGE_FLAGS_STRONGNAMESIGNED</c> bit cleared must be reported as DelaySigned.
        /// We emit one on the fly by attaching a public key and tagging the assembly
        /// <c>[assembly: AssemblyDelaySign(true)]</c>.
        /// </summary>
        [Fact]
        public void GetAssemblyStrongNameLevel_DelaySignedAssembly_ReturnsDelaySigned()
        {
            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFolder folder = env.CreateFolder();

            AssemblyName name = new AssemblyName("DelaySignedTestAssembly_" + Guid.NewGuid().ToString("N"));
            name.SetPublicKey(typeof(string).Assembly.GetName().GetPublicKey());

            // Mark as delay-signed via AssemblyDelaySignAttribute.
            CustomAttributeBuilder delaySign = new CustomAttributeBuilder(
                typeof(AssemblyDelaySignAttribute).GetConstructor([typeof(bool)]),
                [true]);

            AssemblyBuilder asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                name,
                AssemblyBuilderAccess.Save,
                folder.Path);
            asmBuilder.SetCustomAttribute(delaySign);

            string fileName = name.Name + ".dll";
            ModuleBuilder modBuilder = asmBuilder.DefineDynamicModule(name.Name, fileName);
            modBuilder.DefineType("Stub", TypeAttributes.Public).CreateType();
            asmBuilder.Save(fileName);

            string asmPath = Path.Combine(folder.Path, fileName);
            File.Exists(asmPath).ShouldBeTrue();

            StrongNameUtils.GetAssemblyStrongNameLevel(asmPath).ShouldBe(StrongNameLevel.DelaySigned);
        }

        /// <summary>
        /// An unsigned managed assembly (COR20 header present, no strong name signature directory)
        /// must be reported as None.
        /// </summary>
        [Fact]
        public void GetAssemblyStrongNameLevel_UnsignedManagedAssembly_ReturnsNone()
        {
            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFolder folder = env.CreateFolder();

            // Emit a trivial dynamic assembly to disk with no key file / key pair attached.
            AssemblyName name = new AssemblyName("UnsignedTestAssembly_" + Guid.NewGuid().ToString("N"));
            AssemblyBuilder asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                name,
                AssemblyBuilderAccess.Save,
                folder.Path);
            string fileName = name.Name + ".dll";
            ModuleBuilder modBuilder = asmBuilder.DefineDynamicModule(name.Name, fileName);
            modBuilder.DefineType("Stub", TypeAttributes.Public).CreateType();
            asmBuilder.Save(fileName);

            string asmPath = Path.Combine(folder.Path, fileName);
            File.Exists(asmPath).ShouldBeTrue();

            StrongNameUtils.GetAssemblyStrongNameLevel(asmPath).ShouldBe(StrongNameLevel.None);
        }

        /// <summary>
        /// A native PE without a COR20 header must be reported as Unknown (not None) so that
        /// callers like AxTlbBaseReference.SigningRequirementsMatchExistingWrapper can distinguish
        /// "no managed signature present" from "not a managed image at all".
        /// </summary>
        [WindowsOnlyFact]
        public void GetAssemblyStrongNameLevel_NativePE_ReturnsUnknown()
        {
            string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string kernel32 = Path.Combine(systemRoot, "System32", "kernel32.dll");
            File.Exists(kernel32).ShouldBeTrue();

            StrongNameUtils.GetAssemblyStrongNameLevel(kernel32).ShouldBe(StrongNameLevel.Unknown);
        }

        [Fact]
        public void GetAssemblyStrongNameLevel_NonExistentFile_ReturnsUnknown()
        {
            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile missing = env.GetTempFile(".dll");
            File.Exists(missing.Path).ShouldBeFalse();

            StrongNameUtils.GetAssemblyStrongNameLevel(missing.Path).ShouldBe(StrongNameLevel.Unknown);
        }

        [Fact]
        public void GetAssemblyStrongNameLevel_GarbageFile_ReturnsUnknown()
        {
            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile garbage = env.CreateFile();
            File.WriteAllBytes(garbage.Path, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09]);

            StrongNameUtils.GetAssemblyStrongNameLevel(garbage.Path).ShouldBe(StrongNameLevel.Unknown);
        }

        [Fact]
        public void GetAssemblyStrongNameLevel_EmptyFile_ReturnsUnknown()
        {
            using TestEnvironment env = TestEnvironment.Create();
            TransientTestFile empty = env.CreateFile();

            StrongNameUtils.GetAssemblyStrongNameLevel(empty.Path).ShouldBe(StrongNameLevel.Unknown);
        }

        [Fact]
        public void GetAssemblyStrongNameLevel_NullPath_Throws()
        {
            Should.Throw<ArgumentNullException>(() => StrongNameUtils.GetAssemblyStrongNameLevel(null));
        }
    }
}

#endif

