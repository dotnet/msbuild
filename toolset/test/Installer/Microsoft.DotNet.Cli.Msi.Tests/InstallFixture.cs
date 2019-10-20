// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Dotnet.Cli.Msi.Tests
{
    public class InstallFixture : IDisposable
    {
        private MsiManager _msiMgr = null;

        // all the tests assume that the msi to be tested is available via environment variable %CLI_MSI%

        public InstallFixture()
        {
            string msiFile = Environment.GetEnvironmentVariable("CLI_MSI");
            if(string.IsNullOrEmpty(msiFile))
            {
                throw new InvalidOperationException("%CLI_MSI% must point to the msi that is to be tested");
            }

            _msiMgr = new MsiManager(msiFile);

            // make sure that the msi is not already installed, if so the machine is in a bad state
            Assert.False(_msiMgr.IsInstalled, "The dotnet CLI msi is already installed");

            _msiMgr.Install(InstallLocation);
            Assert.True(_msiMgr.IsInstalled);
        }

        public MsiManager MsiManager
        {
            get
            {
                return _msiMgr;
            }
        }

        public string InstallLocation
        {
            get
            {
                return Environment.ExpandEnvironmentVariables(@"%SystemDrive%\dotnet\");
            }
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
