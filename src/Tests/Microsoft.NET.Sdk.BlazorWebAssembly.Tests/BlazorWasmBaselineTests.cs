// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.NET.Sdk.Razor.Tests;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorWasmBaselineTests : AspNetSdkBaselineTest
    {
        public BlazorWasmBaselineTests(ITestOutputHelper log, bool generateBaselines) : base(log, generateBaselines)
        {
            PathTemplatizer = TemplatizeCompressedAssets;
        }

        private string TemplatizeCompressedAssets(StaticWebAsset asset, string originalValue, StaticWebAsset relatedAsset)
        {
            if (!asset.IsAlternativeAsset())
            {
                return null;
            }
            
            if (asset.RelatedAsset == originalValue)
            {
                return null;
            }

            return Path.Combine(Path.GetDirectoryName(asset.Identity), "{" + asset.RelativePath + "}");
        }

        protected override string EmbeddedResourcePrefix => string.Join('.', "Microsoft.NET.Sdk.BlazorWebAssembly.Tests", "StaticWebAssetsBaselines");

        protected override string ComputeBaselineFolder() =>
            Path.Combine(TestContext.GetRepoRoot() ?? AppContext.BaseDirectory, "src", "Tests", "Microsoft.NET.Sdk.BlazorWebAssembly.Tests", "StaticWebAssetsBaselines");
    }
}
