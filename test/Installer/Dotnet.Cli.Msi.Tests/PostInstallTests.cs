using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Xunit;

namespace Dotnet.Cli.Msi.Tests
{
    public class PostInstallTests : IClassFixture<InstallFixture>
    {
        InstallFixture _fixture;
        MsiManager _msiMgr;

        public PostInstallTests(InstallFixture fixture)
        {
            _fixture = fixture;
            _msiMgr = fixture.MsiManager;
        }

        [Fact]
        public void DotnetOnPathTest()
        {
            Assert.True(_msiMgr.IsInstalled);

            Assert.True(Utils.ExistsOnPath("dotnet.exe"), "After installation dotnet tools must be on path");
        }

        [Fact]
        public void Dotnetx64RegKeysTest()
        {
            var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            CheckRegKeys(hklm);
        }

        [Fact]
        public void Dotnetx86RegKeysTest()
        {
            var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            CheckRegKeys(hklm);
        }

        private void CheckRegKeys(RegistryKey rootKey)
        {
            var regKey = rootKey.OpenSubKey(@"SOFTWARE\dotnet\Setup", false);

            Assert.NotNull(regKey);
            Assert.Equal(1, regKey.GetValue("Install"));
            Assert.Equal(_fixture.InstallLocation, regKey.GetValue("InstallDir"));
            Assert.NotNull(regKey.GetValue("Version"));
        }

        [Fact]
        public void UpgradeCodeTest()
        {
            // magic number found in https://github.com/dotnet/cli/blob/master/packaging/windows/variables.wxi
            Assert.Equal("{7D73E4F7-71E2-4236-8CF5-1C499BA3FF50}", _msiMgr.UpgradeCode);
        }
    }
}
