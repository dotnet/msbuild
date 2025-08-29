// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

#nullable disable

namespace Microsoft.Build.Execution
{
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
            if (!NativeMethodsShared.IsWindows)
            {
                return;
            }

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
#pragma warning disable CA1416 // Validate platform compatibility: we checked above but the analyzer misses it
                        Ole32.GetRunningObjectTable(0, out var rot);
#pragma warning restore CA1416 // Validate platform compatibility
                        return rot;
                    });
            }
        }

        /// <summary>
        /// Attempts to retrieve an item from the ROT.
        /// </summary>
        [SupportedOSPlatform("windows")]
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

        [SupportedOSPlatform("windows")]
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
}
