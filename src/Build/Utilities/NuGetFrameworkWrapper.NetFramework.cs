// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

// .NET Framework dispatcher for NuGetFrameworkWrapper.
//
// Under Change Wave 18.9 (the default) MSBuild calls a copy of the NuGet.Frameworks source that is
// compiled directly into Microsoft.Build (see Utilities\NuGetFrameworks), avoiding both reflection
// and a secondary AppDomain. Opting out of the wave restores the historical behavior of loading
// NuGet.Frameworks.dll from the host SDK and invoking it by reflection in a dedicated AppDomain.
// See documentation/NETFramework-NGEN.md.
internal sealed partial class NuGetFrameworkWrapper
{
    private readonly INuGetFrameworkWrapper _implementation;

    private NuGetFrameworkWrapper(INuGetFrameworkWrapper implementation) => _implementation = implementation;

    public static NuGetFrameworkWrapper CreateInstance()
    {
        INuGetFrameworkWrapper implementation = ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_9)
            ? new NuGetFrameworkWrapperVendored()
            : NuGetFrameworkWrapperReflection.Create();

        return new NuGetFrameworkWrapper(implementation);
    }

    public string GetTargetFrameworkIdentifier(string tfm) => _implementation.GetTargetFrameworkIdentifier(tfm);

    public string GetTargetFrameworkVersion(string tfm, int minVersionPartCount) => _implementation.GetTargetFrameworkVersion(tfm, minVersionPartCount);

    public string GetTargetPlatformIdentifier(string tfm) => _implementation.GetTargetPlatformIdentifier(tfm);

    public string GetTargetPlatformVersion(string tfm, int minVersionPartCount) => _implementation.GetTargetPlatformVersion(tfm, minVersionPartCount);

    public bool IsCompatible(string target, string candidate) => _implementation.IsCompatible(target, candidate);

    public string FilterTargetFrameworks(string incoming, string filter) => _implementation.FilterTargetFrameworks(incoming, filter);
}

/// <summary>
/// The set of NuGet.Frameworks operations MSBuild needs during evaluation, abstracted so the
/// .NET Framework build can switch between the vendored in-process implementation and the
/// reflection/AppDomain implementation.
/// </summary>
internal interface INuGetFrameworkWrapper
{
    string GetTargetFrameworkIdentifier(string tfm);

    string GetTargetFrameworkVersion(string tfm, int minVersionPartCount);

    string GetTargetPlatformIdentifier(string tfm);

    string GetTargetPlatformVersion(string tfm, int minVersionPartCount);

    bool IsCompatible(string target, string candidate);

    string FilterTargetFrameworks(string incoming, string filter);
}
