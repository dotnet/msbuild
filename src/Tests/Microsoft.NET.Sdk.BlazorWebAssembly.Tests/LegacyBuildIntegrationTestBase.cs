// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.NET.Sdk.Razor.Tests;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public abstract class LegacyBuildIntegrationTestBase : AspNetSdkBaselineTest
    {
        public LegacyBuildIntegrationTestBase(ITestOutputHelper log) : base(log, GenerateBaselines) { }

        public abstract string TestAsset { get; }

        public abstract string TargetFramework { get; }

        
    }
}
