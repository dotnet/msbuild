// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools
{
    internal class FilePermissionSetter : IFilePermissionSetter
    {
        public void SetUserExecutionPermission(string path)
        {
            RunCommand(path, "u+x");
        }

        /// <summary>
        /// Chmod 755 (chmod a+rwx,g-w,o-w) sets permissions so that, (U)ser / owner can read, can write and can execute.
        /// (G)roup can read, can't write and can execute.
        /// (O)thers can read, can't write and can execute.
        /// </summary>
        public void SetPermission(string path, string chmodArgument)
        {
            RunCommand(path, chmodArgument.ToString());
        }

        private static void RunCommand(string path, string chmodArgument)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            CommandResult result = new CommandFactory.CommandFactory()
                .Create("chmod", new[] {chmodArgument, path})
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute();

            if (result.ExitCode != 0)
            {
                throw new FilePermissionSettingException(result.StdErr);
            }
        }
    }
}
