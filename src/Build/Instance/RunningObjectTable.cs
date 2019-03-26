// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.Build.Execution
{
#if FEATURE_COM_INTEROP
    /// <summary>
    /// Wrapper for the COM Running Object Table.
    /// </summary>
    /// <remarks>
    /// See https://docs.microsoft.com/en-us/windows/desktop/api/objidl/nn-objidl-irunningobjecttable.
    /// </remarks>
    internal class RunningObjectTable : IDisposable, IRunningObjectTableWrapper
    {
        private readonly IRunningObjectTable rot;
        private bool isDisposed = false;

        public RunningObjectTable()
        {
            Ole32.GetRunningObjectTable(0, out this.rot);
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            Marshal.ReleaseComObject(this.rot);
            this.isDisposed = true;
        }

        /// <summary>
        /// Attempts to retrieve an item from the ROT.
        /// </summary>
        public object GetObject(string itemName)
        {
            IMoniker mk = CreateMoniker(itemName);
            int hr = this.rot.GetObject(mk, out object obj);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return obj;
        }

        private IMoniker CreateMoniker(string itemName)
        {
            Ole32.CreateItemMoniker("!", itemName, out IMoniker mk);
            return mk;
        }

        private static class Ole32
        {
            [DllImport(nameof(Ole32))]
            public static extern void CreateItemMoniker(
                [MarshalAs(UnmanagedType.LPWStr)] string lpszDelim,
                [MarshalAs(UnmanagedType.LPWStr)] string lpszItem,
                out IMoniker ppmk);

            [DllImport(nameof(Ole32))]
            public static extern void GetRunningObjectTable(
                int reserved,
                out IRunningObjectTable pprot);
        }
    }
#endif
}
