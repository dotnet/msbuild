// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

using Marshal = System.Runtime.InteropServices.Marshal;
using VarEnum = System.Runtime.InteropServices.VarEnum;
using IFixedTypeInfo = Microsoft.Build.Tasks.IFixedTypeInfo;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A generic interface for creating composite type infos - e.g. a safe array of pointers to something
    /// </summary>
    public interface ICompositeTypeInfo
    {
        TYPEDESC CreateTypeDesc(IntPtr finalTypeHRef, MockUnmanagedMemoryHelper memoryHelper);

        MockTypeInfo GetFinalTypeInfo();
    }

    /// <summary>
    /// Safe array composite type info
    /// </summary>
    public class ArrayCompositeTypeInfo : ICompositeTypeInfo
    {
        private ICompositeTypeInfo _baseElementType;

        public ArrayCompositeTypeInfo(ICompositeTypeInfo baseElement)
        {
            _baseElementType = baseElement;
        }

        #region ICreateTypeDesc Members

        public TYPEDESC CreateTypeDesc(IntPtr finalTypeHRef, MockUnmanagedMemoryHelper memoryHelper)
        {
            TYPEDESC typeDesc;
            typeDesc.vt = (short)VarEnum.VT_SAFEARRAY;
            typeDesc.lpValue = memoryHelper.AllocateHandle(Marshal.SizeOf<TYPEDESC>());
            Marshal.StructureToPtr(_baseElementType.CreateTypeDesc(finalTypeHRef, memoryHelper), typeDesc.lpValue, false);
            return typeDesc;
        }

        /// <summary>
        /// Defer to the base element to get the final type info - this will eventually terminate at a MockTypeInfo node 
        /// which returns itself
        /// </summary>
        /// <returns></returns>
        public MockTypeInfo GetFinalTypeInfo()
        {
            return _baseElementType.GetFinalTypeInfo();
        }

        #endregion
    }

    /// <summary>
    /// Pointer composite type info
    /// </summary>
    public class PtrCompositeTypeInfo : ICompositeTypeInfo
    {
        private ICompositeTypeInfo _baseElementType;

        public PtrCompositeTypeInfo(ICompositeTypeInfo baseElement)
        {
            _baseElementType = baseElement;
        }

        #region ICompositeTypeInfo Members

        public TYPEDESC CreateTypeDesc(IntPtr finalTypeHRef, MockUnmanagedMemoryHelper memoryHelper)
        {
            TYPEDESC typeDesc;
            typeDesc.vt = (short)VarEnum.VT_PTR;
            typeDesc.lpValue = memoryHelper.AllocateHandle(Marshal.SizeOf<TYPEDESC>());
            Marshal.StructureToPtr(_baseElementType.CreateTypeDesc(finalTypeHRef, memoryHelper), typeDesc.lpValue, false);
            return typeDesc;
        }

        public MockTypeInfo GetFinalTypeInfo()
        {
            return _baseElementType.GetFinalTypeInfo();
        }

        #endregion
    }

    /// <summary>
    /// All the information necessary to describe a single function signature
    /// </summary>
    public struct FuncInfo
    {
        public FuncInfo(ICompositeTypeInfo[] parameters, ICompositeTypeInfo returnType)
        {
            this.parameters = parameters;
            this.returnType = returnType;
        }

        public ICompositeTypeInfo[] parameters;
        public ICompositeTypeInfo returnType;
    }

    /// <summary>
    /// Mock class for the ITypeInfo interface
    /// </summary>
    public class MockTypeInfo : ITypeInfo, ICompositeTypeInfo, IFixedTypeInfo
    {
        static private int s_HREF_IMPLTYPES_OFFSET = 1000;
        static private int s_HREF_VARS_OFFSET = 2000;
        static private int s_HREF_FUNCSRET_OFFSET = 3000;
        static private int s_HREF_FUNCSPARAM_OFFSET = 4000;
        static private int s_HREF_FUNCSPARAM_OFFSET_PERFUNC = 100;
        static private int s_HREF_RANGE = 999;

        private MockTypeLib _containingTypeLib;

        public MockTypeLib ContainingTypeLib
        {
            set
            {
                _containingTypeLib = value;
            }
        }

        private int _indexInContainingTypeLib;

        public int IndexInContainingTypeLib
        {
            set
            {
                _indexInContainingTypeLib = value;
            }
        }

        private string _typeName;

        public string TypeName
        {
            set
            {
                _typeName = value;
            }
        }

        private TYPEATTR _typeAttributes;

        private List<MockTypeInfo> _implementedTypes;
        private List<ICompositeTypeInfo> _definedVariables;
        private List<FuncInfo> _definedFunctions;

        private MockUnmanagedMemoryHelper _memoryHelper;

        private MockFaultInjectionHelper<MockTypeLibrariesFailurePoints> _faultInjector;

        /// <summary>
        /// Default constructor
        /// </summary>
        public MockTypeInfo()
        {
            _implementedTypes = new List<MockTypeInfo>();
            _definedVariables = new List<ICompositeTypeInfo>();
            _definedFunctions = new List<FuncInfo>();

            _memoryHelper = new MockUnmanagedMemoryHelper();

            // each type has a unique guid
            _typeAttributes.guid = Guid.NewGuid();

            // default typekind value is TKIND_ENUM so just pick something else that doesn't have a special meaning in the code
            // (we skip enum type infos)
            _typeAttributes.typekind = TYPEKIND.TKIND_INTERFACE;
        }

        /// <summary>
        /// Use a known guid for creating this type info
        /// </summary>
        /// <param name="guid"></param>
        public MockTypeInfo(Guid guid)
            : this()
        {
            _typeAttributes.guid = guid;
        }

        /// <summary>
        /// Use a custom type kind
        /// </summary>
        /// <param name="typeKind"></param>
        public MockTypeInfo(TYPEKIND typeKind)
            : this()
        {
            _typeAttributes.typekind = typeKind;
        }

        /// <summary>
        /// Adds implementedType to the list of this type's implemented interfaces
        /// </summary>
        /// <param name="implementedType"></param>
        public void ImplementsInterface(MockTypeInfo implementedType)
        {
            _typeAttributes.cImplTypes++;
            _implementedTypes.Add(implementedType);
        }

        /// <summary>
        /// Adds implementedType to the list of this type's defined variables
        /// </summary>
        /// <param name="implementedType"></param>
        public void DefinesVariable(ICompositeTypeInfo variableType)
        {
            _typeAttributes.cVars++;
            _definedVariables.Add(variableType);
        }

        /// <summary>
        /// Adds a new function signature to the list of this type's implemented functions
        /// </summary>
        /// <param name="implementedType"></param>
        public void DefinesFunction(MockTypeInfo[] parameters, MockTypeInfo returnType)
        {
            _typeAttributes.cFuncs++;
            _definedFunctions.Add(new FuncInfo(parameters, returnType));
        }

        /// <summary>
        /// Sets the fault injection object for this type
        /// </summary>
        /// <param name="faultInjector"></param>
        public void SetFaultInjector(MockFaultInjectionHelper<MockTypeLibrariesFailurePoints> faultInjector)
        {
            _faultInjector = faultInjector;
        }

        /// <summary>
        /// Helper method for verifying there are no memory leaks
        /// </summary>
        public void AssertAllHandlesReleased()
        {
            _memoryHelper.AssertAllHandlesReleased();
        }

        #region IFixedTypeInfo members

        void IFixedTypeInfo.GetRefTypeOfImplType(int index, out System.IntPtr href)
        {
            Assert.True(index >= 0 && index < _typeAttributes.cImplTypes);

            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_GetRefTypeOfImplType);

            href = ((System.IntPtr)index + s_HREF_IMPLTYPES_OFFSET);
        }

        void IFixedTypeInfo.GetRefTypeInfo(System.IntPtr hRef, out IFixedTypeInfo ppTI)
        {
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_GetRefTypeInfo);
            int hRefInt = (int)hRef;

            if (hRefInt >= s_HREF_IMPLTYPES_OFFSET && hRefInt <= s_HREF_IMPLTYPES_OFFSET + s_HREF_RANGE)
            {
                ppTI = _implementedTypes[hRefInt - s_HREF_IMPLTYPES_OFFSET];
            }
            else if (hRefInt >= s_HREF_VARS_OFFSET && hRefInt <= s_HREF_VARS_OFFSET + s_HREF_RANGE)
            {
                ppTI = _definedVariables[hRefInt - s_HREF_VARS_OFFSET].GetFinalTypeInfo();
            }
            else if (hRefInt >= s_HREF_FUNCSRET_OFFSET && hRefInt <= s_HREF_FUNCSRET_OFFSET + s_HREF_RANGE)
            {
                ppTI = _definedFunctions[hRefInt - s_HREF_FUNCSRET_OFFSET].returnType.GetFinalTypeInfo();
            }
            else if (hRefInt >= s_HREF_FUNCSPARAM_OFFSET && hRefInt <= s_HREF_FUNCSPARAM_OFFSET + s_HREF_RANGE)
            {
                ppTI = _definedFunctions[(hRefInt - s_HREF_FUNCSPARAM_OFFSET) / s_HREF_FUNCSPARAM_OFFSET_PERFUNC].parameters[(hRefInt - s_HREF_FUNCSPARAM_OFFSET) % s_HREF_FUNCSPARAM_OFFSET_PERFUNC].GetFinalTypeInfo();
            }
            else
            {
                ppTI = null;
                Assert.True(false, "unexpected hRef value");
            }
        }

        #endregion 

        #region Implemented ITypeInfo members

        public void GetContainingTypeLib(out ITypeLib ppTLB, out int pIndex)
        {
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_GetContainingTypeLib);

            ppTLB = _containingTypeLib;
            pIndex = _indexInContainingTypeLib;
        }

        public void GetTypeAttr(out IntPtr ppTypeAttr)
        {
            // Fail BEFORE allocating the handle to avoid leaks. If the real COM object fails in this method
            // and doesn't return the handle or clean it up itself there's not much we can do to avoid the leak.
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_GetTypeAttr);

            ppTypeAttr = _memoryHelper.AllocateHandle(Marshal.SizeOf<TYPEATTR>());
            Marshal.StructureToPtr(_typeAttributes, ppTypeAttr, false);
        }

        public void ReleaseTypeAttr(IntPtr pTypeAttr)
        {
            _memoryHelper.FreeHandle(pTypeAttr);

            // Fail AFTER releasing the handle to avoid leaks. If the real COM object fails in this method
            // there's really nothing we can do to avoid leaking stuff
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_ReleaseTypeAttr);
        }

        public void GetRefTypeOfImplType(int index, out int href)
        {
            Assert.True(index >= 0 && index < _typeAttributes.cImplTypes);

            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_GetRefTypeOfImplType);

            href = index + s_HREF_IMPLTYPES_OFFSET;
        }

        public void GetRefTypeInfo(int hRef, out ITypeInfo ppTI)
        {
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_GetRefTypeInfo);

            if (hRef >= s_HREF_IMPLTYPES_OFFSET && hRef <= s_HREF_IMPLTYPES_OFFSET + s_HREF_RANGE)
            {
                ppTI = _implementedTypes[hRef - s_HREF_IMPLTYPES_OFFSET];
            }
            else if (hRef >= s_HREF_VARS_OFFSET && hRef <= s_HREF_VARS_OFFSET + s_HREF_RANGE)
            {
                ppTI = _definedVariables[hRef - s_HREF_VARS_OFFSET].GetFinalTypeInfo();
            }
            else if (hRef >= s_HREF_FUNCSRET_OFFSET && hRef <= s_HREF_FUNCSRET_OFFSET + s_HREF_RANGE)
            {
                ppTI = _definedFunctions[hRef - s_HREF_FUNCSRET_OFFSET].returnType.GetFinalTypeInfo();
            }
            else if (hRef >= s_HREF_FUNCSPARAM_OFFSET && hRef <= s_HREF_FUNCSPARAM_OFFSET + s_HREF_RANGE)
            {
                ppTI = _definedFunctions[(hRef - s_HREF_FUNCSPARAM_OFFSET) / s_HREF_FUNCSPARAM_OFFSET_PERFUNC].parameters[(hRef - s_HREF_FUNCSPARAM_OFFSET) % s_HREF_FUNCSPARAM_OFFSET_PERFUNC].GetFinalTypeInfo();
            }
            else
            {
                ppTI = null;
                Assert.True(false, "unexpected hRef value");
            }
        }

        public void GetVarDesc(int index, out IntPtr ppVarDesc)
        {
            // Fail BEFORE allocating the handle to avoid leaks. If the real COM object fails in this method
            // and doesn't return the handle or clean it up itself there's not much we can do to avoid the leak.
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_GetVarDesc);

            ppVarDesc = _memoryHelper.AllocateHandle(Marshal.SizeOf<VARDESC>());

            _memoryHelper.EnterSubAllocationScope(ppVarDesc);
            VARDESC varDesc = new VARDESC();
            varDesc.elemdescVar.tdesc = _definedVariables[index].CreateTypeDesc(new IntPtr(index + s_HREF_VARS_OFFSET), _memoryHelper);
            _memoryHelper.ExitSubAllocationScope();

            Marshal.StructureToPtr(varDesc, ppVarDesc, false);
        }

        public void ReleaseVarDesc(IntPtr pVarDesc)
        {
            _memoryHelper.FreeHandle(pVarDesc);

            // Fail AFTER releasing the handle to avoid leaks. If the real COM object fails in this method
            // there's really nothing we can do to avoid leaking stuff
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_ReleaseVarDesc);
        }

        public void GetFuncDesc(int index, out IntPtr ppFuncDesc)
        {
            // Fail BEFORE allocating the handle to avoid leaks. If the real COM object fails in this method
            // and doesn't return the handle or clean it up itself there's not much we can do to avoid the leak.
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_GetFuncDesc);

            ppFuncDesc = _memoryHelper.AllocateHandle(Marshal.SizeOf<FUNCDESC>());

            _memoryHelper.EnterSubAllocationScope(ppFuncDesc);
            FUNCDESC funcDesc = new FUNCDESC();

            funcDesc.lprgelemdescParam = _memoryHelper.AllocateHandle(_definedFunctions[index].parameters.Length * Marshal.SizeOf<ELEMDESC>());
            funcDesc.cParams = (short)_definedFunctions[index].parameters.Length;

            for (int i = 0; i < _definedFunctions[index].parameters.Length; i++)
            {
                ELEMDESC elemDesc = new ELEMDESC();
                elemDesc.tdesc = _definedFunctions[index].parameters[i].CreateTypeDesc(
                    new IntPtr((index * s_HREF_FUNCSPARAM_OFFSET_PERFUNC) + i + s_HREF_FUNCSPARAM_OFFSET), _memoryHelper);

                Marshal.StructureToPtr(
                    elemDesc,
                    new IntPtr(funcDesc.lprgelemdescParam.ToInt64() + (i * Marshal.SizeOf<ELEMDESC>())),
                    false);
            }

            funcDesc.elemdescFunc.tdesc = _definedFunctions[index].returnType.CreateTypeDesc(
                new IntPtr(index + s_HREF_FUNCSRET_OFFSET), _memoryHelper);
            _memoryHelper.ExitSubAllocationScope();

            Marshal.StructureToPtr(funcDesc, ppFuncDesc, false);
        }

        public void ReleaseFuncDesc(IntPtr pFuncDesc)
        {
            _memoryHelper.FreeHandle(pFuncDesc);

            // Fail AFTER releasing the handle to avoid leaks. If the real COM object fails in this method
            // there's really nothing we can do to avoid leaking stuff
            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_ReleaseFuncDesc);
        }

        public void GetDocumentation(int index, out string strName, out string strDocString, out int dwHelpContext, out string strHelpFile)
        {
            Assert.Equal(-1, index);

            _faultInjector.FailurePointThrow(MockTypeLibrariesFailurePoints.ITypeInfo_GetDocumentation);

            strName = _typeName;
            strDocString = "garbage";
            dwHelpContext = -1;
            strHelpFile = "garbage^2";
        }

        #endregion

        #region ICreateTypeDesc Members

        public TYPEDESC CreateTypeDesc(IntPtr finalTypeHRef, MockUnmanagedMemoryHelper memoryHelper)
        {
            TYPEDESC typeDesc;
            typeDesc.vt = (short)VarEnum.VT_USERDEFINED;
            typeDesc.lpValue = finalTypeHRef;
            return typeDesc;
        }

        public MockTypeInfo GetFinalTypeInfo()
        {
            return this;
        }

        #endregion

        #region Stubbed ITypeInfo members

        public void AddressOfMember(int memid, INVOKEKIND invKind, out IntPtr ppv)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void CreateInstance(object pUnkOuter, ref Guid riid, out object ppvObj)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetDllEntry(int memid, INVOKEKIND invKind, IntPtr pBstrDllName, IntPtr pBstrName, IntPtr pwOrdinal)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetIDsOfNames(string[] rgszNames, int cNames, int[] pMemId)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetImplTypeFlags(int index, out IMPLTYPEFLAGS pImplTypeFlags)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetMops(int memid, out string pBstrMops)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetNames(int memid, string[] rgBstrNames, int cMaxNames, out int pcNames)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void GetTypeComp(out ITypeComp ppTComp)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void Invoke(object pvInstance, int memid, short wFlags, ref DISPPARAMS pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, out int puArgErr)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
