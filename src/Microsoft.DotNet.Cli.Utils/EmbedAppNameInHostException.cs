// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Cli.Utils
{
    public class EmbedAppNameInHostException : Exception
    {
        public EmbedAppNameInHostException()
        {
        }

        public EmbedAppNameInHostException(string message) : base(message)
        {
        }

        public EmbedAppNameInHostException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
