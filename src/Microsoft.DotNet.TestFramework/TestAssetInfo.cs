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
        private const string DataDirectoryName = ".tam";

        private readonly string [] FilesToExclude = { ".DS_Store", ".noautobuild" };

        private readonly DirectoryInfo [] _directoriesToExclude;

        private readonly string _assetName;

        private readonly DirectoryInfo _dataDirectory;

        private readonly DirectoryInfo _root;

        private readonly DirectoryInfo _operationDirectory;

        private readonly TestAssetInventoryFiles _inventoryFiles;

        private readonly FileInfo _dotnetExeFile;

        private readonly string _projectFilePattern;

        internal DirectoryInfo Root 
        {
            get
            {
                return _operationDirectory;
            }
        }

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

            _root = root;

            _assetName = assetName;

            _dotnetExeFile = dotnetExeFile;

            _projectFilePattern = projectFilePattern;

            _dataDirectory = _root.GetDirectory(DataDirectoryName);

            _operationDirectory = _dataDirectory.GetDirectory("files");

            _inventoryFiles = new TestAssetInventoryFiles(_dataDirectory);

            _directoriesToExclude = new []
            {
                _dataDirectory
            };

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

            RemoveCacheIfSourcesHaveChanged();
            
            return GetInventory(
                _inventoryFiles.Source,
                null,
                DoCopyFiles);
        }

        internal IEnumerable<FileInfo> GetRestoreFiles()
        {
            ThrowIfTestAssetDoesNotExist();

            RemoveCacheIfSourcesHaveChanged();
            
            return GetInventory(
                _inventoryFiles.Restore, 
                GetSourceFiles, 
                DoRestore);
        }

        internal IEnumerable<FileInfo> GetBuildFiles()
        {
            ThrowIfTestAssetDoesNotExist();

            RemoveCacheIfSourcesHaveChanged();
            
            return GetInventory(
                _inventoryFiles.Build,
                () => GetRestoreFiles()
                        .Concat(GetSourceFiles()), // TODO: likely not needed
                DoBuild);
        }

        private DirectoryInfo GetTestDestinationDirectory(string callingMethod, string identifier)
        {
#if NET451
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string baseDirectory = AppContext.BaseDirectory;
#endif
            return new DirectoryInfo(Path.Combine(baseDirectory, callingMethod + identifier, _assetName));
        }

        private IEnumerable<FileInfo> GetOperationFileList()
        {
            return _operationDirectory.GetFiles("*.*", SearchOption.AllDirectories);
        }

        private IEnumerable<FileInfo> GetOriginalFileList()
        {
            return _root.GetFiles("*.*", SearchOption.AllDirectories)
                        .Where(f => !_directoriesToExclude.Any(d => d.Contains(f)))
                        .Where(f => !FilesToExclude.Contains(f.Name));    
        }

        private IEnumerable<FileInfo> GetInventory(
            FileInfo file,
            Func<IEnumerable<FileInfo>> beforeAction,
            Action action)
        {
            var inventory = Enumerable.Empty<FileInfo>();

            IEnumerable<FileInfo> preInventory;

            if (beforeAction == null)
            {
                preInventory = new List<FileInfo>();
            }
            else
            {
                preInventory = beforeAction();
            }

            ExclusiveFolderAccess.Do(_dataDirectory, (folder) => {
                file.Refresh();
                if (file.Exists)
                {
                    inventory = folder.LoadInventory(file);
                }
                else
                {
                    action();

                    inventory = GetOperationFileList().Where(i => !preInventory.Select(p => p.FullName).Contains(i.FullName));

                    folder.SaveInventory(file, inventory);
                }
            });

            return inventory;
        }

        private static string RebasePath(string path, string oldBaseDirectory, string newBaseDirectory)
        {
            path = Path.IsPathRooted(path) ? PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(oldBaseDirectory), path) : path;
            return Path.Combine(newBaseDirectory, path);
        }

        private void RemoveOperationFiles()
        {
            foreach (var opFile in GetOperationFileList())
            {
                opFile.Delete();
            }

            foreach (var f in _inventoryFiles.AllInventoryFiles)
            {
                f.Delete();
            }
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

        private void DoCopyFiles()
        {
            Console.WriteLine($"TestAsset CopyFiles '{_assetName}'");

            _operationDirectory.Refresh();
            if (!_operationDirectory.Exists)
            {
                _operationDirectory.Create();
            }
            else
            {
                if (_operationDirectory.GetFiles().Any())
                {
                    throw new Exception("operation files folder not empty");
                }
            }

            foreach (var f in GetOriginalFileList())
            {
                string destinationPath = RebasePath(f.FullName, _root.FullName, _operationDirectory.FullName);
                var destinationDir = new FileInfo(destinationPath).Directory;
                if (!destinationDir.Exists)
                {
                    destinationDir.Create();
                }
                if (string.Equals(f.Name, "nuget.config", StringComparison.OrdinalIgnoreCase))
                {
                    var doc = XDocument.Load(f.FullName, LoadOptions.PreserveWhitespace);
                    foreach (var v in doc.Root.Element("packageSources").Elements("add").Attributes("value"))
                    {
                        if (!Path.IsPathRooted(v.Value))
                        {
                            string fullPath = Path.GetFullPath(Path.Combine(f.Directory.FullName, v.Value));
                            if (!IsAncestor(new FileInfo(fullPath), _root))
                            {
                                v.Value = fullPath;
                            }
                        }
                        
                        //throw new Exception($"\nvalue = {v.Value}\n" +
                        //    $"f.dir = {f.Directory.FullName}\n" +
                        //    $"fullPath = {fullPath}");
                        
                    }

                    using (var file = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.ReadWrite))
                    {
                        doc.Save(file, SaveOptions.None);
                    }
                }
                else
                {
                    f.CopyTo(destinationPath);
                }
            }
        }

        private void DoRestore()
        {
            //throw new Exception("foooooo");
            try
            {
                Console.WriteLine($"TestAsset Restore '{_assetName}'");

                _operationDirectory.Refresh();
                var projFiles = _operationDirectory.GetFiles(_projectFilePattern, SearchOption.AllDirectories);

                foreach (var projFile in projFiles)
                {
                    var restoreArgs = new string[] { "restore", projFile.FullName };

                    var commandResult = Command.Create(_dotnetExeFile.FullName, restoreArgs)
                                        .CaptureStdOut()
                                        .CaptureStdErr()
                                        .Execute();

                    int exitCode = commandResult.ExitCode;

                    if (exitCode != 0)
                    {
                        Console.WriteLine(commandResult.StdOut);

                        Console.WriteLine(commandResult.StdErr);

                        string message = string.Format($"TestAsset Restore '{_assetName}'@'{projFile.FullName}' Failed with {exitCode}");

                        throw new Exception($"TestAsset {_dotnetExeFile.FullName} {string.Join(" ", restoreArgs)}");
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"NOOOOOOOOOOOOOOOOOOOOOOOOOOOOO:\n{e.Message}");
            }
        }

        private void DoBuild()
        {
            string[] args = new string[] { "build" };

            Console.WriteLine($"TestAsset Build '{_assetName}'");

            var commandResult = Command.Create(_dotnetExeFile.FullName, args) 
                                    .WorkingDirectory(_operationDirectory.FullName)
                                    .CaptureStdOut()
                                    .CaptureStdErr()
                                    .Execute();

            int exitCode = commandResult.ExitCode;

            if (exitCode != 0)
            {
                Console.WriteLine(commandResult.StdOut);

                Console.WriteLine(commandResult.StdErr);

                string message = string.Format($"TestAsset Build '{_assetName}' Failed with {exitCode}");
                
                throw new Exception(message);
            }
        }

        private bool HaveSourcesChanged(ExclusiveFolderAccess folder)
        {
            var originalFiles = GetOriginalFileList();
            var originalFilesRebased = originalFiles.Select(f => RebasePath(f.FullName, _root.FullName, _operationDirectory.FullName));
            var trackedOriginalFiles = folder.LoadInventory(_inventoryFiles.Source);

            bool hasUntrackedFiles = originalFilesRebased.Any(a => !trackedOriginalFiles.Any(t => t.FullName.Equals(a)));
            if (hasUntrackedFiles)
            {
                return true;
            }

            bool hasMissingFiles = trackedOriginalFiles.Any(t => !File.Exists(RebasePath(t.FullName, _operationDirectory.FullName, _root.FullName)));
            if (hasMissingFiles)
            {
                return true;
            }

            foreach (var origFile in originalFiles)
            {
                var copiedFile = new FileInfo(RebasePath(origFile.FullName, _root.FullName, _operationDirectory.FullName));
                if (origFile.LastWriteTimeUtc != copiedFile.LastWriteTimeUtc)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveCacheIfSourcesHaveChanged()
        {
            ExclusiveFolderAccess.Do(_dataDirectory, (folder) => {
                _operationDirectory.Refresh();
                if (!_operationDirectory.Exists)
                {
                    return;
                }

                if (HaveSourcesChanged(folder))
                {
                    Console.WriteLine("Sources have changed................................");
                    RemoveOperationFiles();
                }
            });
        }

        private void ThrowIfTestAssetDoesNotExist()
        {
            if (!_root.Exists)
            { 
                throw new DirectoryNotFoundException($"Directory not found at '{_root.FullName}'"); 
            } 
        }

        private class ExclusiveFolderAccess
        {
            private DirectoryInfo _directory;

            private ExclusiveFolderAccess(DirectoryInfo directory)
            {
                _directory = directory;
            }

            public static void Do(DirectoryInfo directory, Action<ExclusiveFolderAccess> action)
            {
                Task.Run(async () => await ConcurrencyUtilities.ExecuteWithFileLockedAsync<object>(
                    directory.FullName, 
                    lockedToken =>
                    {
                        action(new ExclusiveFolderAccess(directory));
                        return Task.FromResult(new Object());
                    },
                    CancellationToken.None)).Wait();
            }

            public static IEnumerable<FileInfo> Read(FileInfo file)
            {
                IEnumerable<FileInfo> ret = null;
                Do(file.Directory, (folder) => {
                    ret = folder.LoadInventory(file);
                });

                return ret;
            }

            public IEnumerable<FileInfo> LoadInventory(FileInfo file)
            {
                file.Refresh();
                if (!file.Exists)
                {
                    return Enumerable.Empty<FileInfo>();
                }

                var inventory = new List<FileInfo>();
                foreach (var p in File.ReadAllLines(file.FullName))
                {
                    inventory.Add(new FileInfo(p));
                }

                return inventory;
            }

            public void SaveInventory(FileInfo file, IEnumerable<FileInfo> inventory)
            {
                _directory.Refresh();
                if (!_directory.Exists)
                {
                    _directory.Create();
                }

                File.WriteAllLines(file.FullName, inventory.Select((fi) => fi.FullName).ToList());
            }
        }
    }
}
