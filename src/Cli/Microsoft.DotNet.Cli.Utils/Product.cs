// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Product
    {
        public static string LongName => LocalizableStrings.DotNetSdkInfo;
        public static readonly string Version = GetProductVersion();

        private static string GetProductVersion()
        {
            DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
            return versionFile.BuildNumber ??
                   System.Diagnostics.FileVersionInfo.GetVersionInfo(
                           typeof(Product).GetTypeInfo().Assembly.Location)
                       .ProductVersion ??
                   string.Empty;
        }
    }
}
