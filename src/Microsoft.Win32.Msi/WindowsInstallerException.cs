// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Throws an exception for a Win32 error code.
    /// </summary>
    public class WindowsInstallerException : Win32Exception
    {
        public WindowsInstallerException() : base()
        {

        }

        public WindowsInstallerException(int error) : base(error)
        {

        }

        public WindowsInstallerException(string? message) : base(message)
        {

        }

        public WindowsInstallerException(int error, string? message) : base(error, message)
        {

        }

        public WindowsInstallerException(string? message, Exception? innerException) : base(message, innerException)
        {

        }
    }
}
