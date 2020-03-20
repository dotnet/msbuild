// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
