// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class TestUtils
    {
        public static string CreateTemporaryFolder(string name = "")
        {
            string workingDir = Path.Combine(Path.GetTempPath(), "TemplateEngine.Tests", Guid.NewGuid().ToString(), name);
            Directory.CreateDirectory(workingDir);
            return workingDir;
        }

        public static string GetTestTemplateLocation(string templateName)
        {
            return GetTestArtifactLocation(Path.Combine("test_templates", templateName));
        }

        public static string GetTestNugetLocation(string templateName = "TestNupkgInstallTemplateV2.0.0.2.nupkg")
        {
            string artifactsDir = GetTestArtifactLocation("nupkg_templates");
            string nupkgPath = Path.Combine(artifactsDir, templateName);

            if (!File.Exists(nupkgPath))
            {
                throw new FileNotFoundException($"{nupkgPath} does not exist");
            }

            return Path.GetFullPath(nupkgPath);
        }

        public static string CodeBaseRoot
        {
            get
            {
                string codebase = typeof(TestUtils).GetTypeInfo().Assembly.Location;
                string codeBaseRoot = new FileInfo(codebase).Directory.Parent.Parent.Parent.Parent.Parent.FullName;
                return codeBaseRoot;
            }
        }   

        private static string GetTestArtifactLocation(string artifactLocation)
        {
            string templateLocation = Path.Combine(CodeBaseRoot, "test", "Microsoft.TemplateEngine.TestTemplates", artifactLocation);

            if (!Directory.Exists(templateLocation))
            {
                throw new Exception($"{templateLocation} does not exist");
            }
            return Path.GetFullPath(templateLocation);
        }

        public static string GetPackagesLocation()
        {
#if DEBUG
            string configuration = "Debug";
#elif RELEASE
            string configuration = "Release";
#else
            throw new NotSupportedException("The configuration is not supported");
#endif

            string packagesLocation = Path.Combine(CodeBaseRoot, "artifacts", "packages", configuration, "Shipping");

            if (!Directory.Exists(packagesLocation))
            {
                throw new Exception($"{packagesLocation} does not exist");
            }
            return Path.GetFullPath(packagesLocation);
        }

        public static void SetupNuGetConfigForPackagesLocation(string projectDirectory)
        {
            string nugetConfigShim =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""globalPackagesFolder"" value=""{CreateTemporaryFolder("Packages")}"" />
  </config>
  <packageSources>
    <clear />
    <add key=""testPackages"" value=""{GetPackagesLocation()}"" />
  </packageSources>
</configuration>";

            File.WriteAllText(Path.Combine(projectDirectory, "nuget.config"), nugetConfigShim);
        }

        public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + dir.FullName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        public static bool CompareFiles(string file1, string file2)
        {
            using var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read);
            using var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read);
            if (fs1.Length != fs2.Length)
            {
                return false;
            }

            int file1byte;
            int file2byte;
            do
            {
                file1byte = fs1.ReadByte();
                file2byte = fs2.ReadByte();
            }
            while ((file1byte == file2byte) && (file1byte != -1));
            return ((file1byte - file2byte) == 0);
        }
    }
}
