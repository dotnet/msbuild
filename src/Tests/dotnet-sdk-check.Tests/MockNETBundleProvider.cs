// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
