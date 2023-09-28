// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.DotNet.Tools.Test.Utilities.Mock;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.Extensions.DependencyModel.Tests
{
    public class FileSystemMockBuilder
    {
        private readonly List<Action> _actions = new();
        private MockFileSystemModel _mockFileSystemModel;
        public string TemporaryFolder { get; set; }
        public string WorkingDirectory { get; set; }

        internal static IFileSystem Empty { get; } = Create().Build();

        public static FileSystemMockBuilder Create()
        {
            return new FileSystemMockBuilder();
        }

        public FileSystemMockBuilder AddFile(string name, string content = "")
        {
            _actions.Add(() => _mockFileSystemModel.CreateDirectory(Path.GetDirectoryName(name)));
            _actions.Add(() => _mockFileSystemModel.CreateFile(name, content));
            return this;
        }

        public FileSystemMockBuilder AddFiles(string basePath, params string[] files)
        {
            _actions.Add(() => _mockFileSystemModel.CreateDirectory(basePath));

            foreach (string file in files)
            {
                _actions.Add(() => _mockFileSystemModel.CreateFile(Path.Combine(basePath, file), ""));
            }

            return this;
        }

        /// <summary>
        /// Just a "home" means different path on Windows and Unix.
        /// Create a platform dependent Temporary directory path and use it to avoid further misinterpretation in
        /// later tests. Like "c:/home vs /home". Instead always use Path.Combine(TemporaryDirectory, "home")
        /// </summary>
        public FileSystemMockBuilder UseCurrentSystemTemporaryDirectory()
        {
            TemporaryFolder = Path.GetTempPath();
            return this;
        }

        internal IFileSystem Build()
        {
            _mockFileSystemModel =
                new MockFileSystemModel(TemporaryFolder, fileSystemMockWorkingDirectory: WorkingDirectory);

            foreach (Action action in _actions)
            {
                action();
            }

            return new FileSystemMock(_mockFileSystemModel);
        }

        private class MockFileSystemModel
        {
            public MockFileSystemModel(string temporaryFolder,
                FileSystemRoot files = null,
                string fileSystemMockWorkingDirectory = null)
            {
                if (fileSystemMockWorkingDirectory == null)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        fileSystemMockWorkingDirectory = @"C:\";
                    }
                    else
                    {
                        fileSystemMockWorkingDirectory = "/";
                    }
                }

                WorkingDirectory = fileSystemMockWorkingDirectory;
                TemporaryFolder =
                    temporaryFolder ?? Path.Combine(fileSystemMockWorkingDirectory, "mockTemporaryFolder");
                Files = files ?? new FileSystemRoot();
                CreateDirectory(WorkingDirectory);
            }

            public string WorkingDirectory { get; }
            public string TemporaryFolder { get; }
            public FileSystemRoot Files { get; }

            public bool TryGetNodeParent(string path, out DirectoryNode current)
            {
                PathModel pathModel = CreateFullPathModel(path);
                current = Files.Volume[pathModel.Volume];

                if (!Files.Volume.ContainsKey(pathModel.Volume))
                {
                    return false;
                }

                for (int i = 0; i < pathModel.PathArray.Length - 1; i++)
                {
                    string p = pathModel.PathArray[i];

                    if (current.Subs.TryGetValue(p, out var node) && node is DirectoryNode directoryNode)
                    {
                        current = directoryNode;
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }

            public void CreateDirectory(string path)
            {
                PathModel pathModel = CreateFullPathModel(path);

                if (!Files.Volume.TryGetValue(pathModel.Volume, out DirectoryNode current))
                {
                    current = new DirectoryNode();
                    current = Files.Volume.GetOrAdd(pathModel.Volume, current);
                }

                foreach (string p in pathModel.PathArray)
                {
                    if (current.Subs.TryGetValue(p, out var node))
                    {
                        if (node is DirectoryNode directoryNode)
                        {
                            current = directoryNode;
                        }
                        else
                        {
                            throw new IOException(
                                $"Cannot create '{pathModel}' because a file or directory with the same name already exists.");
                        }
                    }
                    else
                    {
                        DirectoryNode directoryNode = new();
                        directoryNode = (DirectoryNode)current.Subs.GetOrAdd(p, directoryNode);
                        current = directoryNode;
                    }
                }
            }

            private PathModel CreateFullPathModel(string path)
            {
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(WorkingDirectory, path);
                }

                PathModel pathModel = new(path);

                return pathModel;
            }

            public void CreateFile(string path, string content)
            {
                PathModel pathModel = CreateFullPathModel(path);

                if (TryGetNodeParent(path, out DirectoryNode current) && current != null)
                {
                    if (current.Subs.TryGetValue(pathModel.FileOrDirectoryName(), out var possibleConflict) &&
                        possibleConflict is DirectoryNode)
                    {
                        throw new IOException($"{path} is a directory");
                    }
                    else
                    {
                        current.Subs[pathModel.FileOrDirectoryName()] = new FileNode(content);
                    }
                }
                else
                {
                    throw new DirectoryNotFoundException(
                        $"Could not find a part of the path {path}. Additional from mock file system, cannot find parent directory");
                }
            }

            public (DirectoryNode, FileNode) GetParentDirectoryAndFileNode(string path, Action onNotAFile)
            {
                if (TryGetNodeParent(path, out DirectoryNode current) && current != null)
                {
                    PathModel pathModel = new(path);

                    if (current.Subs.TryGetValue(pathModel.FileOrDirectoryName(), out var node))
                    {
                        if (node is FileNode fileNode)
                        {
                            return (current, fileNode);
                        }

                        onNotAFile();
                    }
                }

                throw new FileNotFoundException($"Could not find file '{path}'");
            }

            public IEnumerable<string> EnumerateDirectory(
                string path,
                Func<ConcurrentDictionary<string, IFileSystemTreeNode>, IEnumerable<string>> predicate)
            {
                DirectoryNode current = GetParentOfDirectoryNode(path);

                PathModel pathModel = new(path);
                DirectoryNode directoryNode = current.Subs[pathModel.FileOrDirectoryName()] as DirectoryNode;

                Debug.Assert(directoryNode != null, nameof(directoryNode) + " != null");

                return predicate(directoryNode.Subs);
            }

            public DirectoryNode GetParentOfDirectoryNode(string path)
            {
                if (!TryGetNodeParent(path, out DirectoryNode current) || current == null)
                {
                    throw new DirectoryNotFoundException($"Could not find a part of the path {path}");
                }

                PathModel pathModel = CreateFullPathModel(path);
                if (current.Subs.TryGetValue(pathModel.FileOrDirectoryName(), out var node))
                {
                    if (node is FileNode)
                    {
                        throw new IOException("Not a directory");
                    }
                }
                else
                {
                    throw new DirectoryNotFoundException($"Could not find a part of the path {path}");
                }

                return current;
            }
        }

        private class PathModel
        {
            public PathModel(string path)
            {
                const char directorySeparatorChar = '\\';
                const char altDirectorySeparatorChar = '/';

                bool isRooted = false;
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new ArgumentException(nameof(path) + ": " + path);
                }

                string volume = "";
                if (Path.IsPathRooted(path))
                {
                    isRooted = true;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        int charLocation = path.IndexOf(":", StringComparison.Ordinal);

                        if (charLocation > 0)
                        {
                            volume = path.Substring(0, charLocation).ToLowerInvariant();
                            path = path.Substring(charLocation + 2);
                        }
                    }
                }

                string[] pathArray = path.Split(
                    new[] { directorySeparatorChar, altDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries);
                Volume = volume;
                PathArray = pathArray;
                IsRooted = isRooted;
            }

            public PathModel(bool isRooted, string volume, string[] pathArray)
            {
                IsRooted = isRooted;
                Volume = volume ?? throw new ArgumentNullException(nameof(volume));
                PathArray = pathArray ?? throw new ArgumentNullException(nameof(pathArray));
            }

            public bool IsRooted { get; }
            public string Volume { get; }
            public string[] PathArray { get; }

            public override string ToString()
            {
                return $"{nameof(IsRooted)}: {IsRooted}" +
                       $", {nameof(Volume)}: {Volume}" +
                       $", {nameof(PathArray)}: {string.Join("-", PathArray)}";
            }

            public string FileOrDirectoryName()
            {
                return PathArray[PathArray.Length - 1];
            }
        }

        private class FileSystemMock : IFileSystem
        {
            public FileSystemMock(MockFileSystemModel files)
            {
                if (files == null)
                {
                    throw new ArgumentNullException(nameof(files));
                }

                File = new FileMock(files);
                Directory = new DirectoryMock(files);
            }

            public IFile File { get; }

            public IDirectory Directory { get; }
        }

        // facade
        private class FileMock : IFile
        {
            private readonly MockFileSystemModel _files;

            public FileMock(MockFileSystemModel files)
            {
                _files = files ?? throw new ArgumentNullException(nameof(files));
            }

            public bool Exists(string path)
            {
                if (path == null)
                {
                    throw new ArgumentNullException(nameof(path));
                }

                if (_files.TryGetNodeParent(path, out DirectoryNode current))
                {
                    PathModel pathModel = new(path);
                    return (current.Subs.TryGetValue(pathModel.FileOrDirectoryName(), out var node)
                            && node is FileNode);
                }

                return false;
            }

            public string ReadAllText(string path)
            {
                if (path == null)
                {
                    throw new ArgumentNullException(nameof(path));
                }

                if (_files.TryGetNodeParent(path, out DirectoryNode current) && current != null)
                {
                    PathModel pathModel = new(path);
                    if (current.Subs.TryGetValue(pathModel.FileOrDirectoryName(), out var node))
                    {
                        if (node is FileNode fileNode)
                        {
                            return fileNode.Content;
                        }
                        else
                        {
                            throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.");
                        }
                    }
                }

                throw new FileNotFoundException($"Could not find file '{path}'");
            }

            public Stream OpenRead(string path)
            {
                if (path == null)
                {
                    throw new ArgumentNullException(nameof(path));
                }

                return new MemoryStream(Encoding.UTF8.GetBytes(ReadAllText(path)));
            }

            public Stream OpenFile(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare,
                int bufferSize,
                FileOptions fileOptions)
            {
                if (fileMode == FileMode.Open && fileAccess == FileAccess.Read)
                {
                    return OpenRead(path);
                }

                throw new NotImplementedException();
            }

            public void CreateEmptyFile(string path)
            {
                if (path == null)
                {
                    throw new ArgumentNullException(nameof(path));
                }

                _files.CreateFile(path, string.Empty);
            }

            public void WriteAllText(string path, string content)
            {
                if (path == null)
                {
                    throw new ArgumentNullException(nameof(path));
                }

                if (content == null)
                {
                    throw new ArgumentNullException(nameof(content));
                }

                _files.CreateFile(path, content);
            }

            public void Move(string source, string destination)
            {
                if (source == null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                if (destination == null)
                {
                    throw new ArgumentNullException(nameof(destination));
                }

                (DirectoryNode sourceParent, FileNode sourceFileNode)
                    = _files.GetParentDirectoryAndFileNode(
                        source,
                        () => throw new FileNotFoundException($"Could not find file '{source}'"));

                if (_files.TryGetNodeParent(destination, out DirectoryNode current) && current != null)
                {
                    sourceFileNode = (FileNode)current.Subs.GetOrAdd(new PathModel(destination).FileOrDirectoryName(), sourceFileNode);
                    sourceParent.Subs.TryRemove(new PathModel(source).FileOrDirectoryName(), out _);
                }
                else
                {
                    throw new DirectoryNotFoundException($"Could not find a part of the path {destination}");
                }
            }

            public void Copy(string source, string destination)
            {
                if (source == null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                if (destination == null)
                {
                    throw new ArgumentNullException(nameof(destination));
                }

                (_, FileNode sourceFileNode) = _files.GetParentDirectoryAndFileNode(source,
                    () => throw new UnauthorizedAccessException($"Access to the path {source} is denied")
                );

                if (_files.TryGetNodeParent(destination, out DirectoryNode current) && current != null)
                {
                    if (current.Subs.ContainsKey(new PathModel(destination).FileOrDirectoryName()))
                    {
                        throw new IOException($"Path {destination} already exists");
                    }

                    current.Subs.TryAdd(new PathModel(destination).FileOrDirectoryName(),
                        new FileNode(sourceFileNode.Content));
                }
                else
                {
                    throw new DirectoryNotFoundException($"Could not find a part of the path {destination}");
                }
            }

            public void Delete(string path)
            {
                if (_files.TryGetNodeParent(path, out DirectoryNode current))
                {
                    PathModel pathModel = new(path);
                    current.Subs.TryRemove(pathModel.FileOrDirectoryName(), out _);
                }
                else
                {
                    throw new DirectoryNotFoundException($"Could not find a part of the path {path}");
                }
            }
        }

        // facade
        private class DirectoryMock : IDirectory
        {
            private readonly MockFileSystemModel _files;

            public DirectoryMock(MockFileSystemModel files)
            {
                if (files != null)
                {
                    _files = files;
                }
            }

            public bool Exists(string path)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));

                if (_files.TryGetNodeParent(path, out DirectoryNode current))
                {
                    PathModel pathModel = new(path);

                    return current.Subs.TryGetValue(pathModel.FileOrDirectoryName(), out var node)
                           && node is DirectoryNode;
                }

                return false;
            }

            public ITemporaryDirectory CreateTemporaryDirectory()
            {
                TemporaryDirectoryMock temporaryDirectoryMock = new(_files.TemporaryFolder);
                CreateDirectory(temporaryDirectoryMock.DirectoryPath);
                return temporaryDirectoryMock;
            }

            public IEnumerable<string> EnumerateDirectories(string path)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));

                return _files.EnumerateDirectory(path,
                    subs => subs.Where(s => s.Value is DirectoryNode)
                        .Select(s => Path.Combine(path, s.Key)));
            }

            public IEnumerable<string> EnumerateFiles(string path)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));

                return _files.EnumerateDirectory(path,
                    subs => subs.Where(s => s.Value is FileNode)
                        .Select(s => Path.Combine(path, s.Key)));
            }

            public IEnumerable<string> EnumerateFileSystemEntries(string path)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));

                return _files.EnumerateDirectory(path,
                    subs => subs.Select(s => Path.Combine(path, s.Key)));
            }

            public string GetCurrentDirectory()
            {
                return _files.WorkingDirectory;
            }

            public void CreateDirectory(string path)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));

                _files.CreateDirectory(path);
            }

            public string CreateTemporarySubdirectory()
            {
                return CreateTemporaryDirectory().DirectoryPath;
            }

            public void Delete(string path, bool recursive)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));

                DirectoryNode parentOfPath = _files.GetParentOfDirectoryNode(path);
                PathModel pathModel = new(path);
                if (recursive)
                {
                    parentOfPath.Subs.TryRemove(pathModel.FileOrDirectoryName(), out _);
                }
                else
                {
                    if (EnumerateFiles(path).Any())
                    {
                        throw new IOException("Directory not empty");
                    }

                    parentOfPath.Subs.TryRemove(pathModel.FileOrDirectoryName(), out _);
                }
            }

            public void Move(string source, string destination)
            {
                if (source == null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                if (destination == null)
                {
                    throw new ArgumentNullException(nameof(destination));
                }

                DirectoryNode sourceParent
                    = _files.GetParentOfDirectoryNode(source);

                PathModel parentPathModel = new(source);

                IFileSystemTreeNode sourceNode = sourceParent.Subs[parentPathModel.FileOrDirectoryName()];

                if (_files.TryGetNodeParent(destination, out DirectoryNode current) && current != null)
                {
                    PathModel destinationPathModel = new(destination);

                    if (current.Subs.TryGetValue(destinationPathModel.FileOrDirectoryName(), out var node))
                    {
                        if (node == sourceNode)
                        {
                            throw new IOException("Source and destination path must be different");
                        }

                        throw new IOException($"Cannot create {destination} because a file or" +
                                              " directory with the same name already exists");
                    }

                    sourceNode = current.Subs.GetOrAdd(destinationPathModel.FileOrDirectoryName(), sourceNode);
                    sourceParent.Subs.TryRemove(parentPathModel.FileOrDirectoryName(), out _);
                }
                else
                {
                    throw new DirectoryNotFoundException($"Could not find a part of the path {destination}");
                }
            }
        }

        private interface IFileSystemTreeNode
        {
        }

        private class DirectoryNode : IFileSystemTreeNode
        {
            public ConcurrentDictionary<string, IFileSystemTreeNode> Subs { get; } =
                new ConcurrentDictionary<string, IFileSystemTreeNode>();
        }

        private class FileSystemRoot
        {
            // in Linux there is only one Node, and the name is empty
            public ConcurrentDictionary<string, DirectoryNode> Volume { get; } = new ConcurrentDictionary<string, DirectoryNode>();
        }

        private class FileNode : IFileSystemTreeNode
        {
            public FileNode(string content)
            {
                Content = content ?? throw new ArgumentNullException(nameof(content));
            }

            public string Content { get; }
        }

        private class TemporaryDirectoryMock : ITemporaryDirectoryMock
        {
            public TemporaryDirectoryMock(string temporaryDirectory)
            {
                DirectoryPath = temporaryDirectory;
            }

            public bool DisposedTemporaryDirectory { get; private set; }

            public string DirectoryPath { get; }

            public void Dispose()
            {
                DisposedTemporaryDirectory = true;
            }
        }
    }
}
