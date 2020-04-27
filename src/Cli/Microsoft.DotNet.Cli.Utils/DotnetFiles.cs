// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    internal static class DotnetFiles
    {
        private static string SdkRootFolder => Path.Combine(typeof(DotnetFiles).GetTypeInfo().Assembly.Location, "..");

        private static Lazy<DotnetVersionFile> s_versionFileObject =
            new Lazy<DotnetVersionFile>(() => new DotnetVersionFile(VersionFile));

        /// <summary>
        /// The SDK ships with a .version file that stores the commit information and SDK version
        /// </summary>
        public static string VersionFile => Path.GetFullPath(Path.Combine(SdkRootFolder, ".version"));

        internal static DotnetVersionFile VersionFileObject
        {
            get { return s_versionFileObject.Value; }
        }
    }
}
