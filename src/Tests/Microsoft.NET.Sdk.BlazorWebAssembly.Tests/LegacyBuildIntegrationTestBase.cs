// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
