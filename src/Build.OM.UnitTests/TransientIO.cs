// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            if (this.root == null)
            {
                this.root = new DirectoryInfo(
                    this.Parent != null ?
                          this.Parent.GetAbsolutePath(this.SubFolder)
                        : FileUtilities.GetTemporaryDirectory(true)
                );
            }

            return this.root;
        }

        private static bool IsDirSlash(char c) => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        public TransientIO()
        {
        }

        private TransientIO(TransientIO parent, string subFolder)
        {
            this.SubFolder = subFolder;
            this.Parent = parent;
        }

        public bool IsControled(string path)
        {
            if (this.root == null || path == null) return false;
            var tempRoot = this.RootFolder;
            path = Path.GetFullPath(path);
            return path != null && tempRoot != null
                && path.Length > tempRoot.Length
                && IsDirSlash(path[tempRoot.Length])
                && path.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase);
        }

        public string RootFolder => EnsureTempRoot().FullName;

        public void EnsureFileLocation(string path)
        {
            var absolute = this.GetAbsolutePath(path);
            var parent = Path.GetDirectoryName(absolute);
            if (!Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }

        public string GetRelativePath(string path)
        {
            var absolute = GetAbsolutePath(path);

            return absolute.Substring(this.RootFolder.Length + 1);
        }
        public string GetAbsolutePath(string relative)
        {
            var tempRoot = this.RootFolder;

            var absolute = Path.GetFullPath(Path.IsPathRooted(relative) ? relative : Path.Combine(tempRoot, relative));
            if (!IsControled(absolute))
            {
                throw new ArgumentException(nameof(relative));
            }

            return absolute;
        }

        public TransientIO GetSubFolder(string path)
        {
            var subFolder = this.GetRelativePath(path);
            if (!this.Children.TryGetValue(subFolder, out var result))
            {

                result  = new TransientIO(this, subFolder);
                this.Children.Add(subFolder, result);
            }

            return result;
        }

        public string WriteProjectFile(string path, string content)
        {
            var absolute = this.GetAbsolutePath(path);
            content = ObjectModelHelpers.CleanupFileContents(content);
            this.EnsureFileLocation(absolute);
            File.WriteAllText(absolute, content);
            return absolute;
        }

        public void Clear()
        {
            if (this.root != null && Directory.Exists(this.root.FullName))
            {
                // Note: FileUtilities.DeleteDirectoryNoThrow will be very slow if the directory does not exists. (it will retry with timeout in this case for ~0.5 sec).
                // not sure if that was intentional, so have not fixed it there but instead we check exist here.
                FileUtilities.DeleteDirectoryNoThrow(this.root.FullName, true);
            }

            Reset();
        }

        private void Reset()
        {
            this.root = null;
            foreach (var child in this.Children.Values)
            {
                child.Reset();
            }
        }

        public void Dispose()
        {
            this.Clear();
            // this object still can be used ...
        }

    }
}
