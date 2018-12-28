// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Globalization;
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
                if (_pathToDotnetUnderTest == null)
                {
                    _pathToDotnetUnderTest = new Muxer().MuxerPath;
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
