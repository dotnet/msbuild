using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.Build.Execution
{

    /// <summary>
    /// Wrapper for the COM Running Object Table.
    /// </summary>
    /// <remarks>
    /// See https://docs.microsoft.com/en-us/windows/desktop/api/objidl/nn-objidl-irunningobjecttable.
    /// </remarks>
    internal class RunningObjectTable : IDisposable
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
        /// Attempts to register an item in the ROT.
        /// </summary>
        public IDisposable Register(string itemName, object obj)
        {
            IMoniker moniker = CreateMoniker(itemName);

            const int ROTFLAGS_REGISTRATIONKEEPSALIVE = 1;
            int regCookie = this.rot.Register(ROTFLAGS_REGISTRATIONKEEPSALIVE, obj, moniker);

            return new RevokeRegistration(this, regCookie);
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

        private void Revoke(int regCookie)
        {
            this.rot.Revoke(regCookie);
        }

        private IMoniker CreateMoniker(string itemName)
        {
            Ole32.CreateItemMoniker("!", itemName, out IMoniker mk);
            return mk;
        }

        private class RevokeRegistration : IDisposable
        {
            private readonly RunningObjectTable rot;
            private readonly int regCookie;

            public RevokeRegistration(RunningObjectTable rot, int regCookie)
            {
                this.rot = rot;
                this.regCookie = regCookie;
            }

            public void Dispose()
            {
                this.rot.Revoke(this.regCookie);
            }
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
}
