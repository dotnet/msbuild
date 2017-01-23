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

        private readonly TestAssetInventoryFiles _inventoryFiles;

        private readonly FileInfo _dotnetExeFile;

        private readonly string _projectFilePattern;

        internal DirectoryInfo Root 
        {
            get
            {
                return _root;
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

            _inventoryFiles = new TestAssetInventoryFiles(_dataDirectory);

            _directoriesToExclude = new []
            {
                _dataDirectory
            };
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

            ThrowIfAssetSourcesHaveChanged();
            
            return GetInventory(
                _inventoryFiles.Source, 
                null, 
                () => {});
        }

        internal IEnumerable<FileInfo> GetRestoreFiles()
        {
            ThrowIfTestAssetDoesNotExist();

            ThrowIfAssetSourcesHaveChanged();
            
            return GetInventory(
                _inventoryFiles.Restore, 
                GetSourceFiles, 
                DoRestore);
        }

        internal IEnumerable<FileInfo> GetBuildFiles()
        {
            ThrowIfTestAssetDoesNotExist();

            ThrowIfAssetSourcesHaveChanged();
            
            return GetInventory(
                _inventoryFiles.Build,
                () => GetRestoreFiles()
                        .Concat(GetSourceFiles()),
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

        private IEnumerable<FileInfo> GetFileList()
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

                    inventory = GetFileList().Where(i => !preInventory.Select(p => p.FullName).Contains(i.FullName));

                    folder.SaveInventory(file, inventory);
                }
            });

            return inventory;
        }

        private void DoRestore()
        {
            Console.WriteLine($"TestAsset Restore '{_assetName}'");

            var projFiles = _root.GetFiles(_projectFilePattern, SearchOption.AllDirectories);

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

                    throw new Exception(message);
                }
            }
        }

        private void DoBuild()
        {
            string[] args = new string[] { "build" };

            Console.WriteLine($"TestAsset Build '{_assetName}'");

            var commandResult = Command.Create(_dotnetExeFile.FullName, args) 
                                    .WorkingDirectory(_root.FullName)
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

        private void ThrowIfAssetSourcesHaveChanged()
        {
            if (!_dataDirectory.Exists)
            {
                return;
            }

            var dataDirectoryFiles = _dataDirectory.GetFiles("*", SearchOption.AllDirectories);

            if (!dataDirectoryFiles.Any())
            {
                return;
            }

            IEnumerable<FileInfo> trackedFiles = null;
            ExclusiveFolderAccess.Do(_dataDirectory, (folder) => {
                trackedFiles = _inventoryFiles.AllInventoryFiles.SelectMany(f => folder.LoadInventory(f));
            });

            var assetFiles = GetFileList();

            var untrackedFiles = assetFiles.Where(a => !trackedFiles.Any(t => t.FullName.Equals(a.FullName)));

            if (untrackedFiles.Any())
            {
                var message = $"TestAsset {_assetName} has untracked files. " +
                    "Consider cleaning the asset and deleting its `.tam` directory to " + 
                    "recreate tracking files.\n\n" +
                    $".tam directory: {_dataDirectory.FullName}\n" +
                    "Untracked Files: \n";

                message += String.Join("\n", untrackedFiles.Select(f => $" - {f.FullName}\n"));

                throw new Exception(message);
            }

            var earliestDataDirectoryTimestamp =
                dataDirectoryFiles
                    .OrderBy(f => f.LastWriteTime)
                    .First()
                    .LastWriteTime;

            if (earliestDataDirectoryTimestamp == null)
            {
                return;
            }

            var updatedSourceFiles = ExclusiveFolderAccess.Read(_inventoryFiles.Source)
                .Where(f => f.LastWriteTime > earliestDataDirectoryTimestamp);

            if (updatedSourceFiles.Any())
            {
                var message = $"TestAsset {_assetName} has updated files. " +
                    "Consider cleaning the asset and deleting its `.tam` directory to " + 
                    "recreate tracking files.\n\n" +
                    $".tam directory: {_dataDirectory.FullName}\n" +
                    "Updated Files: \n";

                message += String.Join("\n", updatedSourceFiles.Select(f => $" - {f.FullName}\n"));

                throw new GracefulException(message);
            }
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
