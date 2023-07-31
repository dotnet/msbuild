// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ToolPackage
{
    internal class ToolConfigurationException : Exception
    {
        public ToolConfigurationException()
        {
        }

        public ToolConfigurationException(string message) : base(message)
        {
        }

        public ToolConfigurationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
