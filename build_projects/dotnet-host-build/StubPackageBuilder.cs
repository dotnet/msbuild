using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.DotNet.Cli.Build;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Host.Build
{
    public class StubPackageBuilder
    {
        private DotNetCli _dotnet;
        private string _intermediateDirectory;
        private string _outputDirectory;

        private bool _stubBitsBuilt = false;

        public StubPackageBuilder(DotNetCli dotnet, string intermediateDirectory, string outputDirectory)
        {
            _dotnet = dotnet;
            _intermediateDirectory = intermediateDirectory;
            _outputDirectory = outputDirectory;
        }

        public void GeneratePackage(string packageId, string version)
        {
            if (! _stubBitsBuilt)
            {
                BuildStubBits(_dotnet, _intermediateDirectory);
            }

            CreateStubPackage(_dotnet, packageId, version, _intermediateDirectory, _outputDirectory);
        }

        private void BuildStubBits(DotNetCli dotnet, string intermediateDirectory)
        {
            var projectJson = new StringBuilder();
            projectJson.Append("{");
            projectJson.Append("  \"dependencies\": { \"NETStandard.Library\": \"1.5.0-rc2-24008\" },");
            projectJson.Append("  \"frameworks\": { \"netcoreapp1.0\": { \"imports\": [\"netstandard1.5\", \"dnxcore50\"] } },");
            projectJson.Append("  \"runtimes\": { \"win7-x64\": { } },");
            projectJson.Append("}");

            var programCs = "using System; namespace ConsoleApplication { public class Program { public static void Main(string[] args) { Console.WriteLine(\"Hello World!\"); } } }";

            var tempPjDirectory = Path.Combine(intermediateDirectory, "dummyNuGetPackageIntermediate");
            FS.Rmdir(tempPjDirectory);

            Directory.CreateDirectory(tempPjDirectory);

            var tempPjFile = Path.Combine(tempPjDirectory, "project.json");
            var tempSourceFile = Path.Combine(tempPjDirectory, "Program.cs");

            File.WriteAllText(tempPjFile, projectJson.ToString());
            File.WriteAllText(tempSourceFile, programCs.ToString());

            dotnet.Restore("--verbosity", "verbose", "--disable-parallel")
                .WorkingDirectory(tempPjDirectory)
                .Execute()
                .EnsureSuccessful();
            dotnet.Build(tempPjFile, "--runtime", "win7-x64")
                .WorkingDirectory(tempPjDirectory)
                .Execute()
                .EnsureSuccessful();

            _stubBitsBuilt = true;
        }

        private static void CreateStubPackage(DotNetCli dotnet, 
            string packageId, 
            string version,
            string intermediateDirectory, 
            string outputDirectory)
        {
            var projectJson = new StringBuilder();
            projectJson.Append("{");
            projectJson.Append($"  \"version\": \"{version}\",");
            projectJson.Append($"  \"name\": \"{packageId}\",");
            projectJson.Append("  \"dependencies\": { \"NETStandard.Library\": \"1.5.0-rc2-24008\" },");
            projectJson.Append("  \"frameworks\": { \"netcoreapp1.0\": { \"imports\": [\"netstandard1.5\", \"dnxcore50\"] } },");
            projectJson.Append("}");

            var tempPjDirectory = Path.Combine(intermediateDirectory, "dummyNuGetPackageIntermediate");
            var tempPjFile = Path.Combine(tempPjDirectory, "project.json");

            File.WriteAllText(tempPjFile, projectJson.ToString());

            dotnet.Pack(
                tempPjFile, "--no-build",
                "--output", outputDirectory)
                .WorkingDirectory(tempPjDirectory)
                .Execute()
                .EnsureSuccessful();
        }
    }
}
