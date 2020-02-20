// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    }
}
