
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

//  Register test framework for assembly fixture
[assembly: TestFramework("Xunit.NetCore.Extensions.XunitTestFrameworkWithAssemblyFixture", "Xunit.NetCore.Extensions")]

[assembly: AssemblyFixture(typeof(MSBuildTestAssemblyFixture))]

public class MSBuildTestAssemblyFixture
{
    public MSBuildTestAssemblyFixture()
    {
        //  Find correct version of "dotnet", and set DOTNET_HOST_PATH so that the Roslyn tasks will use the right host

        var currentFolder = System.AppContext.BaseDirectory;

        while (currentFolder != null)
        {
            string potentialVersionsPropsPath = Path.Combine(currentFolder, "build", "Versions.props");
            if (File.Exists(potentialVersionsPropsPath))
            {
                var doc = XDocument.Load(potentialVersionsPropsPath);
                var ns = doc.Root.Name.Namespace;
                var cliVersionElement = doc.Root.Elements(ns + "PropertyGroup").Elements(ns + "DotNetCliVersion").FirstOrDefault();
                if (cliVersionElement != null)
                {
                    string cliVersion = cliVersionElement.Value;
                    string dotnetPath = Path.Combine(currentFolder, "artifacts", ".dotnet", cliVersion, "dotnet");

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        dotnetPath += ".exe";
                    }

                    Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", dotnetPath);
                }

                break;
            }

            currentFolder = Directory.GetParent(currentFolder)?.FullName;
        }
    }
}
