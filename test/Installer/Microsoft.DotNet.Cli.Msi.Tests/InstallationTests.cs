// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Dotnet.Cli.Msi.Tests
{
    public class InstallationTests : IDisposable
    {
        private MsiManager _msiMgr;

        public InstallationTests()
        {
            // all the tests assume that the msi to be tested is available via environment variable %CLI_MSI%
            var msiFile = Environment.GetEnvironmentVariable("CLI_MSI");
            if(string.IsNullOrEmpty(msiFile))
            {
                throw new InvalidOperationException("%CLI_MSI% must point to the msi that is to be tested");
            }

            _msiMgr = new MsiManager(msiFile);
        }

        [Fact]
        public void InstallTest()
        {
            string expectedInstallLocation = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\dotnet");

            // make sure that the msi is not already installed, if so the machine is in a bad state
            Assert.False(_msiMgr.IsInstalled, "The dotnet CLI msi is already installed");
            Assert.False(Directory.Exists(expectedInstallLocation));

            _msiMgr.Install();
            Assert.True(_msiMgr.IsInstalled);
            Assert.True(Directory.Exists(expectedInstallLocation));

            _msiMgr.UnInstall();
            Assert.False(_msiMgr.IsInstalled);
            Assert.False(Directory.Exists(expectedInstallLocation));
        }

        public void Dispose()
        {
            if (!_msiMgr.IsInstalled)
            {
                return;
            }

            _msiMgr.UnInstall();
            Assert.False(_msiMgr.IsInstalled, "Unable to cleanup by uninstalling dotnet");
        }
    }
}
