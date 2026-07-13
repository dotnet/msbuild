// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN

using System;
using System.Collections.Generic;
using Microsoft.Build.Tasks;
using Windows.Win32.System.Com;
using Windows.Win32.System.Ole;
using COMException = System.Runtime.InteropServices.COMException;
using Marshal = System.Runtime.InteropServices.Marshal;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class ComReferenceWalker_Tests
    {
        // Obtains a COM-callable-wrapper pointer for the managed mock type library (which implements the
        // built-in System.Runtime.InteropServices.ComTypes.ITypeLib) and hands it to the struct-based walker.
        private static unsafe void AnalyzeTypeLibrary(ComDependencyWalker walker, MockTypeLib typeLib)
        {
            IntPtr typeLibPtr = Marshal.GetComInterfaceForObject(typeLib, typeof(System.Runtime.InteropServices.ComTypes.ITypeLib));
            try
            {
                walker.AnalyzeTypeLibrary((ITypeLib*)typeLibPtr);
            }
            finally
            {
                Marshal.Release(typeLibPtr);
            }
        }

        private void AssertDependenciesContainTypeLib(TLIBATTR[] dependencies, MockTypeLib typeLib, bool contains)
        {
            AssertDependenciesContainTypeLib("", dependencies, typeLib, contains);
        }

        private void AssertDependenciesContainTypeLib(string message, TLIBATTR[] dependencies, MockTypeLib typeLib, bool contains)
        {
            bool dependencyExists = false;

            foreach (TLIBATTR attr in dependencies)
            {
                if (attr.guid == typeLib.Attributes.guid)
                {
                    dependencyExists = true;
                    break;
                }
            }

            Assert.AreEqual(contains, dependencyExists);
        }

        [MSBuildTestMethod]
        public void WalkTypeInfosInEmptyLibrary()
        {
            MockTypeLib typeLib = new MockTypeLib();

            ComDependencyWalker walker = new ComDependencyWalker();
            AnalyzeTypeLibrary(walker, typeLib);
            Assert.AreEqual(0, walker.GetDependencies().GetLength(0));

            typeLib.AssertAllHandlesReleased();
        }

        private void CreateTwoTypeLibs(out MockTypeLib mainTypeLib, out MockTypeLib dependencyTypeLib)
        {
            mainTypeLib = new MockTypeLib();
            mainTypeLib.AddTypeInfo(new MockTypeInfo());

            dependencyTypeLib = new MockTypeLib();
            dependencyTypeLib.AddTypeInfo(new MockTypeInfo());
        }

        private TLIBATTR[] RunDependencyWalker(MockTypeLib mainTypeLib, MockTypeLib dependencyTypeLib, bool dependencyShouldBePresent)
        {
            ComDependencyWalker walker = new ComDependencyWalker();
            AnalyzeTypeLibrary(walker, mainTypeLib);

            TLIBATTR[] dependencies = walker.GetDependencies();

            // types from the main type library should be in the dependency list
            AssertDependenciesContainTypeLib(dependencies, mainTypeLib, true);

            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib, dependencyShouldBePresent);

            mainTypeLib.AssertAllHandlesReleased();
            dependencyTypeLib.AssertAllHandlesReleased();

            return dependencies;
        }

        /// <summary>
        /// A type in the main type library implements an interface from a dependent type library
        /// </summary>
        [MSBuildTestMethod]
        public void ImplementedInterfaces()
        {
            MockTypeLib mainTypeLib, dependencyTypeLib;
            CreateTwoTypeLibs(out mainTypeLib, out dependencyTypeLib);

            mainTypeLib.ContainedTypeInfos[0].ImplementsInterface(dependencyTypeLib.ContainedTypeInfos[0]);

            RunDependencyWalker(mainTypeLib, dependencyTypeLib, true);
        }

        [MSBuildTestMethod]
        public void DefinedVariableUDT()
        {
            MockTypeLib mainTypeLib, dependencyTypeLib;
            CreateTwoTypeLibs(out mainTypeLib, out dependencyTypeLib);

            mainTypeLib.ContainedTypeInfos[0].DefinesVariable(dependencyTypeLib.ContainedTypeInfos[0]);

            RunDependencyWalker(mainTypeLib, dependencyTypeLib, true);
        }

        [MSBuildTestMethod]
        public void DefinedVariableUDTArray()
        {
            MockTypeLib mainTypeLib, dependencyTypeLib;
            CreateTwoTypeLibs(out mainTypeLib, out dependencyTypeLib);

            mainTypeLib.ContainedTypeInfos[0].DefinesVariable(new ArrayCompositeTypeInfo(dependencyTypeLib.ContainedTypeInfos[0]));

            RunDependencyWalker(mainTypeLib, dependencyTypeLib, true);
        }

        [MSBuildTestMethod]
        public void DefinedVariableUDTPtr()
        {
            MockTypeLib mainTypeLib, dependencyTypeLib;
            CreateTwoTypeLibs(out mainTypeLib, out dependencyTypeLib);

            mainTypeLib.ContainedTypeInfos[0].DefinesVariable(new PtrCompositeTypeInfo(dependencyTypeLib.ContainedTypeInfos[0]));

            RunDependencyWalker(mainTypeLib, dependencyTypeLib, true);
        }

        [MSBuildTestMethod]
        public void ThereAndBackAgain()
        {
            MockTypeLib mainTypeLib, dependencyTypeLib;
            CreateTwoTypeLibs(out mainTypeLib, out dependencyTypeLib);

            mainTypeLib.ContainedTypeInfos[0].DefinesVariable(new PtrCompositeTypeInfo(dependencyTypeLib.ContainedTypeInfos[0]));
            dependencyTypeLib.ContainedTypeInfos[0].ImplementsInterface(mainTypeLib.ContainedTypeInfos[0]);

            RunDependencyWalker(mainTypeLib, dependencyTypeLib, true);
        }

        [MSBuildTestMethod]
        public void ComplexComposition()
        {
            MockTypeLib mainTypeLib, dependencyTypeLib;
            CreateTwoTypeLibs(out mainTypeLib, out dependencyTypeLib);

            mainTypeLib.ContainedTypeInfos[0].DefinesVariable(
                new ArrayCompositeTypeInfo(new ArrayCompositeTypeInfo(new PtrCompositeTypeInfo(
                    new PtrCompositeTypeInfo(new ArrayCompositeTypeInfo(new PtrCompositeTypeInfo(dependencyTypeLib.ContainedTypeInfos[0])))))));

            RunDependencyWalker(mainTypeLib, dependencyTypeLib, true);
        }

        [MSBuildTestMethod]
        public void DefinedFunction()
        {
            MockTypeLib mainTypeLib, dependencyTypeLib1, dependencyTypeLib2, dependencyTypeLib3;
            CreateTwoTypeLibs(out mainTypeLib, out dependencyTypeLib1);
            CreateTwoTypeLibs(out dependencyTypeLib2, out dependencyTypeLib3);

            mainTypeLib.ContainedTypeInfos[0].DefinesFunction(
                new MockTypeInfo[] { dependencyTypeLib1.ContainedTypeInfos[0], dependencyTypeLib2.ContainedTypeInfos[0] },
                dependencyTypeLib3.ContainedTypeInfos[0]);

            TLIBATTR[] dependencies = RunDependencyWalker(mainTypeLib, dependencyTypeLib1, true);

            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib2, true);
            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib3, true);

            dependencyTypeLib2.AssertAllHandlesReleased();
            dependencyTypeLib3.AssertAllHandlesReleased();
        }

        [MSBuildTestMethod]
        public void IgnoreKnownOleTypes()
        {
            MockTypeLib mainTypeLib = new MockTypeLib();
            mainTypeLib.AddTypeInfo(new MockTypeInfo());

            MockTypeLib oleTypeLib = new MockTypeLib();
            oleTypeLib.AddTypeInfo(new MockTypeInfo(IUnknown.IID_Guid));
            oleTypeLib.AddTypeInfo(new MockTypeInfo(IDispatch.IID_Guid));
            oleTypeLib.AddTypeInfo(new MockTypeInfo(IDispatchEx.IID_Guid));
            oleTypeLib.AddTypeInfo(new MockTypeInfo(IEnumVARIANT.IID_Guid));
            oleTypeLib.AddTypeInfo(new MockTypeInfo(ITypeInfo.IID_Guid));

            // We don't check for this type in the ComDependencyWalker, so it doesn't get counted as a known OLE type.
            // It's too late in the Dev10 cycle to add it to shipping code without phenomenally good reason, but we should
            // re-examine this in Dev11.
            // oleTypeLib.AddTypeInfo(new MockTypeInfo(TYPEKIND.TKIND_ENUM));

            foreach (MockTypeInfo typeInfo in oleTypeLib.ContainedTypeInfos)
            {
                mainTypeLib.ContainedTypeInfos[0].DefinesVariable(typeInfo);
            }

            RunDependencyWalker(mainTypeLib, oleTypeLib, false);
        }

        [MSBuildTestMethod]
        public void IgnoreGuidType()
        {
            MockTypeLib mainTypeLib = new MockTypeLib();
            mainTypeLib.AddTypeInfo(new MockTypeInfo());

            MockTypeLib oleTypeLib = new MockTypeLib(NativeMethods.LIBID_StdOle);
            oleTypeLib.AddTypeInfo(new MockTypeInfo());
            oleTypeLib.ContainedTypeInfos[0].TypeName = "GUID";

            mainTypeLib.ContainedTypeInfos[0].DefinesVariable(oleTypeLib.ContainedTypeInfos[0]);

            RunDependencyWalker(mainTypeLib, oleTypeLib, false);
        }

        [MSBuildTestMethod]
        public void IgnoreNetExportedTypeLibs()
        {
            MockTypeLib mainTypeLib, dependencyTypeLib;
            CreateTwoTypeLibs(out mainTypeLib, out dependencyTypeLib);

            mainTypeLib.ContainedTypeInfos[0].DefinesFunction(
                new MockTypeInfo[] { dependencyTypeLib.ContainedTypeInfos[0] }, dependencyTypeLib.ContainedTypeInfos[0]);
            dependencyTypeLib.ExportedFromComPlus = "1";

            RunDependencyWalker(mainTypeLib, dependencyTypeLib, false);
        }

        /// <summary>
        /// The main type lib is broken... don't expect any results, but make sure we don't throw.
        /// </summary>
        [MSBuildTestMethod]
        public void FaultInjectionMainLib()
        {
            // The primary test here is that we don't throw, which can't be explicitly expressed in NUnit...
            // other asserts are secondary
            foreach (MockTypeLibrariesFailurePoints failurePoint in Enum.GetValues(typeof(MockTypeLibrariesFailurePoints)))
            {
                MockTypeLib mainTypeLib = new MockTypeLib();
                mainTypeLib.AddTypeInfo(new MockTypeInfo());

                // Make it the StdOle lib to exercise the ITypeInfo.GetDocumentation failure point
                MockTypeLib dependencyTypeLib = new MockTypeLib(NativeMethods.LIBID_StdOle);
                dependencyTypeLib.AddTypeInfo(new MockTypeInfo());

                COMException failureException = new COMException("unhandled exception in " + failurePoint.ToString());
                mainTypeLib.InjectFailure(failurePoint, failureException);
                dependencyTypeLib.InjectFailure(failurePoint, failureException);

                mainTypeLib.ContainedTypeInfos[0].ImplementsInterface(dependencyTypeLib.ContainedTypeInfos[0]);
                mainTypeLib.ContainedTypeInfos[0].DefinesVariable(dependencyTypeLib.ContainedTypeInfos[0]);
                mainTypeLib.ContainedTypeInfos[0].DefinesFunction(
                    new MockTypeInfo[] { dependencyTypeLib.ContainedTypeInfos[0] }, dependencyTypeLib.ContainedTypeInfos[0]);

                ComDependencyWalker walker = new ComDependencyWalker();
                AnalyzeTypeLibrary(walker, mainTypeLib);

                // The primary guarantee is that a failing type library never lets an exception escape the walker
                // (this test method would fail if one did). Injected failures reach the walker across the COM
                // (CCW) boundary as HRESULTs, so the walker records a re-wrapped COMException rather than the
                // original exception instance. Failure points in COM methods that cannot report an HRESULT
                // (GetTypeInfoCount, and the void Release* methods) are swallowed at the boundary and produce no
                // recorded problem, so allow either zero or one captured problem.
                Assert.IsTrue(walker.EncounteredProblems.Count <= 1, "Test failed for failure point " + failurePoint.ToString());
                foreach (Exception problem in walker.EncounteredProblems)
                {
                    Assert.IsExactInstanceOfType<COMException>(problem);
                }

                mainTypeLib.AssertAllHandlesReleased();
                dependencyTypeLib.AssertAllHandlesReleased();
            }
        }

        [MSBuildTestMethod]
        public void FullDependenciesWithIncrementalAnalysis()
        {
            MockTypeLib mainTypeLib1, mainTypeLib2, mainTypeLib3, dependencyTypeLib1, dependencyTypeLib2, dependencyTypeLib3;
            CreateTwoTypeLibs(out mainTypeLib1, out dependencyTypeLib1);
            CreateTwoTypeLibs(out mainTypeLib2, out dependencyTypeLib2);
            CreateTwoTypeLibs(out mainTypeLib3, out dependencyTypeLib3);

            mainTypeLib1.ContainedTypeInfos[0].DefinesVariable(dependencyTypeLib1.ContainedTypeInfos[0]);

            mainTypeLib2.ContainedTypeInfos[0].DefinesVariable(dependencyTypeLib1.ContainedTypeInfos[0]);
            mainTypeLib2.ContainedTypeInfos[0].DefinesVariable(dependencyTypeLib2.ContainedTypeInfos[0]);

            mainTypeLib3.ContainedTypeInfos[0].DefinesVariable(dependencyTypeLib1.ContainedTypeInfos[0]);
            mainTypeLib3.ContainedTypeInfos[0].DefinesVariable(dependencyTypeLib3.ContainedTypeInfos[0]);

            ComDependencyWalker walker = new ComDependencyWalker();

            AnalyzeTypeLibrary(walker, mainTypeLib1);
            TLIBATTR[] dependencies = walker.GetDependencies();
            ICollection<string> analyzedTypes = walker.GetAnalyzedTypeNames();

            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib1, true);
            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib2, false);
            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib3, false);
            Assert.AreEqual(2, analyzedTypes.Count);

            walker.ClearDependencyList();
            AnalyzeTypeLibrary(walker, mainTypeLib2);
            dependencies = walker.GetDependencies();
            analyzedTypes = walker.GetAnalyzedTypeNames();

            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib1, true);
            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib2, true);
            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib3, false);
            Assert.AreEqual(4, analyzedTypes.Count);

            walker.ClearDependencyList();
            AnalyzeTypeLibrary(walker, mainTypeLib3);
            dependencies = walker.GetDependencies();
            analyzedTypes = walker.GetAnalyzedTypeNames();

            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib1, true);
            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib2, false);
            AssertDependenciesContainTypeLib(dependencies, dependencyTypeLib3, true);
            Assert.AreEqual(6, analyzedTypes.Count);
        }
    }
}

#endif
