// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class MockFirstPartyNuGetPackageSigningVerifier: IFirstPartyNuGetPackageSigningVerifier
    {
        private readonly bool _verifyResult;
        private readonly string _commandOutput;
        
        public MockFirstPartyNuGetPackageSigningVerifier(bool verifyResult = true, string commandOutput = "")
        {
            _verifyResult = verifyResult;
            _commandOutput = commandOutput;
        }

        public bool Verify(FilePath nupkgToVerify, out string commandOutput)
        {
            commandOutput = _commandOutput;
            return _verifyResult;
        }
    }
}
