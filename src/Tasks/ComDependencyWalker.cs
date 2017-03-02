// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices.ComTypes;
using System.Globalization;


using Microsoft.Build.Shared;

using Marshal = System.Runtime.InteropServices.Marshal;
using COMException = System.Runtime.InteropServices.COMException;
using VarEnum = System.Runtime.InteropServices.VarEnum;

namespace Microsoft.Build.Tasks
{
    // Abstract the method for releasing COM objects for unit testing. 
    // Our mocks are not actually COM objects and they would blow up if passed to the real Marshal.ReleaseComObject.
    internal delegate int MarshalReleaseComObject(object o);

    /// <summary>
    /// COM type library dependency walker class
    /// </summary>
    internal class ComDependencyWalker
    {
        // Dependencies of all analyzed typelibs. Can be cleared to allow for analyzing typelibs one by one while
        // still skipping already seen types
        private HashSet<TYPELIBATTR> _dependencies;

        // History of already seen types.
        private HashSet<AnalyzedTypesInfoKey> _analyzedTypes;

        private sealed class TYPELIBATTRComparer : IEqualityComparer<TYPELIBATTR>
        {
            public static readonly IEqualityComparer<TYPELIBATTR> Instance = new TYPELIBATTRComparer();

            public bool Equals(TYPELIBATTR a, TYPELIBATTR b)
            {
                return a.guid == b.guid &&
                       a.lcid == b.lcid &&
                       a.syskind == b.syskind &&
                       a.wLibFlags == b.wLibFlags &&
                       a.wMajorVerNum == b.wMajorVerNum &&
                       a.wMinorVerNum == b.wMinorVerNum;
            }

            public int GetHashCode(TYPELIBATTR x)
            {
                return unchecked(x.guid.GetHashCode() + x.lcid + (int)x.syskind + (int)x.wLibFlags + (x.wMajorVerNum << 16) + x.wMinorVerNum);
            }
        }

        private struct AnalyzedTypesInfoKey
        {
            public readonly Guid guid;
            public readonly short wMajorVerNum;
            public readonly short wMinorVerNum;
            public readonly int lcid;
            public readonly int index;

