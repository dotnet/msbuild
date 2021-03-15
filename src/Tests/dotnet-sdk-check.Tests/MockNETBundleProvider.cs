// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Cli.SdkCheck.Tests
{
    public class MockNETBundleProvider : INETBundleProvider
    {
        private NetEnvironmentInfo _environmentInfo;

        public MockNETBundleProvider(NetEnvironmentInfo environmentInfo)
        {
            _environmentInfo = environmentInfo;
        }

        public NetEnvironmentInfo GetDotnetEnvironmentInfo(string dotnetDir)
        {
            return _environmentInfo;
        }
    }
}
