// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class TempDirectory : IDisposable
    {
        public const int MaxNameLength = 255;

        /// <summary>Gets the created directory's path.</summary>
        public string DirPath { get; private set; }

        /// <summary>
        /// Construct a random temp directory in the temp folder.
        /// </summary>
        public TempDirectory()
            : this(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()))
        {
        }

        public TempDirectory(string path)
        {
            DirPath = path;
            Directory.CreateDirectory(path);
        }

        ~TempDirectory() { DeleteDirectory(); }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            DeleteDirectory();
        }

        public string GenerateRandomFilePath() => Path.Combine(DirPath, Path.GetRandomFileName());

        protected virtual void DeleteDirectory()
        {
            try { Directory.Delete(DirPath, recursive: true); }
            catch { /* Ignore exceptions on disposal paths */ }
        }

        /// <summary>
        /// Generates a string with 255 random valid filename characters.
        /// 255 is the max file/folder name length in NTFS and FAT32:
        // https://docs.microsoft.com/en-us/windows/win32/fileio/filesystem-functionality-comparison?redirectedfrom=MSDN#limits
        /// </summary>
        /// <returns>A 255 length string with random valid filename characters.</returns>
        public static string GetMaxLengthRandomName()
        {
            string guid = Guid.NewGuid().ToString("N");
            return guid + new string('x', 255 - guid.Length);
        }
    }
}
