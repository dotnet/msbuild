// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Razor.Tests;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public abstract class LegacyBuildIntegrationTestBase : AspNetSdkBaselineTest
    {
        public LegacyBuildIntegrationTestBase(ITestOutputHelper log) : base(log, GenerateBaselines) { }

        public abstract string TestAsset { get; }

        public abstract string TargetFramework { get; }


    }
}
