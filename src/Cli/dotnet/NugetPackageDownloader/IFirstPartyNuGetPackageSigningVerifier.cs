// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    public interface IFirstPartyNuGetPackageSigningVerifier
    {
        bool Verify(FilePath nupkgToVerify, out string commandOutput);

        /// <summary>
        /// Will only check if a file has signature without validating it.
        /// dotnet executable being not signed is used as a proxy for previews or source build
        /// </summary>
        bool IsExecutableIsFirstPartySignedWithoutValidation(FilePath executable);
    }
}
