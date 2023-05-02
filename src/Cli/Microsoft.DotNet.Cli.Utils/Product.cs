// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
