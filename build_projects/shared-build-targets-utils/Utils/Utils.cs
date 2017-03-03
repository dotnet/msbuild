// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public static class Utils
    {
        public static string GetVersionFileContent(string commitHash, string version)
        {
            return $@"{commitHash}{Environment.NewLine}{version}{Environment.NewLine}";
        }
    }
}
