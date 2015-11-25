using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Deployment.WindowsInstaller.Package;


namespace Dotnet.Cli.Msi.Tests
{
    public class MsiManager
    {
        private string _msiFile;
        private string _productCode;
        private InstallPackage _installPackage;

        public ProductInstallation Installation
        {
            get
            {
                return ProductInstallation.AllProducts.SingleOrDefault(p => p.ProductCode == _productCode);
            }
        }

        public string InstallLocation
        {
            get
            {
                return IsInstalled ? Installation.InstallLocation : null;
            }
        }

        public bool IsInstalled
        {
            get
            {
                var prodInstall = Installation;
                return Installation == null ? false : prodInstall.IsInstalled;
            }
        }

        public string UpgradeCode
        {
            get
            {
                return _installPackage.Property["UpgradeCode"];
            }
        }

        public MsiManager(string msiFile)
        {
            _msiFile = msiFile;

            var ispackage = Installer.VerifyPackage(msiFile);
            if (!ispackage)
            {
                throw new ArgumentException("Not a valid MSI file", msiFile);
            }

            _installPackage = new InstallPackage(msiFile, DatabaseOpenMode.ReadOnly);
            _productCode = _installPackage.Property["ProductCode"];
        }

        public bool Install(string customLocation = null)
        {
            string dotnetHome = "";
            if (!string.IsNullOrEmpty(customLocation))
            {
                dotnetHome = $"DOTNETHOME={customLocation}";
            }
            Installer.SetInternalUI(InstallUIOptions.Silent);
            Installer.InstallProduct(_msiFile, $"ACTION=INSTALL ALLUSERS=2 ACCEPTEULA=1 {dotnetHome}");

            return IsInstalled;
        }

        public bool UnInstall()
        {
            if (!IsInstalled)
            {
                throw new InvalidOperationException($"UnInstall Error: Msi at {_msiFile} is not installed.");
            }

            Installer.SetInternalUI(InstallUIOptions.Silent);
            Installer.InstallProduct(_msiFile, "REMOVE=ALL");

            return !IsInstalled;
        }
    }
}
