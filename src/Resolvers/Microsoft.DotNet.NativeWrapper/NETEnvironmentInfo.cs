// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using static Microsoft.DotNet.NativeWrapper.Interop;

namespace Microsoft.DotNet.NativeWrapper
{
    public interface INetBundleInfo
    {
        public ReleaseVersion Version { get; }

        public string Path { get; }
    }

    public sealed class NetSdkInfo : INetBundleInfo
    {
        public ReleaseVersion Version { get; private set; }

        public string Path { get; private set; }

        public NetSdkInfo(string version, string path)
        {
            Version = new ReleaseVersion(version);
            Path = path;
        }
    }

    public sealed class NetRuntimeInfo : INetBundleInfo
    {
        public ReleaseVersion Version { get; private set; }

        public string Path { get; private set; }

        public string Name { get; private set; }

        public NetRuntimeInfo(string name, string version, string path)
        {
            Version = new ReleaseVersion(version);
            Path = path;
            Name = name;
        }
    }

    public sealed class NetEnvironmentInfo
    {
        public IEnumerable<NetRuntimeInfo> RuntimeInfo { get; private set; }

        public IEnumerable<NetSdkInfo> SdkInfo { get; private set; }

        public NetEnvironmentInfo(IEnumerable<NetRuntimeInfo> runtimeInfo, IEnumerable<NetSdkInfo> sdkInfo)
        {
            RuntimeInfo = runtimeInfo;
            SdkInfo = sdkInfo;
        }

        public NetEnvironmentInfo()
        {
            RuntimeInfo = new List<NetRuntimeInfo>();
            SdkInfo = new List<NetSdkInfo>();
        }

        internal void Initialize(IntPtr info, IntPtr resultContext)
        {
            var infoStruct = Marshal.PtrToStructure<hostfxr_dotnet_environment_info>(info);
            var runtimes = new hostfxr_dotnet_environment_framework_info[infoStruct.framework_count];
            for (var i = 0; i < (int)infoStruct.framework_count; i++)
            {
                var pointer = new IntPtr(infoStruct.frameworks.ToInt64() + i * Marshal.SizeOf(typeof(hostfxr_dotnet_environment_framework_info)));
                runtimes[i] = Marshal.PtrToStructure<hostfxr_dotnet_environment_framework_info>(pointer);
            }
            RuntimeInfo = runtimes.Select(runtime => new NetRuntimeInfo(runtime.name, runtime.version, runtime.path));

            var sdks = new hostfxr_dotnet_environment_sdk_info[infoStruct.sdk_count];
            for (var i = 0; i < (int)infoStruct.sdk_count; i++)
            {
                var pointer = new IntPtr(infoStruct.sdks.ToInt64() + i * Marshal.SizeOf(typeof(hostfxr_dotnet_environment_sdk_info)));
                sdks[i] = Marshal.PtrToStructure<hostfxr_dotnet_environment_sdk_info>(pointer);
            }
            SdkInfo = sdks.Select(sdk => new NetSdkInfo(sdk.version, sdk.path));
        }
    }
}
