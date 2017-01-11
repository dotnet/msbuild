// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.New3.Tests
{
    public class New3TestBase : TestBase
    {
        private static readonly object InitializationSync = new object();
        private static bool _isInitialized;

        protected New3TestBase()
        {
            if (_isInitialized)
            {
                return;
            }

            lock (InitializationSync)
            {
                if (_isInitialized)
                {
                    return;
                }

                //Force any previously computed configuration to be cleared
                new TestCommand("dotnet").Execute("new3 --debug:reinit");
                _isInitialized = true;
            }
        }
    }
}
