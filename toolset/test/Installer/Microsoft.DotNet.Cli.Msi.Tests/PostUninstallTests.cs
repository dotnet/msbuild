// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Win32;
using Xunit;

namespace Dotnet.Cli.Msi.Tests
{
    public class PostUninstallTests : InstallFixture
    {
        private MsiManager _msiMgr;

        public PostUninstallTests()
        {
            _msiMgr = base.MsiManager;
        }

        [Fact]
        public void DotnetOnPathTest()
        {
            Assert.True(_msiMgr.IsInstalled);

            _msiMgr.UnInstall();

            Assert.False(_msiMgr.IsInstalled);
            Assert.False(Utils.ExistsOnPath("dotnet.exe"), "After uninstallation dotnet tools must not be on path");
        }

        [Fact(Skip = "https://github.com/dotnet/cli/issues/2073")]
        public void DotnetRegKeysTest()
        {
            Assert.True(_msiMgr.IsInstalled);

            _msiMgr.UnInstall();

            Assert.False(_msiMgr.IsInstalled);

            var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            Assert.Null(hklm.OpenSubKey(@"SOFTWARE\dotnet\Setup", false));

            hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            Assert.Null(hklm.OpenSubKey(@"SOFTWARE\dotnet\Setup", false));
        }
    }
}
