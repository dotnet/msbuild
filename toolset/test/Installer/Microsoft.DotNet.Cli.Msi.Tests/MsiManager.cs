// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Deployment.WindowsInstaller.Package;

namespace Dotnet.Cli.Msi.Tests
{
    public class MsiManager
    {
        private string _bundleFile;
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
            _bundleFile = Path.ChangeExtension(msiFile, "exe");
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

            RunBundle(dotnetHome);

            return IsInstalled;
        }

        public bool UnInstall()
        {
            if (!IsInstalled)
            {
                throw new InvalidOperationException($"UnInstall Error: Msi at {_msiFile} is not installed.");
            }

            RunBundle("/uninstall");

            return !IsInstalled;
        }

        private void RunBundle(string additionalArguments)
        {
            var arguments = $"/q /norestart {additionalArguments}";
            var process = Process.Start(_bundleFile, arguments);

            if (!process.WaitForExit(5 * 60 * 1000))
            {
                throw new InvalidOperationException($"Failed to wait for the installation operation to complete. Check to see if the installation process is still running. Command line: {_bundleFile} {arguments}");
            }

            else if (0 != process.ExitCode)
            {
                throw new InvalidOperationException($"The installation operation failed with exit code: {process.ExitCode}. Command line: {_bundleFile} {arguments}");
            }
        }
    }
}
