using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;

[assembly:InternalsVisibleTo("dotnet-new3.UnitTests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f33a29044fa9d740c9b3213a93e57c84b472c84e0b8a0e1ae48e67a9f8f6de9d5f7f3d52ac23e48ac51801f1dc950abe901da34d2a9e3baadb141a17c77ef3c565dd5ee5054b91cf63bb3c6ab83f72ab3aafe93d0fc3c2348b764fafb0b1c0733de51459aeab46580384bf9d74c4e28164b7cde247f891ba07891c9d872ad2bb")]

namespace dotnet_new3
{
    public class Program
    {
        private const string HostIdentifier = "dotnetcli";
        private const string HostVersion = "1.0.0";
        private const string CommandName = "new3";

        public static int Main(string[] args)
        {
            return New3Command.Run(CommandName, CreateHost(), FirstRun, args);
        }

        private static ITemplateEngineHost CreateHost()
        {
            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", "C#" }
            };

            try
            {
                string versionString = Command.CreateDotNet("", new[] { "--version" }).CaptureStdOut().Execute().StdOut;
                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    preferences["dotnet-cli-version"] = versionString.Trim();
                }
            }
            catch
            { }

            return new DefaultTemplateEngineHost(HostIdentifier, HostVersion, CultureInfo.CurrentCulture.Name, preferences);
        }

        private static void FirstRun(ITemplateEngineHost host, IInstaller installer)
        { 
            string[] packageList;

            if (Paths.Global.DefaultInstallPackageList.FileExists())
            {
                packageList = Paths.Global.DefaultInstallPackageList.ReadAllText().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (packageList.Length > 0)
                {
                    installer.InstallPackages(packageList);
                }
            }

            if (Paths.Global.DefaultInstallTemplateList.FileExists())
            {
                packageList = Paths.Global.DefaultInstallTemplateList.ReadAllText().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (packageList.Length > 0)
                {
                    installer.InstallPackages(packageList);
                }
            }
        }
    }
}
