// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.NativeWrapper
{
    public class NETBundlesNativeWrapper : INETBundleProvider
    {
        public NetEnvironmentInfo GetDotnetEnvironmentInfo(string dotnetExeDirectory)
        {
            var info = new NetEnvironmentInfo();
            IntPtr reserved = IntPtr.Zero;
            IntPtr resultContext = IntPtr.Zero;

            int errorCode = Interop.hostfxr_get_dotnet_environment_info(dotnetExeDirectory, reserved, info.Initialize, resultContext);

            return info;
        }
    }
}
