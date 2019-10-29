// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.DotNet.TestFramework
{
    internal static class DirectoryInfoExtensions
    {
        public static bool Contains(this DirectoryInfo subject, FileSystemInfo target)
        {
            return target.FullName.StartsWith(subject.FullName);
        }

        public static DirectoryInfo GetDirectory(this DirectoryInfo subject, params string [] directoryNames)
        {
            return new DirectoryInfo(Path.Combine(subject.FullName, Path.Combine(directoryNames)));
        }

        public static FileInfo GetFile(this DirectoryInfo subject, string fileName)
        {
            return new FileInfo(Path.Combine(subject.FullName, fileName));
        }

        public static void EnsureExistsAndEmpty(this DirectoryInfo subject)
        {
            if (subject.Exists)
            {
                subject.Delete(true);
            }

            subject.Create();
        }
    }
}
