// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
