// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Execution
{
#if FEATURE_COM_INTEROP
    /// <summary>
    /// Wrapper for the COM Running Object Table.
    /// </summary>
    /// <remarks>
    /// See https://docs.microsoft.com/en-us/windows/desktop/api/objidl/nn-objidl-irunningobjecttable.
    /// </remarks>
    internal class RunningObjectTable : IRunningObjectTableWrapper
    {
        private readonly Task<IRunningObjectTable> _rotTask;

        public RunningObjectTable()
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
            {
                Ole32.GetRunningObjectTable(0, out var rot);
                _rotTask = Task.FromResult(rot);
            }
            else
            {
                // To avoid deadlock, create ROT in a threadpool threads which guarantees to be MTA. And the
                // object will be MTA
                _rotTask =
                Task.Run(() =>
                    {
                        Ole32.GetRunningObjectTable(0, out var rot);
                        return rot;
                    });
            }
        }

        /// <summary>
        /// Attempts to retrieve an item from the ROT.
        /// </summary>
        public object GetObject(string itemName)
        {
            var rot = _rotTask.GetAwaiter().GetResult();

            IMoniker moniker;
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
            {
                Ole32.CreateItemMoniker("!", itemName, out moniker);
            }
            else
            {
                // To avoid deadlock, create Moniker in a threadpool threads which guarantees to be MTA. And the
                // object will be MTA
                var task = Task.Run(() =>
                {
                    Ole32.CreateItemMoniker("!", itemName, out var mk);
                    return mk;
                });

                moniker = task.GetAwaiter().GetResult();
            }

            int hr = rot.GetObject(moniker, out object obj);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return obj;
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
