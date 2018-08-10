// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Configurer
{
    public class NoOpAspNetCertificateSentinel : IAspNetCertificateSentinel
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
