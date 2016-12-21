using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Edge;

namespace dotnet_new3
{
    public class TemplateInstaller
    {
        private string _tempDir;

        public CommandResult InstallTemplate(string package, string version)
        {
            string content = GenerateProjectContent(package, version);
            string packageFileName = "temporary.csproj";
            string contentPath = Path.Combine(TempDir, packageFileName);
            contentPath.WriteAllText(content);

            IEnumerable<string> commandArgs = new List<string>()
            {
                contentPath,
                "--packages",
                TempDir
            };
            CommandResult result = Command.CreateDotNet("restore", commandArgs).Execute();

            return result;
        }

        public static bool TryParsePackageAndVersion(string packageSpecification, out string name, out string version)
        {
            string[] parts = packageSpecification.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Count() != 2)
            {
                name = null;
                version = null;
                return false;
            }

            name = parts[0];
            version = parts[1];
            return true;
        }

        public void Cleanup()
        {
            Paths.DeleteDirectory(TempDir);
        }

        ~TemplateInstaller()
        {
            Cleanup();
        }

        private string GenerateProjectContent(string package, string version)
        {
            string content = string.Format(@"<Project ToolsVersion=""15.0"" Sdk=""Microsoft.NET.Sdk"">
              <PropertyGroup>
                <TargetFrameworks>netcoreapp1.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include=""{0}"" Version=""{1}"" />
              </ItemGroup>
            </Project>"
                , package
                , version);
            return content;
        }

        // Generates a temp dir name, and creates the directory
        public string TempDir
        {
            get
            {
                if (_tempDir == null)
                {
                    string newDirName = Guid.NewGuid().ToString();
                    _tempDir = Path.Combine(Paths.User.ScratchDir, newDirName);
                    Paths.CreateDirectory(_tempDir);
                }

                return _tempDir;
            }
        }
    }
}
