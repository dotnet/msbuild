
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

public class MSBuildTestAssemblyFixture : IDisposable
{
    string _oldTempPath;
    bool _disposed;

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

        //  Use a project-specific temporary path
        //  This is so multiple test projects can be run in parallel without sharing the same temp directory
        _oldTempPath = Environment.GetEnvironmentVariable("TMP");
        string newTempPath = Path.Combine(AppContext.BaseDirectory, "Temp");
        if (!Directory.Exists(newTempPath))
        {
            Directory.CreateDirectory(newTempPath);
        }
        Environment.SetEnvironmentVariable("TMP", newTempPath);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            
            //  Ideally we would delete the temp directory, but this is apparently failing in some cases (possibly because
            //  tests are failing and not being cleaned up correctly)
            //Directory.Delete(Environment.GetEnvironmentVariable("TMP"), true);

            Environment.SetEnvironmentVariable("TMP", _oldTempPath);

            _disposed = true;
        }
    }
}
