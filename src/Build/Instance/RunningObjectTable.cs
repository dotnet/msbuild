using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Handy wrapper for the COM Running Object Table. The scope
    /// for this table is session-wide.
    /// </summary>
    internal class RunningObjectTable : IDisposable
    {
        private IRunningObjectTable _rot;

        public IRunningObjectTable GetRot() => _rot;

        public RunningObjectTable()
        {
            int result = Ole32.GetRunningObjectTable(0, out _rot);
            if (result != 0)
            {
                throw new Win32Exception(result);
            }
        }

        ~RunningObjectTable()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the Running Object Table instance.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_rot != null)
                {
                    Marshal.ReleaseComObject(_rot);
                }
                _rot = null;
            }
        }

        public IDisposable Register(string itemName, object obj)
        {
            IMoniker mk = CreateMoniker(itemName);

            int registration = _rot.Register(1, obj, mk);

            return new RegistrationHandle(this, registration);
        }

        public static IMoniker CreateMoniker(string itemName)
        {
            IMoniker mk;
            int result = Ole32.CreateItemMoniker("!", itemName, out mk);
            if (result != 0)
            {
                throw new Win32Exception(result);
            }
            return mk;
        }

        /// <summary>
        /// Attempts to retrieve an item from the ROT; returns null if not found.
        /// </summary>
        public object GetObject(string itemName)
        {
            IMoniker mk = CreateMoniker(itemName);
            object obj;
            try
            {
                int result = _rot.GetObject(mk, out obj);
                if (result != 0)
                {
                    throw new Win32Exception(result);
                }
                return obj;
            }
            catch (COMException ce)
            {
                // MK_E_UNAVAILABLE: The moniker is not in the ROT
                if (ce.ErrorCode == unchecked((int)0x800401e3))
                {
                    return null;
                }
                throw;
            }
        }

        private void Revoke(int registration)
        {
            _rot.Revoke(registration);
        }

        private class RegistrationHandle : IDisposable
        {
            private RunningObjectTable _rot;
            private int _registration;

            public RegistrationHandle(RunningObjectTable rot, int registration)
            {
                _rot = rot;
                _registration = registration;
            }

            ~RegistrationHandle()
            {
                // Can't use normal Dispose() because the _rot may already
                // have been garbage collected by this point
                IRunningObjectTable rot;
                int result = Ole32.GetRunningObjectTable(0, out rot);
                if (result == 0)
                {
                    rot.Revoke(_registration);
                    Marshal.ReleaseComObject(rot);
                }
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                _rot.Revoke(_registration);
            }
        }
    }

    internal class Ole32
    {
        [DllImport("Ole32.dll")]
        public static extern int CreateItemMoniker(
            [MarshalAs(UnmanagedType.LPWStr)] string lpszDelim,
            [MarshalAs(UnmanagedType.LPWStr)] string lpszItem,
            out IMoniker ppmk
            );

        [DllImport("Ole32.dll")]
        public static extern int GetRunningObjectTable(
            int reserved,
            out IRunningObjectTable pprot
            );
    }
}
