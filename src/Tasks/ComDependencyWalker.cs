// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Globalization;

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
        private readonly HashSet<TYPELIBATTR> _dependencies;

        // History of already seen types.
        private readonly HashSet<AnalyzedTypesInfoKey> _analyzedTypes;

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

        private readonly MarshalReleaseComObject _marshalReleaseComObject;

        /// <summary>
        /// List of exceptions thrown by the components during scanning
        /// </summary>
        internal List<Exception> EncounteredProblems { get; }

        /// <summary>
        /// Internal constructor
        /// </summary>
        internal ComDependencyWalker(MarshalReleaseComObject marshalReleaseComObject)
        {
            _dependencies = new HashSet<TYPELIBATTR>(TYPELIBATTRComparer.Instance);
            _analyzedTypes = new HashSet<AnalyzedTypesInfoKey>(AnalyzedTypesInfoKeyComparer.Instance);
            EncounteredProblems = new List<Exception>();

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
                EncounteredProblems.Add(ex);
            }
        }

        /// <summary>
        /// Analyze the given type looking for dependencies on other type libraries
        /// </summary>
        /// <param name="typeInfo"></param>
        private void AnalyzeTypeInfo(ITypeInfo typeInfo)
        {
            ITypeLib containingTypeLib = null;

            try
            {
                typeInfo.GetContainingTypeLib(out containingTypeLib, out int indexInContainingTypeLib);

                ComReference.GetTypeLibAttrForTypeLib(ref containingTypeLib, out TYPELIBATTR containingTypeLibAttributes);

                // Have we analyzed this type info already? If so skip it.
                var typeInfoId = new AnalyzedTypesInfoKey(
                    containingTypeLibAttributes.guid, containingTypeLibAttributes.wMajorVerNum,
                    containingTypeLibAttributes.wMinorVerNum, containingTypeLibAttributes.lcid, indexInContainingTypeLib);

                // Get enough information about the type to figure out if we want to register it as a dependency

                ComReference.GetTypeAttrForTypeInfo(typeInfo, out TYPEATTR typeAttributes);

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
        private static bool CanSkipType(ITypeInfo typeInfo, ITypeLib typeLib, TYPEATTR typeAttributes, TYPELIBATTR typeLibAttributes)
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
                typeInfo.GetDocumentation(-1, out string typeName, out _, out _, out _);

                if (string.CompareOrdinal(typeName, "GUID") == 0)
                {
                    return true;
                }
            }

            // Skip types exported from .NET assemblies
            if (typeLib is ITypeLib2 typeLib2)
            {
                typeLib2.GetCustData(ref NativeMethods.GUID_ExportedFromComPlus, out object exportedFromComPlusObj);

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
        private void ScanImplementedTypes(ITypeInfo typeInfo, TYPEATTR typeAttributes)
        {
            for (int implTypeIndex = 0; implTypeIndex < typeAttributes.cImplTypes; implTypeIndex++)
            {
                IFixedTypeInfo implementedType = null;

                try
                {
                    var fixedTypeInfo = (IFixedTypeInfo)typeInfo;
                    fixedTypeInfo.GetRefTypeOfImplType(implTypeIndex, out IntPtr hRef);
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
        private void ScanDefinedVariables(ITypeInfo typeInfo, TYPEATTR typeAttributes)
        {
            for (int definedVarIndex = 0; definedVarIndex < typeAttributes.cVars; definedVarIndex++)
            {
                IntPtr varDescHandleToRelease = IntPtr.Zero;

                try
                {
                    ComReference.GetVarDescForVarIndex(typeInfo, definedVarIndex, out VARDESC varDesc, out varDescHandleToRelease);
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
        private void ScanDefinedFunctions(ITypeInfo typeInfo, TYPEATTR typeAttributes)
        {
            for (int definedFuncIndex = 0; definedFuncIndex < typeAttributes.cFuncs; definedFuncIndex++)
            {
                IntPtr funcDescHandleToRelease = IntPtr.Zero;

                try
                {
                    ComReference.GetFuncDescForDescIndex(typeInfo, definedFuncIndex, out FUNCDESC funcDesc, out funcDescHandleToRelease);

                    int offset = 0;

                    // Analyze the argument types
                    for (int paramIndex = 0; paramIndex < funcDesc.cParams; paramIndex++)
                    {
                        var elemDesc = (ELEMDESC)Marshal.PtrToStructure(
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
        private void AnalyzeElement(ITypeInfo typeInfo, ELEMDESC elementDesc)
        {
            TYPEDESC typeDesc = elementDesc.tdesc;

            // If the current type is a pointer or an array, determine the child type and analyze that.
            while (((VarEnum)typeDesc.vt == VarEnum.VT_PTR) || ((VarEnum)typeDesc.vt == VarEnum.VT_SAFEARRAY))
            {
                var childTypeDesc = (TYPEDESC)Marshal.PtrToStructure(typeDesc.lpValue, typeof(TYPEDESC));
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
            var returnArray = new TYPELIBATTR[_dependencies.Count];
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
