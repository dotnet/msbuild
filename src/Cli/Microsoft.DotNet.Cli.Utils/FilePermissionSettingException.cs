// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class FilePermissionSettingException : Exception
    {
        public FilePermissionSettingException()
        {
        }

        public FilePermissionSettingException(string message) : base(message)
        {
        }

        public FilePermissionSettingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
