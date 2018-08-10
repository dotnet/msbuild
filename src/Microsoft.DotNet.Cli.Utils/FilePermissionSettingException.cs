// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.Cli.Utils
{
    [Serializable]
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

        protected FilePermissionSettingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
