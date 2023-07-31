// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;

namespace Microsoft.NET.TestFramework.Utilities
{
    public class FileThumbPrint : IEquatable<FileThumbPrint>
    {
        private FileThumbPrint(string path, DateTime lastWriteTimeUtc, string hash)
        {
            Path = path;
            LastWriteTimeUtc = lastWriteTimeUtc;
            Hash = hash;
        }

        public string Path { get; }

        public DateTime LastWriteTimeUtc { get; }

        public string Hash { get; }

        public static FileThumbPrint Create(string path)
        {
            byte[] hashBytes;
            using (var sha1 = SHA1.Create())
            using (var fileStream = File.OpenRead(path))
            {
                hashBytes = sha1.ComputeHash(fileStream);
            }

            var hash = Convert.ToBase64String(hashBytes);
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            return new FileThumbPrint(path, lastWriteTimeUtc, hash);
        }

        /// <summary>
        /// Returns a list of thumbprints for all files (recursive) in the specified directory, sorted by file paths.
        /// </summary>
        public static List<FileThumbPrint> CreateFolderThumbprint(TestAsset testAsset, string directoryPath, params string[] filesToIgnore)
        {
            directoryPath = System.IO.Path.Combine(testAsset.TestRoot, directoryPath);
            var files = Directory.GetFiles(directoryPath).Where(p => !filesToIgnore.Contains(p));
            var thumbprintLookup = new List<FileThumbPrint>();
            foreach (var file in files)
            {
                var thumbprint = Create(file);
                thumbprintLookup.Add(thumbprint);
            }

            thumbprintLookup.Sort(Comparer<FileThumbPrint>.Create((a, b) => StringComparer.Ordinal.Compare(a.Path, b.Path)));
            return thumbprintLookup;
        }

        public bool Equals(FileThumbPrint other)
        {
            return
                string.Equals(Path, other.Path, StringComparison.Ordinal) &&
                LastWriteTimeUtc == other.LastWriteTimeUtc &&
                string.Equals(Hash, other.Hash, StringComparison.Ordinal);
        }

        public override int GetHashCode() => LastWriteTimeUtc.GetHashCode();
    }
}
