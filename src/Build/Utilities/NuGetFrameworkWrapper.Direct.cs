// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using NuGet.Frameworks;

namespace Microsoft.Build.Evaluation;

// Direct-reference partial: NuGet.Frameworks is referenced at compile time and called
// without reflection, avoiding the per-call MethodInfo.Invoke cost of the reflection path.
internal sealed partial class NuGetFrameworkWrapper
{
    private NuGetFrameworkWrapper()
    { }

    public static NuGetFrameworkWrapper CreateInstance() => new();

    private static NuGetFramework Parse(string tfm) => NuGetFramework.Parse(tfm);

    public string GetTargetFrameworkIdentifier(string tfm) => Parse(tfm).Framework;

    public string GetTargetFrameworkVersion(string tfm, int minVersionPartCount) =>
        GetNonZeroVersionParts(Parse(tfm).Version, minVersionPartCount);

    public string GetTargetPlatformIdentifier(string tfm) => Parse(tfm).Platform;

    public string GetTargetPlatformVersion(string tfm, int minVersionPartCount) =>
        GetNonZeroVersionParts(Parse(tfm).PlatformVersion, minVersionPartCount);

    public bool IsCompatible(string target, string candidate) =>
        DefaultCompatibilityProvider.Instance.IsCompatible(Parse(target), Parse(candidate));

    public string FilterTargetFrameworks(string incoming, string filter) =>
        FilterTargetFrameworks<NuGetFramework, NuGetFrameworkAdapter>(incoming, filter, default);

    private readonly struct NuGetFrameworkAdapter : ITfmAdapter<NuGetFramework>
    {
        public NuGetFramework Parse(string tfm) => NuGetFramework.Parse(tfm);
        public string GetFramework(NuGetFramework parsed) => parsed.Framework;
        public bool GetAllFrameworkVersions(NuGetFramework parsed) => parsed.AllFrameworkVersions;
        public Version GetVersion(NuGetFramework parsed) => parsed.Version;
    }
}
