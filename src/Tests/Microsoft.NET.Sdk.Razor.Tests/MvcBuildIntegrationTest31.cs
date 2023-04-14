// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class MvcBuildIntegrationTest31 : MvcBuildIntegrationTestLegacy
    {
        public MvcBuildIntegrationTest31(ITestOutputHelper log) : base(log) {}

        public override string TestProjectName => "SimpleMvc31";
        public override string TargetFramework => "netcoreapp3.1";
    }
}
