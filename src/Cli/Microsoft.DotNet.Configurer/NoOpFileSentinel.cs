// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Configurer
{
    public class NoOpFileSentinel : IFileSentinel
    {
        private bool _exists;

        public NoOpFileSentinel(bool exists)
        {
            _exists = exists;
        }

        public bool Exists()
        {
            return _exists;
        }

        public void Create()
        {
        }
    }
}
