// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN

using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Ole;
using Windows.Win32.System.Variant;
using COMException = System.Runtime.InteropServices.COMException;
using Marshal = System.Runtime.InteropServices.Marshal;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// COM type library dependency walker class
    /// </summary>
    internal class ComDependencyWalker
    {
        // Dependencies of all analyzed typelibs. Can be cleared to allow for analyzing typelibs one by one while
        // still skipping already seen types
        private readonly HashSet<TLIBATTR> _dependencies;

        // History of already seen types.
        private readonly HashSet<AnalyzedTypesInfoKey> _analyzedTypes;

        private sealed class TLIBATTRComparer : IEqualityComparer<TLIBATTR>
        {
            public static readonly IEqualityComparer<TLIBATTR> Instance = new TLIBATTRComparer();

            public bool Equals(TLIBATTR a, TLIBATTR b)
            {
                return a.guid == b.guid &&
                       a.lcid == b.lcid &&
                       a.syskind == b.syskind &&
                       a.wLibFlags == b.wLibFlags &&
                       a.wMajorVerNum == b.wMajorVerNum &&
                       a.wMinorVerNum == b.wMinorVerNum;
            }

            public int GetHashCode(TLIBATTR x)
            {
                return unchecked(x.guid.GetHashCode() + (int)x.lcid + (int)x.syskind + (int)x.wLibFlags + (x.wMajorVerNum << 16) + x.wMinorVerNum);
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

            public override readonly string ToString()
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

        /// <summary>
        /// List of exceptions thrown by the components during scanning
        /// </summary>
        internal List<Exception> EncounteredProblems { get; }

        /// <summary>
        /// Internal constructor
        /// </summary>
        internal ComDependencyWalker()
        {
            _dependencies = new HashSet<TLIBATTR>(TLIBATTRComparer.Instance);
            _analyzedTypes = new HashSet<AnalyzedTypesInfoKey>(AnalyzedTypesInfoKeyComparer.Instance);
            EncounteredProblems = new List<Exception>();
        }

        /// <summary>
        /// The main entry point to the dependency walker
        /// </summary>
        /// <param name="typeLibrary">type library to be analyzed</param>
        internal unsafe void AnalyzeTypeLibrary(ITypeLib* typeLibrary)
        {
            try
            {
                uint typeInfoCount = typeLibrary->GetTypeInfoCount();

                for (uint i = 0; i < typeInfoCount; i++)
                {
                    using ComScope<ITypeInfo> typeInfo = new(null);
                    typeLibrary->GetTypeInfo(i, typeInfo).ThrowOnFailure();
                    AnalyzeTypeInfo(typeInfo.Pointer);
                }
            }
            // This is the only catch block in this class, meaning that once a type library throws it's game over for it.
            // I've tried using a finer grained approach but experiments with COM objects on my machine have shown that if
            // a type library is broken, it's broken in several places (e.g. dependencies on a type lib that's not
            // registered properly). Trying to recover from errors and continue with scanning dependencies only meant
            // that we got lots of exceptions thrown which was not only not very useful for the end user, but also horribly slow.
            catch (COMException ex)
            {
                EncounteredProblems.Add(ex);
            }
        }

        /// <summary>
        /// Analyze the given type looking for dependencies on other type libraries
        /// </summary>
        /// <param name="typeInfo"></param>
        private unsafe void AnalyzeTypeInfo(ITypeInfo* typeInfo)
        {
            using ComScope<ITypeLib> containingTypeLib = new(null);
            uint indexInContainingTypeLib;
            typeInfo->GetContainingTypeLib(containingTypeLib, &indexInContainingTypeLib).ThrowOnFailure();

            ComReference.GetTypeLibAttrForTypeLib(containingTypeLib.Pointer, out TLIBATTR containingTypeLibAttributes);

            // Have we analyzed this type info already? If so skip it.
            var typeInfoId = new AnalyzedTypesInfoKey(
                containingTypeLibAttributes.guid, (short)containingTypeLibAttributes.wMajorVerNum,
                (short)containingTypeLibAttributes.wMinorVerNum, (int)containingTypeLibAttributes.lcid, (int)indexInContainingTypeLib);

            // Get enough information about the type to figure out if we want to register it as a dependency
            ComReference.GetTypeAttrForTypeInfo(typeInfo, out TYPEATTR typeAttributes);

            // Is it one of the types we don't care about?
            if (!CanSkipType(typeInfo, containingTypeLib.Pointer, typeAttributes, containingTypeLibAttributes))
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

        /// <summary>
        /// Returns true if we don't need to analyze this particular type.
        /// </summary>
        private static unsafe bool CanSkipType(ITypeInfo* typeInfo, ITypeLib* typeLib, TYPEATTR typeAttributes, TLIBATTR typeLibAttributes)
        {
            // Well known OLE type? Compare against the IIDs CsWin32 emits on each generated interface struct.
            if ((typeAttributes.guid == IUnknown.IID_Guid) ||
                (typeAttributes.guid == IDispatch.IID_Guid) ||
                (typeAttributes.guid == IDispatchEx.IID_Guid) ||
                (typeAttributes.guid == IEnumVARIANT.IID_Guid) ||
                (typeAttributes.guid == ITypeInfo.IID_Guid))
            {
                return true;
            }

            // Is this the Guid type? If so we should be using the corresponding .NET type.
            if (typeLibAttributes.guid == NativeMethods.LIBID_StdOle)
            {
                // All out-params must be supplied: the runtime-callable-wrapper marshaller writes every
                // [out] BSTR even though only the name is needed here.
                using BSTR typeName = default;
                using BSTR docString = default;
                using BSTR helpFile = default;
                uint helpContext;
                typeInfo->GetDocumentation(-1, &typeName, &docString, &helpContext, &helpFile).ThrowOnFailure();

                if (string.CompareOrdinal(typeName.ToString(), "GUID") == 0)
                {
                    return true;
                }
            }

            // Skip types exported from .NET assemblies
            using ComScope<ITypeLib2> typeLib2 = new(null);
            Guid typeLib2Iid = ITypeLib2.IID_Guid;
            if (typeLib->QueryInterface(&typeLib2Iid, typeLib2).Succeeded)
            {
                Guid exportedFromComPlusGuid = NativeMethods.GUID_ExportedFromComPlus;
                VARIANT custData = default;
                if (typeLib2.Pointer->GetCustData(&exportedFromComPlusGuid, &custData).Succeeded)
                {
                    object exportedFromComPlusObj;
                    try
                    {
                        exportedFromComPlusObj = Marshal.GetObjectForNativeVariant((IntPtr)(&custData));
                    }
                    finally
                    {
                        PInvoke.VariantClear(&custData);
                    }

                    if (exportedFromComPlusObj is string exportedFromComPlus && !string.IsNullOrEmpty(exportedFromComPlus))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// For a given type, analyze recursively all the types implemented by it.
        /// </summary>
        private unsafe void ScanImplementedTypes(ITypeInfo* typeInfo, TYPEATTR typeAttributes)
        {
            for (int implTypeIndex = 0; implTypeIndex < typeAttributes.cImplTypes; implTypeIndex++)
            {
                uint hRef;
                typeInfo->GetRefTypeOfImplType((uint)implTypeIndex, &hRef).ThrowOnFailure();

                using ComScope<ITypeInfo> implementedType = new(null);
                typeInfo->GetRefTypeInfo(hRef, implementedType).ThrowOnFailure();

                AnalyzeTypeInfo(implementedType.Pointer);
            }
        }

        /// <summary>
        /// For a given type, analyze all the variables defined by it
        /// </summary>
        private unsafe void ScanDefinedVariables(ITypeInfo* typeInfo, TYPEATTR typeAttributes)
        {
            for (int definedVarIndex = 0; definedVarIndex < typeAttributes.cVars; definedVarIndex++)
            {
                VARDESC* varDesc = null;

                try
                {
                    ComReference.GetVarDescForVarIndex(typeInfo, definedVarIndex, out varDesc);
                    AnalyzeElement(typeInfo, varDesc->elemdescVar);
                }
                finally
                {
                    if (varDesc != null)
                    {
                        typeInfo->ReleaseVarDesc(varDesc);
                    }
                }
            }
        }

        /// <summary>
        /// For a given type, analyze all the functions implemented by it. That means all the argument and return types.
        /// </summary>
        private unsafe void ScanDefinedFunctions(ITypeInfo* typeInfo, TYPEATTR typeAttributes)
        {
            for (int definedFuncIndex = 0; definedFuncIndex < typeAttributes.cFuncs; definedFuncIndex++)
            {
                FUNCDESC* funcDesc = null;

                try
                {
                    ComReference.GetFuncDescForDescIndex(typeInfo, definedFuncIndex, out funcDesc);

                    // Analyze the argument types
                    for (int paramIndex = 0; paramIndex < funcDesc->cParams; paramIndex++)
                    {
                        AnalyzeElement(typeInfo, funcDesc->lprgelemdescParam[paramIndex]);
                    }

                    // Analyze the return value type
                    AnalyzeElement(typeInfo, funcDesc->elemdescFunc);
                }
                finally
                {
                    if (funcDesc != null)
                    {
                        typeInfo->ReleaseFuncDesc(funcDesc);
                    }
                }
            }
        }

        /// <summary>
        /// Analyze the given element (i.e. composite type of an argument) recursively
        /// </summary>
        private unsafe void AnalyzeElement(ITypeInfo* typeInfo, ELEMDESC elementDesc)
        {
            TYPEDESC typeDesc = elementDesc.tdesc;

            // If the current type is a pointer or an array, determine the child type and analyze that.
            while ((typeDesc.vt == VARENUM.VT_PTR) || (typeDesc.vt == VARENUM.VT_SAFEARRAY))
            {
                typeDesc = *typeDesc.Anonymous.lptdesc;
            }

            // We're only interested in user defined types for recursive analysis
            if (typeDesc.vt == VARENUM.VT_USERDEFINED)
            {
                uint hrefType = typeDesc.Anonymous.hreftype;

                using ComScope<ITypeInfo> childTypeInfo = new(null);
                typeInfo->GetRefTypeInfo(hrefType, childTypeInfo).ThrowOnFailure();

                AnalyzeTypeInfo(childTypeInfo.Pointer);
            }
        }

        /// <summary>
        /// Get all the dependencies of the processed libraries
        /// </summary>
        /// <returns></returns>
        internal TLIBATTR[] GetDependencies()
        {
            var returnArray = new TLIBATTR[_dependencies.Count];
            _dependencies.CopyTo(returnArray);
            return returnArray;
        }

        /// <summary>
        /// FOR UNIT-TESTING ONLY
        /// Returns a list of the analyzed type names
        /// </summary>
        internal ICollection<string> GetAnalyzedTypeNames()
        {
            var names = new string[_analyzedTypes.Count];
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

#endif
