// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using NuGet.Common;
using PathUtility = Microsoft.DotNet.Tools.Common.PathUtility;
using System.Xml.Linq;

namespace Microsoft.DotNet.TestFramework
{
    public class TestAssetInfo
    {
        private readonly string [] FilesToExclude = { ".DS_Store", ".noautobuild" };

        public string AssetName { get; private set; }

        public FileInfo DotnetExeFile { get; private set; }

        public string ProjectFilePattern { get; private set; }

        public DirectoryInfo Root { get; private set; }

        internal TestAssetInfo(DirectoryInfo root, string assetName, FileInfo dotnetExeFile, string projectFilePattern)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (string.IsNullOrWhiteSpace(assetName))
            {
                throw new ArgumentException("Argument cannot be null or whitespace", nameof(assetName));
            }

            if (dotnetExeFile == null)
            {
                throw new ArgumentNullException(nameof(dotnetExeFile));
            }

            if (string.IsNullOrWhiteSpace(projectFilePattern))
            {
                throw new ArgumentException("Argument cannot be null or whitespace", nameof(projectFilePattern));
            }

            Root = root;

            AssetName = assetName;

            DotnetExeFile = dotnetExeFile;

            ProjectFilePattern = projectFilePattern;

            //throw new Exception($"root = {_root}\nassetName = {_assetName}\ndotnetExeFile = {_dotnetExeFile}\nprojectFilePattern = {_projectFilePattern}\ndataDir = {_dataDirectory}\ndirectoriesToExclude = {string.Join<DirectoryInfo>(";",_directoriesToExclude)}");
        }

        public TestAssetInstance CreateInstance([CallerMemberName] string callingMethod = "", string identifier = "")
        {
            var instancePath = GetTestDestinationDirectory(callingMethod, identifier);

            var testInstance = new TestAssetInstance(this, instancePath);

            return testInstance;
        }

        internal IEnumerable<FileInfo> GetSourceFiles()
        {
            ThrowIfTestAssetDoesNotExist();

            return Root.GetFiles("*.*", SearchOption.AllDirectories)
                        .Where(f => !FilesToExclude.Contains(f.Name));
        }

        private DirectoryInfo GetTestDestinationDirectory(string callingMethod, string identifier)
        {
#if NET451
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string baseDirectory = AppContext.BaseDirectory;
#endif
            return new DirectoryInfo(Path.Combine(baseDirectory, callingMethod + identifier, AssetName));
        }

        private static string RebasePath(string path, string oldBaseDirectory, string newBaseDirectory)
        {
            path = Path.IsPathRooted(path) ? PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(oldBaseDirectory), path) : path;
            return Path.Combine(newBaseDirectory, path);
        }

        private bool IsAncestor(FileInfo file, DirectoryInfo maybeAncestor)
        {
            var dir = file.Directory;
            do
            {
                if (string.Equals(maybeAncestor.FullName, dir.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                dir = dir.Parent;
            }
            while (dir != null);

            return false;
        }

        //private void DoCopyFiles()
        //{
        //    Console.WriteLine($"TestAsset CopyFiles '{AssetName}'");

        //    _operationDirectory.Refresh();
        //    if (!_operationDirectory.Exists)
        //    {
        //        _operationDirectory.Create();
        //    }
        //    else
        //    {
        //        if (_operationDirectory.GetFiles().Any())
        //        {
        //            throw new Exception("operation files folder not empty");
        //        }
        //    }

        //    foreach (var f in GetOriginalFileList())
        //    {
        //        string destinationPath = RebasePath(f.FullName, Root.FullName, _operationDirectory.FullName);
        //        var destinationDir = new FileInfo(destinationPath).Directory;
        //        if (!destinationDir.Exists)
        //        {
        //            destinationDir.Create();
        //        }
        //        if (string.Equals(f.Name, "nuget.config", StringComparison.OrdinalIgnoreCase))
        //        {
        //            var doc = XDocument.Load(f.FullName, LoadOptions.PreserveWhitespace);
        //            foreach (var v in doc.Root.Element("packageSources").Elements("add").Attributes("value"))
        //            {
        //                if (!Path.IsPathRooted(v.Value))
        //                {
        //                    string fullPath = Path.GetFullPath(Path.Combine(f.Directory.FullName, v.Value));
        //                    if (!IsAncestor(new FileInfo(fullPath), Root))
        //                    {
        //                        v.Value = fullPath;
        //                    }
        //                }
                        
        //                //throw new Exception($"\nvalue = {v.Value}\n" +
        //                //    $"f.dir = {f.Directory.FullName}\n" +
        //                //    $"fullPath = {fullPath}");
                        
        //            }

        //            using (var file = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.ReadWrite))
        //            {
        //                doc.Save(file, SaveOptions.None);
        //            }
        //        }
        //        else
        //        {
        //            f.CopyTo(destinationPath);
        //        }
        //    }
        //}


        private void ThrowIfTestAssetDoesNotExist()
        {
            if (!Root.Exists)
            { 
                throw new DirectoryNotFoundException($"Directory not found at '{Root.FullName}'"); 
            } 
        }
    }
}
