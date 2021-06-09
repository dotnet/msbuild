// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class FirstPartySignatureVerifierTests : SdkTest
    {
        public FirstPartySignatureVerifierTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenCallingIsExecutable1StPartySignedWithValidationItShouldNotThrow()
        {
            Action a = () => new FirstPartyNuGetPackageSigningVerifier().IsExecutableIsFirstPartySignedWithoutValidation(
                new FilePath(
                    typeof(DotNet.Cli.Program).Assembly.Location));
            a.ShouldNotThrow();
        }
    }
}
