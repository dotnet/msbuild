// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.TemplateEngine.Cli
{
    internal sealed class HiveSynchronizationException : Exception
    {
        internal HiveSynchronizationException(string message, string version)
            : base(message)
        {
            SdkVersion = version;
        }

        internal HiveSynchronizationException(string message, string version, Exception innerException)
            : base(message, innerException)
        {
            SdkVersion = version;
        }

        internal string SdkVersion { get; }
    }
}
