using System;
using System.Collections.Generic;
using System.Linq;
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

        [Fact]
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
