// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Product
    {
        public static string LongName => LocalizableStrings.DotNetCommandLineTools;
        public static readonly string Version = GetProductVersion();

        private static string GetProductVersion()
        {
            var attr = typeof(Product)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attr?.InformationalVersion;
        }
    }
}
