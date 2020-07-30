// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.TemplateEngine.Cli
{
    public sealed class HiveSynchronizationException : Exception
    {
        public HiveSynchronizationException(string message, string version)
            : base(message)
        {
            SdkVersion = version;
        }

        public HiveSynchronizationException(string message, string version, Exception innerException)
            : base(message, innerException)
        {
            SdkVersion = version;
        }

        public string SdkVersion { get; }
    }
}
