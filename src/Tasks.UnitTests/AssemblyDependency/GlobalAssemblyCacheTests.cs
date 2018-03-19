using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using SystemProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference GlobalAssemblyCache.
    /// </summary>
    public sealed class GlobalAssemblyCacheTests : ResolveAssemblyReferenceTestFixture
    {
        private const string system4 = "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
        private const string system2 = "System, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
        private const string system1 = "System, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
        private const string systemNotStrong = "System, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null";

        private const string system4Path = "c:\\clr4\\System.dll";
        private const string system2Path = "c:\\clr2\\System.dll";
        private const string system1Path = "c:\\clr2\\System1.dll";

        private GetAssemblyRuntimeVersion _runtimeVersion = new GetAssemblyRuntimeVersion(MockGetRuntimeVersion);
        private GetPathFromFusionName _getPathFromFusionName = new GetPathFromFusionName(MockGetPathFromFusionName);
        private GetGacEnumerator _gacEnumerator = new GetGacEnumerator(MockAssemblyCacheEnumerator);

        /// <summary>
        /// Verify when the GAC enumerator returns
        ///
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        ///
        /// And we target 2.0 runtime that we get the Version 2.0.0.0 system.
        ///
        /// This test two aspects. First that we get the correct runtime, second that we get the highest version for that assembly in the runtime.
        /// </summary>
        [Fact]
        public void VerifySimpleNamev2057020()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("2.0.57027"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, false);
            Assert.NotNull(path);
            Assert.True(path.Equals(system2Path, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Verify when the GAC enumerator returns
        ///
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        ///
        /// And we target 2.0 runtime that we get the Version 2.0.0.0 system.
        ///
        /// Verify that by setting the wants specific version to true that we will return the highest version when only the simple name is used.
        /// Essentially specific version for the gac resolver means do not filter by runtime.
        /// </summary>
        [Fact]
        public void VerifySimpleNamev2057020SpecificVersion()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("2.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.NotNull(path);
            Assert.True(path.Equals(system4Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when the GAC enumerator returns
        ///
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        ///
        /// And we target 2.0 runtime that we get the Version 2.0.0.0 system.
        ///
        /// Verify that by setting the wants specific version to true that we will return the highest version when only the simple name is used.
        /// Essentially specific version for the gac resolver means do not filter by runtime.
        /// </summary>
        [Fact]
        public void VerifyFusionNamev2057020SpecificVersion()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, Version=2.0.0.0");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("2.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.NotNull(path);
            Assert.True(path.Equals(system2Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when the GAC enumerator returns
        ///
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        ///
        /// And we target 4.0 runtime that we get the Version 4.0.0.0 system.
        ///
        /// This test two aspects. First that we get the correct runtime, second that we get the highest version for that assembly in the runtime.
        /// </summary>
        [Fact]
        public void VerifySimpleNamev40()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("4.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, false);
            Assert.NotNull(path);
            Assert.True(path.Equals(system4Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when the GAC enumerator returns
        ///
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        ///
        /// And we target 4.0 runtime that we get the Version 4.0.0.0 system.
        ///
        /// Verify that by setting the wants specific version to true that we will return the highest version when only the simple name is used.
        /// Essentially specific version for the gac resolver means do not filter by runtime.
        /// </summary>
        [Fact]
        public void VerifySimpleNamev40SpecificVersion()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("4.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.NotNull(path);
            Assert.True(path.Equals(system4Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when the GAC enumerator returns
        ///
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        ///
        ///
        /// Verify that by setting the wants specific version to true that we will return the highest version when only the simple name is used.
        /// Essentially specific version for the gac resolver means do not filter by runtime.
        /// </summary>
        [Fact]
        public void VerifyFusionNamev40SpecificVersion()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, Version=4.0.0.0");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("4.0.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.NotNull(path);
            Assert.True(path.Equals(system4Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when a assembly name is passed in which has the public key explicitly set to null that we return null as the assembly cannot be in the gac.
        /// </summary>
        [Fact]
        public void VerifyEmptyPublicKeyspecificVersion()
        {
            Assert.Throws<FileLoadException>(() =>
            {
                AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=");
                string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            }
           );
        }

        /// <summary>
        /// Verify when a assembly name is passed in which has the public key explicitly set to null that we return null as the assembly cannot be in the gac.
        /// </summary>
        [Fact]
        public void VerifyNullPublicKey()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=null");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, false);
            Assert.Null(path);
        }

        /// <summary>
        /// Verify when a assembly name is passed in which has the public key explicitly set to null that we return null as the assembly cannot be in the gac.
        /// </summary>
        [Fact]
        public void VerifyNullPublicKeyspecificVersion()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=null");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.Null(path);
        }


        /// <summary>
        /// When a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
        /// this was causing the GAC (api's) to crash.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void VerifyProcessorArchitectureDoesNotCrash()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, null /* use the real gac enumerator*/, false);
            Assert.Null(path);
        }

        /// <summary>
        /// When a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
        /// this was causing the GAC (api's) to crash.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void VerifyProcessorArchitectureDoesNotCrashSpecificVersion()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, null /* use the real gac enumerator*/, true);
            Assert.Null(path);
        }

        /// <summary>
        /// See bug 648678,  when a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
        /// this was causing the GAC (api's) to crash.
        /// </summary>
        [Fact]
        public void VerifyProcessorArchitectureDoesNotCrashFullFusionName()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, getRuntimeVersion, new Version("2.0.50727"), true, new FileExists(MockFileExists), _getPathFromFusionName, null /* use the real gac enumerator*/, false);
            Assert.Null(path);
        }

        /// <summary>
        /// When a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
        /// this was causing the GAC (api's) to crash.
        /// </summary>
        [Fact]
        public void VerifyProcessorArchitectureDoesNotCrashFullFusionNameSpecificVersion()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, getRuntimeVersion, new Version("2.0.50727"), true, new FileExists(MockFileExists), _getPathFromFusionName, null /* use the real gac enumerator*/, true);
            Assert.Null(path);
        }


        // System.Runtime dependency calculation tests

        // No dependency
        [Fact]
        public void SystemRuntimeDepends_No_Build()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Regular"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\SystemRuntime\Regular.dll");

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;
            Assert.True(
                t.Execute
                (
                    fileExists,
                    directoryExists,
                    getDirectories,
                    getAssemblyName,
                    getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                    getRegistrySubKeyNames,
                    getRegistrySubKeyDefaultValue,
#endif
                    getLastWriteTime,
                    getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                    openBaseKey,
#endif
                    checkIfAssemblyIsInGac,
                    isWinMDFile,
                    readMachineTypeFromPEHeader
                )
            );

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "false", StringComparison.OrdinalIgnoreCase)); //                 "Expected no System.Runtime dependency found during build."
            Assert.True(string.Equals(t.DependsOnNETStandard, "false", StringComparison.OrdinalIgnoreCase)); //                   "Expected no netstandard dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(
                t.Execute
                (
                    fileExists,
                    directoryExists,
                    getDirectories,
                    getAssemblyName,
                    getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                    getRegistrySubKeyNames,
                    getRegistrySubKeyDefaultValue,
#endif
                    getLastWriteTime,
                    getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                    openBaseKey,
#endif
                    checkIfAssemblyIsInGac,
                    isWinMDFile,
                    readMachineTypeFromPEHeader
                )
            );

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "false", StringComparison.OrdinalIgnoreCase)); //                 "Expected no System.Runtime dependency found during intellibuild."
            Assert.True(string.Equals(t.DependsOnNETStandard, "false", StringComparison.OrdinalIgnoreCase)); //                   "Expected no netstandard dependency found during intellibuild."
        }


        // Direct dependency
        [Fact]
        public void SystemRuntimeDepends_Yes()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("System.Runtime"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\SystemRuntime\System.Runtime.dll");

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(
                t.Execute
                (
                    fileExists,
                    directoryExists,
                    getDirectories,
                    getAssemblyName,
                    getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                    getRegistrySubKeyNames,
                    getRegistrySubKeyDefaultValue,
#endif
                    getLastWriteTime,
                    getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                    openBaseKey,
#endif
                    checkIfAssemblyIsInGac,
                    isWinMDFile,
                    readMachineTypeFromPEHeader
                )
            );

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(
                t.Execute
                (
                    fileExists,
                    directoryExists,
                    getDirectories,
                    getAssemblyName,
                    getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                    getRegistrySubKeyNames,
                    getRegistrySubKeyDefaultValue,
#endif
                    getLastWriteTime,
                    getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                    openBaseKey,
#endif
                    checkIfAssemblyIsInGac,
                    isWinMDFile,
                    readMachineTypeFromPEHeader
                )
            );

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during intellibuild."
        }

        // Indirect dependency
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void SystemRuntimeDepends_Yes_Indirect()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Portable"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\SystemRuntime\Portable.dll");

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(
                t.Execute
                (
                    fileExists,
                    directoryExists,
                    getDirectories,
                    getAssemblyName,
                    getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                    getRegistrySubKeyNames,
                    getRegistrySubKeyDefaultValue,
#endif
                    getLastWriteTime,
                    getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                    openBaseKey,
#endif
                    checkIfAssemblyIsInGac,
                    isWinMDFile,
                    readMachineTypeFromPEHeader
                )
            );

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(
                t.Execute
                (
                    fileExists,
                    directoryExists,
                    getDirectories,
                    getAssemblyName,
                    getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                    getRegistrySubKeyNames,
                    getRegistrySubKeyDefaultValue,
#endif
                    getLastWriteTime,
                    getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                    openBaseKey,
#endif
                    checkIfAssemblyIsInGac,
                    isWinMDFile,
                    readMachineTypeFromPEHeader
                )
            );

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during intellibuild."
        }

        [Fact]
        public void SystemRuntimeDepends_Yes_Indirect_ExternallyResolved()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Portable"),
            };

            t.Assemblies[0].SetMetadata("ExternallyResolved", "true");
            t.Assemblies[0].SetMetadata("HintPath", s_portableDllPath);

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac, isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during intellibuild."
        }

        [Fact]
        public void NETStandardDepends_Yes()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("netstandard"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\NetStandard\netstandard.dll");

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during intellibuild."
        }

        [Fact]
        public void NETStandardDepends_Yes_Indirect()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("netstandardlibrary"),
            };

            t.Assemblies[0].SetMetadata("HintPath", s_netstandardLibraryDllPath);

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Console.WriteLine (((MockEngine)t.BuildEngine).Log);
            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected netstandard dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected netstandard dependency found during intellibuild."
        }


        [Fact]
        public void NETStandardDepends_Yes_Indirect_ExternallyResolved()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("netstandardlibrary"),
            };

            t.Assemblies[0].SetMetadata("ExternallyResolved", "true");
            t.Assemblies[0].SetMetadata("HintPath", s_netstandardLibraryDllPath);

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected netstandard dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected netstandard dependency found during intellibuild."
        }

        [Fact]
        public void DependsOn_NETStandard_and_SystemRuntime()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("netstandardlibrary"),
                new TaskItem("Portable"),
            };

            t.Assemblies[0].SetMetadata("HintPath", s_netstandardLibraryDllPath);
            t.Assemblies[1].SetMetadata("HintPath", s_portableDllPath);

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during build."
            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                   "Expected netstandard dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during intellibuild."
            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                   "Expected netstandard dependency found during intellibuild."
        }

        [Fact]
        public void DependsOn_NETStandard_and_SystemRuntime_ExternallyResolved()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("netstandardlibrary"),
                new TaskItem("Portable"),
            };

            t.Assemblies[0].SetMetadata("ExternallyResolved", "true");
            t.Assemblies[0].SetMetadata("HintPath", s_netstandardLibraryDllPath);
            t.Assemblies[1].SetMetadata("ExternallyResolved", "true");
            t.Assemblies[1].SetMetadata("HintPath", s_portableDllPath);

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during build."
            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                   "Expected netstandard dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(t.Execute(
                fileExists,
                directoryExists,
                getDirectories,
                getAssemblyName,
                getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                getLastWriteTime,
                getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during intellibuild."
            Assert.True(string.Equals(t.DependsOnNETStandard, "true", StringComparison.OrdinalIgnoreCase)); //                   "Expected netstandard dependency found during intellibuild."
        }

        #region HelperDelegates

        private static string MockGetRuntimeVersion(string path)
        {
            if (path.Equals(system1Path, StringComparison.OrdinalIgnoreCase))
            {
                return "v2.0.50727";
            }

            if (path.Equals(system4Path, StringComparison.OrdinalIgnoreCase))
            {
                return "v4.0.0";
            }

            if (path.Equals(system2Path, StringComparison.OrdinalIgnoreCase))
            {
                return "v2.0.50727";
            }

            return String.Empty;
        }

        private bool MockFileExists(string path)
        {
            return true;
        }

        private static string MockGetPathFromFusionName(string strongName)
        {
            if (strongName.Equals(system1, StringComparison.OrdinalIgnoreCase))
            {
                return system1Path;
            }

            if (strongName.Equals(system2, StringComparison.OrdinalIgnoreCase))
            {
                return system2Path;
            }

            if (strongName.Equals(systemNotStrong, StringComparison.OrdinalIgnoreCase))
            {
                return system2Path;
            }

            if (strongName.Equals(system4, StringComparison.OrdinalIgnoreCase))
            {
                return system4Path;
            }

            return String.Empty;
        }

        private static IEnumerable<AssemblyNameExtension> MockAssemblyCacheEnumerator(string strongName)
        {
            List<string> listOfAssemblies = new List<string>();

            if (strongName.StartsWith("System, Version=2.0.0.0", StringComparison.OrdinalIgnoreCase))
            {
                listOfAssemblies.Add(system2);
            }
            else if (strongName.StartsWith("System, Version=4.0.0.0", StringComparison.OrdinalIgnoreCase))
            {
                listOfAssemblies.Add(system4);
            }
            else
            {
                listOfAssemblies.Add(system1);
                listOfAssemblies.Add(system2);
                listOfAssemblies.Add(system4);
            }
            return new MockEnumerator(listOfAssemblies);
        }

        internal class MockEnumerator : IEnumerable<AssemblyNameExtension>
        {
            private List<string> _assembliesToEnumerate = null;
            private List<string>.Enumerator _enumerator;

            public MockEnumerator(List<string> assembliesToEnumerate)
            {
                _assembliesToEnumerate = assembliesToEnumerate;

                _enumerator = assembliesToEnumerate.GetEnumerator();
            }


            public IEnumerator<AssemblyNameExtension> GetEnumerator()
            {
                foreach (string assembly in _assembliesToEnumerate)
                {
                    yield return new AssemblyNameExtension(assembly);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator)GetEnumerator();
            }
        }

        #endregion
    }
}
