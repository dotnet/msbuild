// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class ProcessStartInfoExtensions
    {
        public static int Execute(this ProcessStartInfo startInfo)
        {
            if (startInfo == null)
            {
                throw new ArgumentNullException(nameof(startInfo));
            }

            var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();
            process.WaitForExit();

            return process.ExitCode;
        }
    }
}
