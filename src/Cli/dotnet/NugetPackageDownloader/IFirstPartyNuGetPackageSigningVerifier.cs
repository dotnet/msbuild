// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    public interface IFirstPartyNuGetPackageSigningVerifier
    {
        bool Verify(FilePath nupkgToVerify, out string commandOutput);
    }
}
