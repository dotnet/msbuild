// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{

        public void CreateDeb(
            string debianConfigJsonFile,
            string packageName,
            string packageVersion,
            string inputBinariesDirectory,
            Dictionary<string, string> debianConfigVariables,
            string outputFile,
            string manpagesDirectory = null,
            bool versionManpages = false,
            IEnumerable<string> debianFiles = null)
        {
            string debIntermediatesDirectory = Path.Combine(_intermediateDirectory, packageName, packageVersion);

            string layoutDirectory = Path.Combine(debIntermediatesDirectory, "debianLayoutDirectory");
            var debianLayoutDirectories = new DebianLayoutDirectories(layoutDirectory);

            CreateEmptyDebianLayout(debianLayoutDirectories);
            CopyFilesToDebianLayout(
                debianLayoutDirectories, 
                packageVersion,
                debianConfigJsonFile, 
                inputBinariesDirectory, 
                manpagesDirectory,
                versionManpages,
                debianFiles);
            ReplaceDebianConfigJsonVariables(debianLayoutDirectories, debianConfigVariables);
            CreateDebianPackage(debianLayoutDirectories, debIntermediatesDirectory, outputFile, packageName, packageVersion);
        }

        private void CopyFilesToDebianLayout(
            DebianLayoutDirectories layoutDirectories,
            string packageVersion,
            string debianConfigFile,
            string inputBinariesDirectory,
            string manpagesDirectory,
            bool versionManpages,
            IEnumerable<string> debianFiles)
        {
            FS.CopyRecursive(inputBinariesDirectory, layoutDirectories.PackageRoot);

            if (manpagesDirectory != null)
            {
                FS.CopyRecursive(manpagesDirectory, layoutDirectories.Docs);

                // Append version to all manpage files
                if (versionManpages)
                {
                    foreach (var file in Directory.EnumerateFiles(layoutDirectories.Docs, "*", SearchOption.AllDirectories))
                    {
                        var versionedFile = Path.Combine(
                            Path.GetDirectoryName(file),
                            $"{Path.GetFileNameWithoutExtension(file)}-{packageVersion}{Path.GetExtension(file)}");

                        if (File.Exists(versionedFile))
                        {
                            File.Delete(versionedFile);
                        }

                        File.Move(file, versionedFile);
                    }
                }
            }

            File.Copy(debianConfigFile,
                Path.Combine(layoutDirectories.LayoutDirectory, s_debianConfigJsonFileName));

            if (debianFiles != null)
            {
                foreach (var debianFile in debianFiles)
                {
                    File.Copy(debianFile,
                        Path.Combine(layoutDirectories.DebianFiles, Path.GetFileName(debianFile)));
                }
            }
        }

        private void ReplaceDebianConfigJsonVariables(
            DebianLayoutDirectories debianLayoutDirectories, 
            Dictionary<string, string> debianConfigVariables)
        {
            var debianConfigFile = Path.Combine(debianLayoutDirectories.LayoutDirectory, s_debianConfigJsonFileName);
            var debianConfigFileContents = File.ReadAllText(debianConfigFile);

            foreach (var variable in debianConfigVariables)
            {
                var variableToken = $"%{variable.Key}%";
                debianConfigFileContents = debianConfigFileContents.Replace(variableToken, variable.Value);
            }

            File.WriteAllText(debianConfigFile, debianConfigFileContents);
        }
    }
}
