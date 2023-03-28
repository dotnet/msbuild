// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

#nullable disable

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Implementation of file system operations directly over the dot net managed layer
    /// </summary>
    internal class ManagedFileSystem : IFileSystem
    {
        private static readonly ManagedFileSystem Instance = new ManagedFileSystem();

        public static ManagedFileSystem Singleton() => ManagedFileSystem.Instance;

        private static bool ShouldUseMicrosoftIO
        {
            get
            {
#if !MICROSOFT_BUILD_ENGINE_OM_UNITTESTS
                return NativeMethodsShared.IsWindows;
#else
                // We need to mock usage of ChangeWaves class,
                // because Microsoft.Build.Engine.OM.UnitTests should not have access to internals of Microsoft.Build.Framework.
                return true;
#endif
            }
        }

        protected ManagedFileSystem() { }

        public TextReader ReadFile(string path)
        {
            return new StreamReader(path);
        }

        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return new FileStream(path, mode, access, share);
        }

        public string ReadFileAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public byte[] ReadFileAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

#if FEATURE_MSIOREDIST
        private static IEnumerable<string> HandleFileLoadException(
            Func<string, string, Microsoft.IO.SearchOption, IEnumerable<string>> enumerateFunctionDelegate,
            string path,
            string searchPattern,
            Microsoft.IO.SearchOption searchOption)
        {
            try
            {
                return enumerateFunctionDelegate(path, searchPattern, searchOption);
            }
            // Microsoft.IO.Redist has a dependency on System.Buffers and if System.Buffers assembly is not found the line above throws an exception.
            // However, FileMatcher class (that in most cases calls the enumeration) does not allow to fail on a IO-related exception. Such behavior hides the actual exception and makes it obscure.
            // We rethrow it to make it fail with a proper error message and call stack.
            catch (FileLoadException ex)
            {
                throw new InvalidOperationException(ex.Message, ex);
            }
            // Sometimes FileNotFoundException is thrown when there is an assembly load failure. In this case it should have FusionLog.
            catch (FileNotFoundException ex) when (ex.FusionLog != null)
            {
                throw new InvalidOperationException(ex.Message, ex);
            }
        }
#endif

        public virtual IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
#if FEATURE_MSIOREDIST
            return ShouldUseMicrosoftIO
                ? HandleFileLoadException(
                    (path, searchPattern, searchOption) => Microsoft.IO.Directory.EnumerateFiles(path, searchPattern, searchOption),
                    path,
                    searchPattern,
                    (Microsoft.IO.SearchOption)searchOption)
                : Directory.EnumerateFiles(path, searchPattern, searchOption);
#else
            return Directory.EnumerateFiles(path, searchPattern, searchOption);
#endif
        }

        public virtual IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
#if FEATURE_MSIOREDIST
            return ShouldUseMicrosoftIO
                ? HandleFileLoadException(
                    (path, searchPattern, searchOption) => Microsoft.IO.Directory.EnumerateDirectories(path, searchPattern, searchOption),
                    path,
                    searchPattern,
                    (Microsoft.IO.SearchOption)searchOption)
                : Directory.EnumerateDirectories(path, searchPattern, searchOption);
#else
            return Directory.EnumerateDirectories(path, searchPattern, searchOption);
#endif
        }

        public virtual IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
#if FEATURE_MSIOREDIST
            return ShouldUseMicrosoftIO
                ? HandleFileLoadException(
                    (path, searchPattern, searchOption) => Microsoft.IO.Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption),
                    path,
                    searchPattern, (Microsoft.IO.SearchOption)searchOption)
                : Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption);
#else
            return Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption);
#endif
        }

        public FileAttributes GetAttributes(string path)
        {
            return File.GetAttributes(path);
        }

        public virtual DateTime GetLastWriteTimeUtc(string path)
        {
            return File.GetLastWriteTimeUtc(path);
        }

        public virtual bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public virtual bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public virtual bool FileOrDirectoryExists(string path)
        {
            return FileExists(path) || DirectoryExists(path);
        }
    }
}
