// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if FEATURE_WINDOWSINTEROP
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
#endif

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
#if FEATURE_WINDOWSINTEROP
        // Cached pointer to the IRunningObjectTable obtained on an MTA thread. Stored as nint
        // because ComScope<T> is a ref struct and cannot be a field. Released by the finalizer.
        private readonly Task<nint> _rotTask;
#endif

        public RunningObjectTable()
        {
#if FEATURE_WINDOWSINTEROP
            if (!NativeMethodsShared.IsWindows)
            {
                return;
            }

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
            {
                _rotTask = Task.FromResult(GetRunningObjectTablePointer());
            }
            else
            {
                // To avoid deadlock, create ROT on a threadpool thread which guarantees MTA.
                _rotTask = Task.Run(GetRunningObjectTablePointer);
            }
#endif
        }

#if FEATURE_WINDOWSINTEROP
        ~RunningObjectTable()
        {
            if (_rotTask is not null && _rotTask.Status == TaskStatus.RanToCompletion)
            {
                ReleasePointer(_rotTask.Result);
            }
        }

        [SupportedOSPlatform("windows5.0")]
        private static unsafe nint GetRunningObjectTablePointer()
        {
            IRunningObjectTable* rot;
            HRESULT hr = PInvoke.GetRunningObjectTable(0, &rot);
            return hr.Succeeded ? (nint)rot : 0;
        }

        private static unsafe void ReleasePointer(nint p)
        {
            if (p != 0)
            {
                ((IUnknown*)p)->Release();
            }
        }
#endif

        /// <summary>
        /// Attempts to retrieve an item from the ROT.
        /// </summary>
#if FEATURE_WINDOWSINTEROP
        [SupportedOSPlatform("windows5.0")]
#endif
        public object GetObject(string itemName)
        {
#if FEATURE_WINDOWSINTEROP
            return GetObjectWindows(itemName);
#else
            throw new PlatformNotSupportedException();
#endif
        }

#if FEATURE_WINDOWSINTEROP
        [SupportedOSPlatform("windows5.0")]
        private unsafe object GetObjectWindows(string itemName)
        {
            nint rotPtr = _rotTask.GetAwaiter().GetResult();
            if (rotPtr == 0)
            {
                throw new COMException("Failed to obtain the Running Object Table.");
            }

            // The IRunningObjectTable pointer was obtained on an MTA thread; calling through
            // it from an STA caller would bypass COM apartment marshalling. Run all ROT calls
            // on an MTA thread so the moniker creation and GetObject invocation happen in the
            // same apartment as the pointer's owner.
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
            {
                return GetObjectCore(rotPtr, itemName);
            }

            return Task.Run(() => GetObjectCore(rotPtr, itemName)).GetAwaiter().GetResult();
        }

        [SupportedOSPlatform("windows5.0")]
        private static unsafe object GetObjectCore(nint rotPtr, string itemName)
        {
            IRunningObjectTable* rot = (IRunningObjectTable*)rotPtr;

            IMoniker* monikerRaw;
            HRESULT hr = PInvoke.CreateItemMoniker("!", itemName, &monikerRaw);
            if (hr.Failed)
            {
                throw new COMException("CreateItemMoniker failed", hr);
            }

            using ComScope<IMoniker> moniker = new(monikerRaw);

            IUnknown* pUnk = null;
            hr = rot->GetObject(moniker.Pointer, &pUnk);
            if (hr.Failed)
            {
                ThrowComExceptionWithDetails(hr, itemName);
            }

            try
            {
                return Marshal.GetObjectForIUnknown((IntPtr)pUnk);
            }
            finally
            {
                if (pUnk is not null)
                {
                    pUnk->Release();
                }
            }
        }

        /// <summary>
        /// Throws an exception with detailed COM error information if available.
        /// </summary>
        [SupportedOSPlatform("windows5.0")]
        private static unsafe void ThrowComExceptionWithDetails(int hr, string itemName)
        {
            StringBuilder errorMessage = new();
            errorMessage.AppendLine($"Failed to get object '{itemName}' from Running Object Table.");
            errorMessage.AppendLine($"HRESULT: 0x{hr:X8} ({hr})");

            IErrorInfo* pErrorInfo = null;
            HRESULT errInfoHr = PInvoke.GetErrorInfo(0, &pErrorInfo);
            if (errInfoHr == HRESULT.S_OK && pErrorInfo is not null)
            {
                BSTR description = default;
                BSTR source = default;
                BSTR helpFile = default;
                uint helpContext = 0;

                try
                {
                    pErrorInfo->GetDescription(&description);
                    pErrorInfo->GetSource(&source);
                    pErrorInfo->GetHelpFile(&helpFile);
                    pErrorInfo->GetHelpContext(out helpContext);

                    AppendIfNotEmpty("description", description.ToString());
                    AppendIfNotEmpty("source", source.ToString());
                    AppendIfNotEmpty("help file", helpFile.ToString());
                    if (helpContext != 0)
                    {
                        errorMessage.AppendLine($"help context: {helpContext}");
                    }
                }
                catch
                {
                    // If we can't get error info details, just continue with the basic code.
                }
                finally
                {
                    if (description.Value is not null)
                    {
                        PInvoke.SysFreeString(description);
                    }
                    if (source.Value is not null)
                    {
                        PInvoke.SysFreeString(source);
                    }
                    if (helpFile.Value is not null)
                    {
                        PInvoke.SysFreeString(helpFile);
                    }
                    pErrorInfo->Release();
                }
            }

            throw new COMException(errorMessage.ToString(), hr);

            void AppendIfNotEmpty(string label, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    errorMessage.AppendLine($"{label}: {value}");
                }
            }
        }
#endif
    }
}
