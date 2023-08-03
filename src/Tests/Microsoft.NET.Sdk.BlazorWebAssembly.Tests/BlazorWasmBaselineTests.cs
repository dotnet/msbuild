// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Razor.Tests;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorWasmBaselineTests : AspNetSdkBaselineTest
    {
        public BlazorWasmBaselineTests(ITestOutputHelper log, bool generateBaselines) : base(log, generateBaselines)
        {
        }

        protected override string EmbeddedResourcePrefix => string.Join('.', "Microsoft.NET.Sdk.BlazorWebAssembly.Tests", "StaticWebAssetsBaselines");

        protected override string ComputeBaselineFolder() =>
            Path.Combine(TestContext.GetRepoRoot() ?? AppContext.BaseDirectory, "src", "Tests", "Microsoft.NET.Sdk.BlazorWebAssembly.Tests", "StaticWebAssetsBaselines");
    }
}
