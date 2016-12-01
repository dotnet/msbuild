// 
// FilePath.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2011 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;

namespace Microsoft.DotNet.Cli.Sln.Internal.FileManipulation
{
    public struct FilePath : IComparable<FilePath>, IComparable, IEquatable<FilePath>
    {
        public static readonly StringComparer PathComparer = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        public static readonly StringComparison PathComparison = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        readonly string fileName;

        public static readonly FilePath Null = new FilePath(null);
        public static readonly FilePath Empty = new FilePath(string.Empty);

        public FilePath(string name)
        {
            if (name != null && name.Length > 6 && name[0] == 'f' && name.StartsWith("file://", StringComparison.Ordinal))
                name = new Uri(name).LocalPath;

            fileName = name;
        }

        public bool IsNull
        {
            get { return fileName == null; }
        }

        public bool IsNullOrEmpty
        {
            get { return string.IsNullOrEmpty(fileName); }
        }

        public bool IsNotNull
        {
            get { return fileName != null; }
        }

        public bool IsEmpty
        {
            get { return fileName != null && fileName.Length == 0; }
        }

        const int PATHMAX = 4096 + 1;

        static readonly char[] invalidPathChars = Path.GetInvalidPathChars();
        public static char[] GetInvalidPathChars()
        {
            return (char[])invalidPathChars.Clone();
        }

        static readonly char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        public static char[] GetInvalidFileNameChars()
        {
            return (char[])invalidFileNameChars.Clone();
        }

        public FilePath FullPath
        {
            get
            {
                return new FilePath(!string.IsNullOrEmpty(fileName) ? Path.GetFullPath(fileName) : "");
            }
        }

        public bool IsDirectory
        {
            get
            {
                return Directory.Exists(FullPath);
            }
        }

        /// <summary>
        /// Returns a path in standard form, which can be used to be compared
        /// for equality with other canonical paths. It is similar to FullPath,
        /// but unlike FullPath, the directory "/a/b" is considered equal to "/a/b/"
        /// </summary>
        public FilePath CanonicalPath
        {
            get
            {
                if (fileName == null)
                    return FilePath.Null;
                if (fileName.Length == 0)
                    return FilePath.Empty;
                string fp = Path.GetFullPath(fileName);
                if (fp.Length > 0)
                {
                    if (fp[fp.Length - 1] == Path.DirectorySeparatorChar)
                        return fp.TrimEnd(Path.DirectorySeparatorChar);
                    if (fp[fp.Length - 1] == Path.AltDirectorySeparatorChar)
                        return fp.TrimEnd(Path.AltDirectorySeparatorChar);
                }
                return fp;
            }
        }

        public string FileName
        {
            get
            {
                return Path.GetFileName(fileName);
            }
        }

        public string Extension
        {
            get
            {
                return Path.GetExtension(fileName);
            }
        }

        public bool HasExtension(string extension)
        {
            return fileName.Length > extension.Length
                && fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                && fileName[fileName.Length - extension.Length - 1] != Path.PathSeparator;
        }

        public string FileNameWithoutExtension
        {
            get
            {
                return Path.GetFileNameWithoutExtension(fileName);
            }
        }

        public FilePath ParentDirectory
        {
            get
            {
                return new FilePath(Path.GetDirectoryName(fileName));
            }
        }

        public bool IsAbsolute
        {
            get { return Path.IsPathRooted(fileName); }
        }

        public bool IsChildPathOf(FilePath basePath)
        {
            if (basePath.fileName[basePath.fileName.Length - 1] != Path.DirectorySeparatorChar)
                return fileName.StartsWith(basePath.fileName + Path.DirectorySeparatorChar, PathComparison);
            else
                return fileName.StartsWith(basePath.fileName, PathComparison);
        }

        public FilePath ChangeExtension(string ext)
        {
            return Path.ChangeExtension(fileName, ext);
        }

        /// <summary>
        /// Returns a file path with the name changed to the provided name, but keeping the extension
        /// </summary>
        /// <returns>The new file path</returns>
        /// <param name="newName">New file name</param>
        public FilePath ChangeName(string newName)
        {
            return ParentDirectory.Combine(newName) + Extension;
        }

