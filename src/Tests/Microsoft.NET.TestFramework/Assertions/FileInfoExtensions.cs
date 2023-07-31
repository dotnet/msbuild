// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Assertions
{
    public static partial class FileInfoExtensions
    {
        public static FileInfoAssertions Should(this FileInfo file)
        {
            return new FileInfoAssertions(file);
        }

        public static AssemblyAssertions AssemblyShould(this FileInfo file)
        {
            return new AssemblyAssertions(file);
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
