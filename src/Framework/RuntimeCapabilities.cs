// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipes;

namespace Microsoft.Build.Framework;

/// <summary>
/// What the runtime of the current process provides. These checks matter when the .NET Framework
/// flavor of MSBuild is loaded into a .NET process, for example by an IDE that loads MSBuild.exe's
/// assemblies with MSBuildLocator. See documentation/NETFramework-MSBuild-on-NET.md.
/// </summary>
/// <remarks>
/// On .NET, a method that references a type or member that the runtime does not have throws
/// <see cref="TypeLoadException"/> or <see cref="MissingMethodException"/> when the method is
/// compiled, before any of its code runs, even if the reference is in a branch that is not taken.
/// Such references are kept in separate methods marked
/// <see cref="System.Runtime.CompilerServices.MethodImplOptions.NoInlining"/> (see
/// NetFrameworkApis), and the checks below decide whether those methods are called.
/// Each check tests for the member itself instead of the runtime version.
/// </remarks>
internal static class RuntimeCapabilities
{
    /// <summary>
    /// The value of PipeOptions.CurrentUserOnly, which the .NET Framework reference assemblies do
    /// not define. See https://learn.microsoft.com/dotnet/api/system.io.pipes.pipeoptions.
    /// </summary>
    internal const PipeOptions PipeOptionsCurrentUserOnly = (PipeOptions)0x20000000;

    /// <summary>
    /// True when the runtime recognizes <see cref="PipeOptionsCurrentUserOnly"/>: on .NET, not on
    /// .NET Framework. With the option set, NamedPipeClientStream.Connect checks that the server
    /// end of the pipe is owned by the current user. That is the check MSBuild otherwise makes with
    /// PipeStream.GetAccessControl(), a method .NET does not have. When the runtime does not
    /// recognize the option, the result is false and the GetAccessControl check is made.
    /// </summary>
    internal static readonly bool SupportsPipeOptionsCurrentUserOnly = Enum.IsDefined(typeof(PipeOptions), PipeOptionsCurrentUserOnly);

    /// <summary>
    /// <see cref="PipeOptionsCurrentUserOnly"/> when <see cref="SupportsPipeOptionsCurrentUserOnly"/>
    /// is true, otherwise <see cref="PipeOptions.None"/>.
    /// </summary>
    internal static PipeOptions CurrentUserOnlyIfSupported => SupportsPipeOptionsCurrentUserOnly ? PipeOptionsCurrentUserOnly : PipeOptions.None;

    /// <summary>
    /// True when Assembly.Load uses the CodeBase of an AssemblyName to find the file: on .NET
    /// Framework and Mono, whose core library is mscorlib. .NET ignores the CodeBase.
    /// </summary>
    internal static readonly bool AssemblyLoadUsesCodeBase = typeof(object).Assembly.GetName().Name == "mscorlib";

    /// <summary>
    /// True when AssemblyName.KeyPair can be read and set: on .NET Framework and Mono. On .NET both
    /// accessors throw <see cref="PlatformNotSupportedException"/>.
    /// </summary>
    internal static readonly bool SupportsAssemblyNameKeyPair = AssemblyLoadUsesCodeBase;

#if FEATURE_APPDOMAIN
    /// <summary>
    /// True when the runtime can create AppDomains with an AppDomainSetup and copy an
    /// AppDomainSetup through its configuration bytes: on .NET Framework, not on .NET.
    /// </summary>
    internal static readonly bool SupportsAppDomains =
        typeof(AppDomainSetup).GetMethod(nameof(AppDomainSetup.GetConfigurationBytes), Type.EmptyTypes) != null &&
        Array.Exists(
            typeof(AppDomain).GetMethods(),
            method => method.Name == nameof(AppDomain.CreateDomain) &&
                method.GetParameters() is { Length: 3 } parameters &&
                parameters[2].ParameterType == typeof(AppDomainSetup));

    /// <summary>
    /// True when .NET Remoting (System.Runtime.Remoting) is available: on .NET Framework, not on
    /// .NET. Without it no object is a remoting proxy and no lease sponsor is registered.
    /// </summary>
    internal static readonly bool SupportsRemoting =
        Type.GetType("System.Runtime.Remoting.RemotingServices, mscorlib", throwOnError: false) != null;
#endif
}
