// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.DiaSymReader;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public class FullPdbReader : IPdbReader
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        [DllImport("Microsoft.DiaSymReader.Native.x86.dll", EntryPoint = "CreateSymReader")]
        private extern static void CreateSymReader32(ref Guid id, [MarshalAs(UnmanagedType.IUnknown)]out object symReader);

        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        [DllImport("Microsoft.DiaSymReader.Native.amd64.dll", EntryPoint = "CreateSymReader")]
        private extern static void CreateSymReader64(ref Guid id, [MarshalAs(UnmanagedType.IUnknown)]out object symReader);

        private readonly ISymUnmanagedReader3 _symReader;

        public FullPdbReader(Stream pdbStream)
        {
            pdbStream.Position = 0;

            _symReader = CreateNativeSymReader(pdbStream);
        }

        public SourceInformation GetSourceInformation(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                return null;
            }

            var methodToken = methodInfo.GetMethodToken();

            var method = GetMethod(methodToken);

            return method?.GetSourceInformation();
        }

        private ISymUnmanagedMethod GetMethod(int methodToken)
        {
            ISymUnmanagedMethod method;
            _symReader.GetMethod(methodToken, out method);
            return method;
        }

        private static ISymUnmanagedReader3 CreateNativeSymReader(Stream pdbStream)
        {
            object symReader = null;
            var guid = default(Guid);
            if (IntPtr.Size == 4)
            {
                CreateSymReader32(ref guid, out symReader);
            }
            else
            {
                CreateSymReader64(ref guid, out symReader);
            }
            var reader = (ISymUnmanagedReader3)symReader;
            var hr = reader.Initialize(new DummyMetadataImport(), null, null, new ComStreamWrapper(pdbStream));
            SymUnmanagedReaderExtensions.ThrowExceptionForHR(hr);
            return reader;
        }

        public void Dispose()
        {
            ((ISymUnmanagedDispose) _symReader).Destroy();
        }
    }

    [ComVisible(false)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44")]
    interface IMetadataImport { }

    class DummyMetadataImport : IMetadataImport { }
}
