// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace dia2
{
	[Guid("624B7D9C-24EA-4421-9D06-3B577471C1FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[ComImport]
	public interface IDiaEnumSymbolsByAddr
	{
		
		[return: MarshalAs(UnmanagedType.Interface)]
		IDiaSymbol symbolByAddr([In] uint isect, [In] uint offset);
		
		[return: MarshalAs(UnmanagedType.Interface)]
		IDiaSymbol symbolByRVA([In] uint relativeVirtualAddress);
		
		[return: MarshalAs(UnmanagedType.Interface)]
		IDiaSymbol symbolByVA([In] ulong virtualAddress);
		
		void Next([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol rgelt, out uint pceltFetched);
		
		void Prev([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol rgelt, out uint pceltFetched);
		
		void Clone([MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbolsByAddr ppenum);
	}
}
