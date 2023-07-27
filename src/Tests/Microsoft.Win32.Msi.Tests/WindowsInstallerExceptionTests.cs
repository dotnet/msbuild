// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi.Tests
{
    public class WindowsInstallerExceptionTests
    {
        [WindowsOnlyTheory]
        [InlineData(Error.ACCESS_DENIED, "Access is denied")]
        [InlineData(Error.INSTALL_PACKAGE_OPEN_FAILED, "This installation package could not be opened. Verify that the package exists and that you can access it, or contact the application vendor to verify that this is a valid Windows Installer package")]
        public void ItContainsValidErrorMessage(uint errorCode, string expectedMessage)
        {
            WindowsInstallerException e = new(unchecked((int)errorCode));

            // Exception messages are different between .NET Framework 4.7.2 (no "." at the end) and .NET 6.0 (includes ".")
            Assert.StartsWith(expectedMessage, e.Message);
        }
    }
}
