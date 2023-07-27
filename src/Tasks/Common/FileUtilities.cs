// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

using System.Diagnostics;

namespace Microsoft.NET.Build.Tasks
{
    static partial class FileUtilities
    {
        public static Version GetFileVersion(string sourcePath)
        {
            if (sourcePath != null)
            {
                var fvi = FileVersionInfo.GetVersionInfo(sourcePath);

                if (fvi != null)
                {
                    return new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
                }
            }

            return null;
        }

        static readonly HashSet<string> s_assemblyExtensions = new HashSet<string>(new[] { ".dll", ".exe", ".winmd" }, StringComparer.OrdinalIgnoreCase);
        public static Version TryGetAssemblyVersion(string sourcePath)
        {
            var extension = Path.GetExtension(sourcePath);

            return s_assemblyExtensions.Contains(extension) ? GetAssemblyVersion(sourcePath) : null;
        }
    }
}
