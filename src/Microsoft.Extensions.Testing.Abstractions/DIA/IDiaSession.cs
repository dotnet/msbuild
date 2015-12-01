// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace dia2
{
    [Guid("2F609EE1-D1C8-4E24-8288-3326BADCD211"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IDiaSession
    {
        [DispId(1)]
        ulong loadAddress
        {

            get;

            set;
        }
        [DispId(2)]
        IDiaSymbol globalScope
        {

            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }

        void getEnumTables([MarshalAs(UnmanagedType.Interface)] out IDiaEnumTables ppEnumTables);

        void getSymbolsByAddr([MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbolsByAddr ppEnumbyAddr);

        void findChildren([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findChildrenEx([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findChildrenExByAddr([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [In] uint isect, [In] uint offset, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findChildrenExByVA([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [In] ulong va, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findChildrenExByRVA([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [In] uint rva, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findSymbolByAddr([In] uint isect, [In] uint offset, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol);

        void findSymbolByRVA([In] uint rva, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol);

        void findSymbolByVA([In] ulong va, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol);

        void findSymbolByToken([In] uint token, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol);

        void symsAreEquiv([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol symbolA, [MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol symbolB);

        void symbolById([In] uint id, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol);

        void findSymbolByRVAEx([In] uint rva, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol, out int displacement);

        void findSymbolByVAEx([In] ulong va, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol, out int displacement);

        void findFile([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol pCompiland, [MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint compareFlags, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSourceFiles ppResult);

        void findFileById([In] uint uniqueId, [MarshalAs(UnmanagedType.Interface)] out IDiaSourceFile ppResult);

        void findLines([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol compiland, [MarshalAs(UnmanagedType.Interface)] [In] IDiaSourceFile file, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findLinesByAddr([In] uint seg, [In] uint offset, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findLinesByRVA([In] uint rva, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findLinesByVA([In] ulong va, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findLinesByLinenum([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol compiland, [MarshalAs(UnmanagedType.Interface)] [In] IDiaSourceFile file, [In] uint linenum, [In] uint column, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInjectedSource([MarshalAs(UnmanagedType.LPWStr)] [In] string srcFile, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumInjectedSources ppResult);

        void getEnumDebugStreams([MarshalAs(UnmanagedType.Interface)] out IDiaEnumDebugStreams ppEnumDebugStreams);

        void findInlineFramesByAddr([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] uint isect, [In] uint offset, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findInlineFramesByRVA([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] uint rva, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findInlineFramesByVA([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] ulong va, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void findInlineeLines([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInlineeLinesByAddr([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] uint isect, [In] uint offset, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInlineeLinesByRVA([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] uint rva, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInlineeLinesByVA([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol parent, [In] ulong va, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInlineeLinesByLinenum([MarshalAs(UnmanagedType.Interface)] [In] IDiaSymbol compiland, [MarshalAs(UnmanagedType.Interface)] [In] IDiaSourceFile file, [In] uint linenum, [In] uint column, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInlineesByName([MarshalAs(UnmanagedType.LPWStr)] [In] string name, [In] uint option, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void addressForVA([In] ulong va, out uint pISect, out uint pOffset);

        void addressForRVA([In] uint rva, out uint pISect, out uint pOffset);

        void findILOffsetsByAddr([In] uint isect, [In] uint offset, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findILOffsetsByRVA([In] uint rva, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findILOffsetsByVA([In] ulong va, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findInputAssemblyFiles([MarshalAs(UnmanagedType.Interface)] out IDiaEnumInputAssemblyFiles ppResult);

        void findInputAssembly([In] uint index, [MarshalAs(UnmanagedType.Interface)] out IDiaInputAssemblyFile ppResult);

        void findInputAssemblyById([In] uint uniqueId, [MarshalAs(UnmanagedType.Interface)] out IDiaInputAssemblyFile ppResult);

        void getFuncMDTokenMapSize(out uint pcb);

        void getFuncMDTokenMap([In] uint cb, out uint pcb, out byte pb);

        void getTypeMDTokenMapSize(out uint pcb);

        void getTypeMDTokenMap([In] uint cb, out uint pcb, out byte pb);

        void getNumberOfFunctionFragments_VA([In] ulong vaFunc, [In] uint cbFunc, out uint pNumFragments);

        void getNumberOfFunctionFragments_RVA([In] uint rvaFunc, [In] uint cbFunc, out uint pNumFragments);

        void getFunctionFragments_VA([In] ulong vaFunc, [In] uint cbFunc, [In] uint cFragments, out ulong pVaFragment, out uint pLenFragment);

        void getFunctionFragments_RVA([In] uint rvaFunc, [In] uint cbFunc, [In] uint cFragments, out uint pRvaFragment, out uint pLenFragment);

        void getExports([MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);

        void getHeapAllocationSites([MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppResult);
    }
}