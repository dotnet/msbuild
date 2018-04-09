// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.DotNet.Configurer
{
    public class NoOpFileSentinel : IFileSentinel
    {
        public bool Exists()
        {
            return true;
        }

        public void Create()
        {
        }
    }
}
