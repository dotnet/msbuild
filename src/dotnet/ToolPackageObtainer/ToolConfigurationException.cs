// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ToolPackageObtainer
{
    internal class ToolConfigurationException : ArgumentException
    {
        public ToolConfigurationException(string message) : base(message)
        {
        }
    }
}
