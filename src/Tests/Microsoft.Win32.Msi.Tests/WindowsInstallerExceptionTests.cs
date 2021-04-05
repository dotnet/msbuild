// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Win32.Msi;

namespace Microsoft.Win32.Msi.Tests
{
    public class WindowsInstallerExceptionTests
    {
        [Theory]
        [InlineData(Error.ACCESS_DENIED, "Access is denied.")]
        [InlineData(Error.INSTALL_PACKAGE_OPEN_FAILED, "This installation package could not be opened. Verify that the package exists and that you can access it, or contact the application vendor to verify that this is a valid Windows Installer package.")]
        public void ItContainsValidErrorMessage(uint errorCode, string expectedMessage)
        {
            WindowsInstallerException e = new(unchecked((int)errorCode));

            Assert.Equal(expectedMessage, e.Message);
        }
    }
}
