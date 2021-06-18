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
            string codebase = typeof(TestUtils).GetTypeInfo().Assembly.Location;
            string dir = Path.GetDirectoryName(codebase);
            string templateLocation = Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates", templateName);

            if (!Directory.Exists(templateLocation))
            {
                throw new Exception($"{templateLocation} does not exist");
            }
            return Path.GetFullPath(templateLocation);
        }

        public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
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
    }
}
