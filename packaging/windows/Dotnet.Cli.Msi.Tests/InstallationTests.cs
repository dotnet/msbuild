using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Dotnet.Cli.Msi.Tests
{
    public class InstallationTests : IDisposable
    {
        private string _msiFile;
        private MsiManager _msiMgr;

        public InstallationTests()
        {
            // all the tests assume that the msi to be tested is available via environment variable %CLI_MSI%
            _msiFile = Environment.GetEnvironmentVariable("CLI_MSI");
            if(string.IsNullOrEmpty(_msiFile))
            {
                throw new InvalidOperationException("%CLI_MSI% must point to the msi that is to be tested");
            }

            _msiMgr = new MsiManager(_msiFile);
        }

        
        [Theory]
        [InlineData("")]
        [InlineData(@"%SystemDrive%\dotnet")]
        public void InstallTest(string installLocation)
        {
            installLocation = Environment.ExpandEnvironmentVariables(installLocation);
            string expectedInstallLocation = string.IsNullOrEmpty(installLocation) ?
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\dotnet") :
                installLocation;            

            // make sure that the msi is not already installed, if so the machine is in a bad state
            Assert.False(_msiMgr.IsInstalled, "The dotnet CLI msi is already installed");
            Assert.False(Directory.Exists(expectedInstallLocation));

            _msiMgr.Install(installLocation);
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
