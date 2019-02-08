// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class DotnetUnderTest
    {
        static string _pathToDotnetUnderTest;

        public static string FullName
        {
            get
            {
                // This will hurt us when we try to publish and run tests
                // separately. Filled an issue against arcade to change
                // the CLI used to run the tests, at which point we can
                // revert this code to what it was before.
                // Issue: https://github.com/dotnet/arcade/issues/1207
                if (_pathToDotnetUnderTest == null)
                {
                    _pathToDotnetUnderTest = Path.Combine(
                        new RepoDirectoriesProvider().DotnetRoot,
                        $"dotnet{Constants.ExeSuffix}");
                }
                
                return _pathToDotnetUnderTest;
            }
        }

        public static bool IsLocalized()
        {
            for (var culture = CultureInfo.CurrentUICulture; !culture.Equals(CultureInfo.InvariantCulture); culture = culture.Parent)
            {
                if (culture.Name == "en")
                {
                    return false;
                }
            }

            return true;
        }
    }
}
