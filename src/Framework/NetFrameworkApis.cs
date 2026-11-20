// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;

namespace Microsoft.Build.Framework;

/// <summary>
/// Calls to .NET Framework APIs that .NET does not have, for code that also runs when the .NET
/// Framework flavor of MSBuild is loaded into a .NET process. Each API is referenced only from a
/// method marked <see cref="MethodImplOptions.NoInlining"/>, which is called only when
/// <see cref="RuntimeCapabilities"/> reports the API as available, so that the callers can be
/// compiled on .NET. See the remarks on <see cref="RuntimeCapabilities"/>.
/// </summary>
internal static class NetFrameworkApis
{
    /// <summary>
    /// RemotingServices.IsTransparentProxy, or false when remoting is not available.
    /// </summary>
    internal static bool IsTransparentProxy(object? value) => RuntimeCapabilities.SupportsRemoting && IsTransparentProxyCore(value);

    /// <summary>
    /// RemotingServices.Disconnect, or nothing when remoting is not available.
    /// </summary>
    internal static void Disconnect(MarshalByRefObject value)
    {
        if (RuntimeCapabilities.SupportsRemoting)
        {
            DisconnectCore(value);
        }
    }

    /// <summary>
    /// The configuration bytes of <paramref name="appDomainSetup"/> to send to another process,
    /// or null when there is no setup or the runtime has no configuration bytes.
    /// </summary>
    internal static byte[]? GetConfigurationBytes(AppDomainSetup? appDomainSetup)
        => appDomainSetup != null && RuntimeCapabilities.SupportsAppDomains ? GetConfigurationBytesCore(appDomainSetup) : null;

    /// <summary>
    /// A new AppDomainSetup with the configuration bytes received from another process, or null
    /// when the runtime has no configuration bytes. A process on such a runtime is never a node
    /// or a task host, so it does not receive these packets.
    /// </summary>
    internal static AppDomainSetup? CreateAppDomainSetup(byte[]? configurationBytes)
        => RuntimeCapabilities.SupportsAppDomains ? CreateAppDomainSetupCore(configurationBytes) : null;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsTransparentProxyCore(object? value) => RemotingServices.IsTransparentProxy(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DisconnectCore(MarshalByRefObject value) => RemotingServices.Disconnect(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static byte[] GetConfigurationBytesCore(AppDomainSetup appDomainSetup) => appDomainSetup.GetConfigurationBytes();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static AppDomainSetup CreateAppDomainSetupCore(byte[]? configurationBytes)
    {
        var appDomainSetup = new AppDomainSetup();
        appDomainSetup.SetConfigurationBytes(configurationBytes);
        return appDomainSetup;
    }
}
#endif
