// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

using Microsoft.Build.Tasks;

using Marshal = System.Runtime.InteropServices.Marshal;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// All possible failure points in MockTypeLib/MockTypeInfo classes. When adding new functionality there,
    /// add a new failure point here and add a corresponding call to faultInjector.FailurePointThrow in the method.
    /// </summary>
    public enum MockTypeLibrariesFailurePoints
    {
        // MockTypeLibrary.ITypeLib
        ITypeLib_GetLibAttr = 1,
        ITypeLib_ReleaseTLibAttr,
        ITypeLib_GetTypeInfo,
        ITypeLib_GetTypeInfoCount,

        // MockTypeLibrary.ITypeLib2
        ITypeLib2_GetCustData,

        // MockTypeInfo.ITypeInfo
        ITypeInfo_GetContainingTypeLib,
        ITypeInfo_GetTypeAttr,
        ITypeInfo_ReleaseTypeAttr,
        ITypeInfo_GetRefTypeOfImplType,
        ITypeInfo_GetRefTypeInfo,
        ITypeInfo_GetVarDesc,
        ITypeInfo_ReleaseVarDesc,
        ITypeInfo_GetFuncDesc,
        ITypeInfo_ReleaseFuncDesc,
        ITypeInfo_GetDocumentation
    }

    /// <summary>
    /// Mock class for the ITypeLib interface
    /// </summary>
    public class MockTypeLib : ITypeLib, ITypeLib2
    {
        private List<MockTypeInfo> _containedTypeInfos;

        public List<MockTypeInfo> ContainedTypeInfos
        {
            get
            {
                return _containedTypeInfos;
            }
        }

        private TYPELIBATTR _typeLibAttributes;

        public TYPELIBATTR Attributes
        {
            get
            {
                return _typeLibAttributes;
            }
        }

        private string _exportedFromComPlus;

        public string ExportedFromComPlus
        {
            set
            {
                _exportedFromComPlus = value;
            }
        }

        // helper class for unmanaged allocations and leak tracking
        private MockUnmanagedMemoryHelper _memoryHelper;

        // helper class for injecting failures into chosen method calls
        private MockFaultInjectionHelper<MockTypeLibrariesFailurePoints> _faultInjector;

        /// <summary>
        /// Public constructor
        /// </summary>
        public MockTypeLib()
        {
            _containedTypeInfos = new List<MockTypeInfo>();
            _typeLibAttributes.guid = Guid.NewGuid();
            _exportedFromComPlus = null;

            _memoryHelper = new MockUnmanagedMemoryHelper();
            _faultInjector = new MockFaultInjectionHelper<MockTypeLibrariesFailurePoints>();
        }

        /// <summary>
        /// Create a mock type library with a specific guid
        /// </summary>
        /// <param name="guid"></param>
        public MockTypeLib(Guid guid)
            : this()
        {
            _typeLibAttributes.guid = guid;
        }

        /// <summary>
        /// Tells the type lib to inject a specific failure (exception) at the chosen failure point.
        /// </summary>
        /// <param name="failurePoint"></param>
        /// <param name="exceptionToThrow"></param>
        public void InjectFailure(MockTypeLibrariesFailurePoints failurePoint, Exception exceptionToThrow)
        {
            _faultInjector.InjectFailure(failurePoint, exceptionToThrow);
        }

        /// <summary>
        /// Add a new type info to the type library
        /// </summary>
        /// <param name="typeInfo"></param>
        public void AddTypeInfo(MockTypeInfo typeInfo)
        {
            _containedTypeInfos.Add(typeInfo);
            typeInfo.ContainingTypeLib = this;
            typeInfo.IndexInContainingTypeLib = _containedTypeInfos.Count - 1;
            typeInfo.SetFaultInjector(_faultInjector);
        }

        /// <summary>
        /// Helper method for verifying there are no memory leaks from unmanaged allocations
        /// </summary>
        public void AssertAllHandlesReleased()
        {
            _memoryHelper.AssertAllHandlesReleased();

            foreach (MockTypeInfo typeInfo in _containedTypeInfos)
            {
                typeInfo.AssertAllHandlesReleased();
            }
        }

        #region Implemented ITypeLib members

        public void GetLibAttr(out IntPtr ppTLibAttr)
        {
            // Fail BEFORE allocating the handle to avoid leaks. If the real COM object fails in this method
            // and doesn't return the handle or clean it up itself there's not much we can do to avoid the leak.
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeLib_GetLibAttr);

            ppTLibAttr = _memoryHelper.AllocateHandle(Marshal.SizeOf<TYPELIBATTR>());
            Marshal.StructureToPtr(this.Attributes, ppTLibAttr, false);
        }

        public void ReleaseTLibAttr(IntPtr pTLibAttr)
        {
            _memoryHelper.FreeHandle(pTLibAttr);

            // Fail AFTER releasing the handle to avoid leaks. If the real COM object fails in this method
            // there's really nothing we can do to avoid leaking stuff
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeLib_ReleaseTLibAttr);
        }

        public void GetTypeInfo(int index, out ITypeInfo ppTI)
        {
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeLib_GetTypeInfo);

            Assert.True(index >= 0 && index < _containedTypeInfos.Count);
            ppTI = _containedTypeInfos[index];
        }

        public int GetTypeInfoCount()
        {
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeLib_GetTypeInfoCount);

            return _containedTypeInfos.Count;
        }

        #endregion

        #region Stubbed ITypeLib members

        public void FindName(string szNameBuf, int lHashVal, ITypeInfo[] ppTInfo, int[] rgMemId, ref short pcFound)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetDocumentation(int index, out string strName, out string strDocString, out int dwHelpContext, out string strHelpFile)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetTypeComp(out ITypeComp ppTComp)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetTypeInfoOfGuid(ref Guid guid, out ITypeInfo ppTInfo)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetTypeInfoType(int index, out TYPEKIND pTKind)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public bool IsName(string szNameBuf, int lHashVal)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion

        #region Implemented ITypeLib2 members

        public void GetCustData(ref Guid guid, out object pVarVal)
        {
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeLib2_GetCustData);

            if (guid == NativeMethods.GUID_ExportedFromComPlus)
            {
                pVarVal = _exportedFromComPlus;
            }
            else
            {
                Assert.True(false, "unexpected guid in ITypeLib2.GetCustData");
                pVarVal = null;
            }
        }

        #endregion

        #region Stubbed ITypeLib2 Members

        public void GetAllCustData(IntPtr pCustData)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetDocumentation2(int index, out string pbstrHelpString, out int pdwHelpStringContext, out string pbstrHelpStringDll)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetLibStatistics(IntPtr pcUniqueNames, out int pcchUniqueNames)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
