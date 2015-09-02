namespace Microsoft.Win32.SafeHandles
{
    using System;
    using System.Runtime.InteropServices;

    internal abstract class SafeHandleZeroOrMinusOneIsInvalid : SafeHandle
    {
        protected SafeHandleZeroOrMinusOneIsInvalid(bool ownsHandle) : base(IntPtr.Zero, ownsHandle)
        {
        }

        public override bool IsInvalid
        {
            get
            { return handle == IntPtr.Zero || handle == new IntPtr(-1); }
        }
    }
}