            public AnalyzedTypesInfoKey(Guid guid, short major, short minor, int lcid, int index)
            {
                this.guid = guid;
                this.wMajorVerNum = major;
                this.wMinorVerNum = minor;
                this.lcid = lcid;
                this.index = index;
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}:{4}",
                    this.guid, this.wMajorVerNum,
                    this.wMinorVerNum, this.lcid, this.index);
            }
        }

        private sealed class AnalyzedTypesInfoKeyComparer : IEqualityComparer<AnalyzedTypesInfoKey>
        {
            public static readonly IEqualityComparer<AnalyzedTypesInfoKey> Instance = new AnalyzedTypesInfoKeyComparer();

            public bool Equals(AnalyzedTypesInfoKey a, AnalyzedTypesInfoKey b)
            {
                return a.guid == b.guid &&
                       a.wMajorVerNum == b.wMajorVerNum &&
                       a.wMinorVerNum == b.wMinorVerNum &&
                       a.lcid == b.lcid &&
                       a.index == b.index;
            }

            public int GetHashCode(AnalyzedTypesInfoKey x)
            {
                return unchecked(x.guid.GetHashCode() + (x.wMajorVerNum << 16) + x.wMinorVerNum + x.lcid + x.index);
            }
        }

        private MarshalReleaseComObject _marshalReleaseComObject;

        private List<Exception> _encounteredProblems;

        /// <summary>
        /// List of exceptions thrown by the components during scanning
        /// </summary>
        internal List<Exception> EncounteredProblems
        {
            get
            {
                return _encounteredProblems;
            }
        }

        /// <summary>
        /// Internal constructor
        /// </summary>
        internal ComDependencyWalker(MarshalReleaseComObject marshalReleaseComObject)
        {
            _dependencies = new HashSet<TYPELIBATTR>(TYPELIBATTRComparer.Instance);
            _analyzedTypes = new HashSet<AnalyzedTypesInfoKey>(AnalyzedTypesInfoKeyComparer.Instance);
            _encounteredProblems = new List<Exception>();

            _marshalReleaseComObject = marshalReleaseComObject;
        }

        /// <summary>
        /// The main entry point to the dependency walker
        /// </summary>
        /// <param name="typeLibrary">type library to be analyzed</param>
        internal void AnalyzeTypeLibrary(ITypeLib typeLibrary)
        {
            try
            {
                int typeInfoCount = typeLibrary.GetTypeInfoCount();

                for (int i = 0; i < typeInfoCount; i++)
                {
                    ITypeInfo typeInfo = null;

                    try
                    {
                        typeLibrary.GetTypeInfo(i, out typeInfo);
                        AnalyzeTypeInfo(typeInfo);
                    }
                    finally
                    {
                        if (typeInfo != null)
                        {
                            _marshalReleaseComObject(typeInfo);
                        }
                    }
                }
            }
            // This is the only catch block in this class, meaning that once a type library throws it's game over for it.
            // I've tried using a finer grained approach but experiments with COM objects on my machine have shown that if
            // a type library is broken, it's broken in several places (e.g. dependencies on a type lib that's not
            // registered properly). Trying to recover from errors and continue with scanning dependencies only meant
            // that we got lots of exceptions thrown which was not only not very useful for the end user, but also horribly slow.
            catch (COMException ex)
            {
                _encounteredProblems.Add(ex);
            }
        }

        /// <summary>
        /// Analyze the given type looking for dependencies on other type libraries
        /// </summary>
        /// <param name="typeInfo"></param>
        private void AnalyzeTypeInfo(ITypeInfo typeInfo)
        {
            ITypeLib containingTypeLib = null;
            int indexInContainingTypeLib;

            try
            {
                typeInfo.GetContainingTypeLib(out containingTypeLib, out indexInContainingTypeLib);

                TYPELIBATTR containingTypeLibAttributes;
                ComReference.GetTypeLibAttrForTypeLib(ref containingTypeLib, out containingTypeLibAttributes);

                // Have we analyzed this type info already? If so skip it.
                AnalyzedTypesInfoKey typeInfoId = new AnalyzedTypesInfoKey(
                    containingTypeLibAttributes.guid, containingTypeLibAttributes.wMajorVerNum,
                    containingTypeLibAttributes.wMinorVerNum, containingTypeLibAttributes.lcid, indexInContainingTypeLib);

                // Get enough information about the type to figure out if we want to register it as a dependency
                TYPEATTR typeAttributes;

                ComReference.GetTypeAttrForTypeInfo(typeInfo, out typeAttributes);

                // Is it one of the types we don't care about?
                if (!CanSkipType(typeInfo, containingTypeLib, typeAttributes, containingTypeLibAttributes))
                {
                    _dependencies.Add(containingTypeLibAttributes);

                    if (_analyzedTypes.Add(typeInfoId))
                    {
                        // We haven't already analyzed this type, so rescan
                        ScanImplementedTypes(typeInfo, typeAttributes);
                        ScanDefinedVariables(typeInfo, typeAttributes);
                        ScanDefinedFunctions(typeInfo, typeAttributes);
                    }
                }
                // Make sure if we encounter this type again, we won't rescan it, since we already know we can skip it
                else
                {
                    _analyzedTypes.Add(typeInfoId);
                }
            }
            finally
            {
                if (containingTypeLib != null)
                {
                    _marshalReleaseComObject(containingTypeLib);
                }
            }
        }

        /// <summary>
        /// Returns true if we don't need to analyze this particular type.
        /// </summary>
        /// <param name="typeInfo"></param>
        /// <param name="typeLib"></param>
        /// <param name="typeAttributes"></param>
        /// <param name="typeLibAttributes"></param>
        /// <returns></returns>
        private bool CanSkipType(ITypeInfo typeInfo, ITypeLib typeLib, TYPEATTR typeAttributes, TYPELIBATTR typeLibAttributes)
        {
            // Well known OLE type?
            if ((typeAttributes.guid == NativeMethods.IID_IUnknown) ||
                (typeAttributes.guid == NativeMethods.IID_IDispatch) ||
                (typeAttributes.guid == NativeMethods.IID_IDispatchEx) ||
                (typeAttributes.guid == NativeMethods.IID_IEnumVariant) ||
                (typeAttributes.guid == NativeMethods.IID_ITypeInfo))
            {
                return true;
            }

            // Is this the Guid type? If so we should be using the corresponding .NET type. 
            if (typeLibAttributes.guid == NativeMethods.IID_StdOle)
            {
                string typeName, ignoredDocString, ignoredHelpFile;
                int ignoredHelpContext;

                typeInfo.GetDocumentation(-1, out typeName, out ignoredDocString, out ignoredHelpContext, out ignoredHelpFile);

                if (string.CompareOrdinal(typeName, "GUID") == 0)
                {
                    return true;
                }
            }

            // Skip types exported from .NET assemblies
            ITypeLib2 typeLib2 = typeLib as ITypeLib2;

            if (typeLib2 != null)
            {
                object exportedFromComPlusObj;
                typeLib2.GetCustData(ref NativeMethods.GUID_ExportedFromComPlus, out exportedFromComPlusObj);

                string exportedFromComPlus = exportedFromComPlusObj as string;

                if (!string.IsNullOrEmpty(exportedFromComPlus))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// For a given type, analyze recursively all the types implemented by it.
        /// </summary>
        /// <param name="typeInfo"></param>
        /// <param name="typeAttributes"></param>
        private void ScanImplementedTypes(ITypeInfo typeInfo, TYPEATTR typeAttributes)
        {
            for (int implTypeIndex = 0; implTypeIndex < typeAttributes.cImplTypes; implTypeIndex++)
            {
                IFixedTypeInfo implementedType = null;

                try
                {
                    IntPtr hRef;
                    IFixedTypeInfo fixedTypeInfo = (IFixedTypeInfo)typeInfo;
                    fixedTypeInfo.GetRefTypeOfImplType(implTypeIndex, out hRef);
                    fixedTypeInfo.GetRefTypeInfo(hRef, out implementedType);

                    AnalyzeTypeInfo((ITypeInfo)implementedType);
                }
                finally
                {
                    if (implementedType != null)
                    {
                        _marshalReleaseComObject(implementedType);
                    }
                }
            }
        }

        /// <summary>
        /// For a given type, analyze all the variables defined by it
        /// </summary>
        /// <param name="typeInfo"></param>
        /// <param name="typeAttributes"></param>
        private void ScanDefinedVariables(ITypeInfo typeInfo, TYPEATTR typeAttributes)
        {
            for (int definedVarIndex = 0; definedVarIndex < typeAttributes.cVars; definedVarIndex++)
            {
                IntPtr varDescHandleToRelease = IntPtr.Zero;

                try
                {
                    VARDESC varDesc;
                    ComReference.GetVarDescForVarIndex(typeInfo, definedVarIndex, out varDesc, out varDescHandleToRelease);
                    AnalyzeElement(typeInfo, varDesc.elemdescVar);
                }
                finally
                {
                    if (varDescHandleToRelease != IntPtr.Zero)
                    {
                        typeInfo.ReleaseVarDesc(varDescHandleToRelease);
                    }
                }
            }
        }

        /// <summary>
        /// For a given type, analyze all the functions implemented by it. That means all the argument and return types.
        /// </summary>
        /// <param name="typeInfo"></param>
        /// <param name="typeAttributes"></param>
        private void ScanDefinedFunctions(ITypeInfo typeInfo, TYPEATTR typeAttributes)
        {
            for (int definedFuncIndex = 0; definedFuncIndex < typeAttributes.cFuncs; definedFuncIndex++)
            {
                IntPtr funcDescHandleToRelease = IntPtr.Zero;

                try
                {
                    FUNCDESC funcDesc;
                    ComReference.GetFuncDescForDescIndex(typeInfo, definedFuncIndex, out funcDesc, out funcDescHandleToRelease);

                    int offset = 0;

                    // Analyze the argument types
                    for (int paramIndex = 0; paramIndex < funcDesc.cParams; paramIndex++)
                    {
                        ELEMDESC elemDesc = (ELEMDESC)Marshal.PtrToStructure(
                            new IntPtr(funcDesc.lprgelemdescParam.ToInt64() + offset), typeof(ELEMDESC));

                        AnalyzeElement(typeInfo, elemDesc);

                        offset += Marshal.SizeOf<ELEMDESC>();
                    }

                    // Analyze the return value type
                    AnalyzeElement(typeInfo, funcDesc.elemdescFunc);
                }
                finally
                {
                    if (funcDescHandleToRelease != IntPtr.Zero)
                    {
                        typeInfo.ReleaseFuncDesc(funcDescHandleToRelease);
                    }
                }
            }
        }

        /// <summary>
        /// Analyze the given element (i.e. composite type of an argument) recursively
        /// </summary>
        /// <param name="elementDesc"></param>
        /// <param name="typeInfo"></param>
        private void AnalyzeElement(ITypeInfo typeInfo, ELEMDESC elementDesc)
        {
            TYPEDESC typeDesc = elementDesc.tdesc;

            // If the current type is a pointer or an array, determine the child type and analyze that.
            while (((VarEnum)typeDesc.vt == VarEnum.VT_PTR) || ((VarEnum)typeDesc.vt == VarEnum.VT_SAFEARRAY))
            {
                TYPEDESC childTypeDesc = (TYPEDESC)Marshal.PtrToStructure(typeDesc.lpValue, typeof(TYPEDESC));
                typeDesc = childTypeDesc;
            }

            // We're only interested in user defined types for recursive analysis
            if ((VarEnum)typeDesc.vt == VarEnum.VT_USERDEFINED)
            {
                IntPtr hrefType = typeDesc.lpValue;
                IFixedTypeInfo childTypeInfo = null;

                try
                {
                    IFixedTypeInfo fixedTypeInfo = (IFixedTypeInfo)typeInfo;
                    fixedTypeInfo.GetRefTypeInfo(hrefType, out childTypeInfo);

                    AnalyzeTypeInfo((ITypeInfo)childTypeInfo);
                }
                finally
                {
                    if (childTypeInfo != null)
                    {
                        _marshalReleaseComObject(childTypeInfo);
                    }
                }
            }
        }

        /// <summary>
        /// Get all the dependencies of the processed libraries
        /// </summary>
        /// <returns></returns>
        internal TYPELIBATTR[] GetDependencies()
        {
            TYPELIBATTR[] returnArray = new TYPELIBATTR[_dependencies.Count];
            _dependencies.CopyTo(returnArray);
            return returnArray;
        }

        /// <summary>
        /// FOR UNIT-TESTING ONLY
        /// Returns a list of the analyzed type names
        /// </summary>
        internal ICollection<string> GetAnalyzedTypeNames()
        {
            string[] names = new string[_analyzedTypes.Count];
            int i = 0;
            foreach (AnalyzedTypesInfoKey analyzedType in _analyzedTypes)
            {
                names[i++] = analyzedType.ToString();
            }

            return names;
        }

        /// <summary>
        /// Clear the dependency list so we can read dependencies incrementally but still have the advantage of 
        /// not scanning previously seen types
        /// </summary>
        internal void ClearDependencyList()
        {
            _dependencies.Clear();
        }

        /// <summary>
        /// Clear the analyzed type cache.  This is necessary if we have to resolve dependencies that are also 
        /// COM references in the project, or we may get an inaccurate view of what their dependencies are. 
        /// </summary>
        internal void ClearAnalyzedTypeCache()
        {
            _analyzedTypes.Clear();
        }
    }
}
