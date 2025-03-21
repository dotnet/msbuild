// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Build.Shared;

    public class TransientIO : IDisposable
    {
        private DirectoryInfo root;
        private TransientIO Parent { get; }
        private string SubFolder { get; }
        private Dictionary<string, TransientIO> Children = new Dictionary<string, TransientIO>(StringComparer.OrdinalIgnoreCase);

        private DirectoryInfo EnsureTempRoot()
        {
            if (root == null)
            {
                root = new DirectoryInfo(
                    Parent != null ?
                          Parent.GetAbsolutePath(SubFolder)
                        : FileUtilities.GetTemporaryDirectory(true));
            }

            return root;
        }

        private static bool IsDirSlash(char c) => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        public TransientIO()
        {
        }

        private TransientIO(TransientIO parent, string subFolder)
        {
            SubFolder = subFolder;
            Parent = parent;
        }

        public bool IsControled(string path)
        {
            if (root == null || path == null)
            {
                return false;
            }

            var tempRoot = RootFolder;
            path = Path.GetFullPath(path);
            return path != null && tempRoot != null
                && path.Length > tempRoot.Length
                && IsDirSlash(path[tempRoot.Length])
                && path.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase);
        }

        public string RootFolder => EnsureTempRoot().FullName;

        public void EnsureFileLocation(string path)
        {
            var absolute = GetAbsolutePath(path);
            var parent = Path.GetDirectoryName(absolute);
            if (!Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }

        public string GetRelativePath(string path)
        {
            var absolute = GetAbsolutePath(path);

            return absolute.Substring(RootFolder.Length + 1);
        }
        public string GetAbsolutePath(string relative)
        {
            var tempRoot = RootFolder;

            var absolute = Path.GetFullPath(Path.IsPathRooted(relative) ? relative : Path.Combine(tempRoot, relative));
            if (!IsControled(absolute))
            {
                throw new ArgumentException(nameof(relative));
            }

            return absolute;
        }

        public TransientIO GetSubFolder(string path)
        {
            var subFolder = GetRelativePath(path);
            if (!Children.TryGetValue(subFolder, out var result))
            {
                result = new TransientIO(this, subFolder);
                Children.Add(subFolder, result);
            }

            return result;
        }

        public string WriteProjectFile(string path, string content)
        {
            var absolute = GetAbsolutePath(path);
            content = ObjectModelHelpers.CleanupFileContents(content);
            EnsureFileLocation(absolute);
            File.WriteAllText(absolute, content);
            return absolute;
        }

        public void Clear()
        {
            if (root != null && Directory.Exists(root.FullName))
            {
                // Note: FileUtilities.DeleteDirectoryNoThrow will be very slow if the directory does not exists. (it will retry with timeout in this case for ~0.5 sec).
                // not sure if that was intentional, so have not fixed it there but instead we check exist here.
                FileUtilities.DeleteDirectoryNoThrow(root.FullName, true);
            }

            Reset();
        }

        private void Reset()
        {
            root = null;
            foreach (var child in Children.Values)
            {
                child.Reset();
            }
        }

        public void Dispose()
        {
            Clear();
            // this object still can be used ...
        }
    }
}
