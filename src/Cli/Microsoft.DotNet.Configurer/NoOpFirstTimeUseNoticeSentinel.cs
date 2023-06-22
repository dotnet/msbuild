// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
