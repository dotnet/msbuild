// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.NET.TestFramework.Assertions
{
    public static partial class FileInfoExtensions
    {
        public static FileInfoAssertions Should(this FileInfo file)
        {
            return new FileInfoAssertions(file);
        }

        public static IDisposable Lock(this FileInfo subject)
        {
            return new FileInfoLock(subject);
        }

        public static IDisposable NuGetLock(this FileInfo subject)
        {
            return new FileInfoNuGetLock(subject);
        }

        public static string ReadAllText(this FileInfo subject)
        {
            return File.ReadAllText(subject.FullName);
        }
    }
}
