// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Cli.Utils.ExceptionExtensions
{
    internal static class ExceptionExtensions
    {
        public static void ReportAsWarning(this Exception e)
        {
            Reporter.Verbose.WriteLine($"Warning: Ignoring exception: {e.ToString().Yellow()}");
        }
    }
}
