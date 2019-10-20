// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class DirectoryInfoExtensions
    {
        public static DirectoryInfoAssertions Should(this DirectoryInfo dir)
        {
            return new DirectoryInfoAssertions(dir);
        }

        public static DirectoryInfo Sub(this DirectoryInfo dir, string name)
        {
            return new DirectoryInfo(Path.Combine(dir.FullName, name));
        }

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
    }
}
