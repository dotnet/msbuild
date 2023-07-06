// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.NugetSearch
{
    internal class NugetSearchApiRequestException : GracefulException
    {
        public NugetSearchApiRequestException(string message)
            : base(new[] {message}, null, false)
        {
        }
    }
}
