// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;

namespace Microsoft.DotNet.Configurer
{
    public class NoOpFirstTimeUseNoticeSentinel : IFirstTimeUseNoticeSentinel
    {
        public bool Exists()
        {
            return true;
        }

        public void CreateIfNotExists()
        {
        }

        public void Dispose()
        {
        }
    }
}
