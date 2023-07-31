// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
