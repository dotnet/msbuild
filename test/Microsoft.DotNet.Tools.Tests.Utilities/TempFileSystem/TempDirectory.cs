// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
 
namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class TempDirectory
    {
        private readonly string _path;
        private readonly TempRoot _root;
 
        protected TempDirectory(TempRoot root)
            : this(CreateUniqueDirectory(TempRoot.Root), root)
        {
        }
 
        private TempDirectory(string path, TempRoot root)
        {
            Debug.Assert(path != null);
            Debug.Assert(root != null);
 
            _path = path;
            _root = root;
        }
 
        private static string CreateUniqueDirectory(string basePath)
        {
            while (true)
            {
                string dir = System.IO.Path.Combine(basePath, Guid.NewGuid().ToString());
                try
                {
                    Directory.CreateDirectory(dir);
                    return dir;
                }
                catch (IOException)
                {
                    // retry
                }
            }
        }
 
        public string Path
        {
            get { return _path; }
        }
 
        public DirectoryInfo DirectoryInfo => new DirectoryInfo(Path);

        /// <summary>
        /// Creates a file in this directory.
        /// </summary>
        /// <param name="name">File name.</param>
        public TempFile CreateFile(string name)
        {
            string filePath = System.IO.Path.Combine(_path, name);
            TempRoot.CreateStream(filePath);
            return _root.AddFile(new DisposableFile(filePath));
        }
 
        /// <summary>
        /// Creates a file in this directory that is a copy of the specified file.
        /// </summary>
        public TempFile CopyFile(string originalPath)
        {
            string name = System.IO.Path.GetFileName(originalPath);
            string filePath = System.IO.Path.Combine(_path, name);
            File.Copy(originalPath, filePath);
            return _root.AddFile(new DisposableFile(filePath));
        }

        /// <summary>
        /// Recursively copy the provided directory into this TempDirectory.
        /// Does not handle links.
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <returns></returns>
        public TempDirectory CopyDirectory(string sourceDirectory)
        {
            Debug.Assert(Directory.Exists(sourceDirectory), $"{sourceDirectory} does not exists");
             
            var tempCopy = CreateDirectory(new DirectoryInfo(sourceDirectory).Name);

            foreach(var file in Directory.EnumerateFiles(sourceDirectory))
            {
                tempCopy.CopyFile(file);
            }

            foreach(var directory in Directory.EnumerateDirectories(sourceDirectory))
            {
                tempCopy.CopyDirectory(directory);
            }

            return tempCopy;
        }
 
        /// <summary>
        /// Creates a subdirectory in this directory.
        /// </summary>
        /// <param name="name">Directory name or unrooted directory path.</param>
        public TempDirectory CreateDirectory(string name)
        {
            string dirPath = System.IO.Path.Combine(_path, name);
            Directory.CreateDirectory(dirPath);
            return new TempDirectory(dirPath, _root);
        }

        public void SetCurrentDirectory()
        {
            Directory.SetCurrentDirectory(_path);
        }
 
        public override string ToString()
        {
            return _path;
        }
    }
}