        public FilePath Combine(params FilePath[] paths)
        {
            string path = fileName;
            foreach (FilePath p in paths)
                path = Path.Combine(path, p.fileName);
            return new FilePath(path);
        }

        public FilePath Combine(params string[] paths)
        {
            return new FilePath(Path.Combine(fileName, Path.Combine(paths)));
        }

        public Task DeleteAsync()
        {
            return Task.Run((System.Action)Delete);
        }

        public void Delete()
        {
            // Ensure that this file/directory and all children are writable
            MakeWritable(true);

            // Also ensure the directory containing this file/directory is writable,
            // otherwise we will not be able to delete it
            ParentDirectory.MakeWritable(false);

            if (Directory.Exists(this))
            {
                Directory.Delete(this, true);
            }
            else if (File.Exists(this))
            {
                File.Delete(this);
            }
        }

        public void MakeWritable()
        {
            MakeWritable(false);
        }

        public void MakeWritable(bool recurse)
        {
            if (Directory.Exists(this))
            {
                try
                {
                    var info = new DirectoryInfo(this);
                    info.Attributes &= ~FileAttributes.ReadOnly;
                }
                catch
                {

                }

                if (recurse)
                {
                    foreach (var sub in Directory.GetFileSystemEntries(this))
                    {
                        ((FilePath)sub).MakeWritable(recurse);
                    }
                }
            }
            else if (File.Exists(this))
            {
                try
                {
                    // Try/catch is to work around a mono bug where dangling symlinks
                    // blow up when you call SetFileAttributes. Just ignore this case
                    // until mono 2.10.7/8 is released which fixes it.
                    var info = new FileInfo(this);
                    info.Attributes &= ~FileAttributes.ReadOnly;
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// Builds a path by combining all provided path sections
        /// </summary>
        public static FilePath Build(params string[] paths)
        {
            return Empty.Combine(paths);
        }

        public static FilePath GetCommonRootPath(IEnumerable<FilePath> paths)
        {
            FilePath root = FilePath.Null;
            foreach (FilePath p in paths)
            {
                if (root.IsNull)
                    root = p;
                else if (root == p)
                    continue;
                else if (root.IsChildPathOf(p))
                    root = p;
                else
                {
                    while (!root.IsNullOrEmpty && !p.IsChildPathOf(root))
                        root = root.ParentDirectory;
                }
            }
            return root;
        }

        public FilePath ToAbsolute(FilePath basePath)
        {
            if (IsAbsolute)
                return FullPath;
            else
                return Combine(basePath, this).FullPath;
        }

        public static implicit operator FilePath(string name)
        {
            return new FilePath(name);
        }

        public static implicit operator string(FilePath filePath)
        {
            return filePath.fileName;
        }

        public static bool operator ==(FilePath name1, FilePath name2)
        {
            return PathComparer.Equals(name1.fileName, name2.fileName);
        }

        public static bool operator !=(FilePath name1, FilePath name2)
        {
            return !(name1 == name2);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is FilePath))
                return false;

            FilePath fn = (FilePath)obj;
            return this == fn;
        }

        public override int GetHashCode()
        {
            if (fileName == null)
                return 0;
            return PathComparer.GetHashCode(fileName);
        }

        public override string ToString()
        {
            return fileName;
        }

        public int CompareTo(FilePath filePath)
        {
            return PathComparer.Compare(fileName, filePath.fileName);
        }

        int IComparable.CompareTo(object obj)
        {
            if (!(obj is FilePath))
                return -1;
            return CompareTo((FilePath)obj);
        }

        #region IEquatable<FilePath> Members

        bool IEquatable<FilePath>.Equals(FilePath other)
        {
            return this == other;
        }

        #endregion
    }

    public static class FilePathUtil
    {
        public static string[] ToStringArray(this FilePath[] paths)
        {
            string[] array = new string[paths.Length];
            for (int n = 0; n < paths.Length; n++)
                array[n] = paths[n].ToString();
            return array;
        }

        public static FilePath[] ToFilePathArray(this string[] paths)
        {
            var array = new FilePath[paths.Length];
            for (int n = 0; n < paths.Length; n++)
                array[n] = paths[n];
            return array;
        }

        public static IEnumerable<string> ToPathStrings(this IEnumerable<FilePath> paths)
        {
            foreach (FilePath p in paths)
                yield return p.ToString();
        }
    }
}
