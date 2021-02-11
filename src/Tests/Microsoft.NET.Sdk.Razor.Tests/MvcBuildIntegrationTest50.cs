// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
    public class MvcBuildIntegrationTest50 : MvcBuildIntegrationTestLegacy
    {
        public MvcBuildIntegrationTest50(ITestOutputHelper log) : base(log) {}

        public override string TestProjectName => "SimpleMvc50";
        public override string TargetFramework => "net5.0";
    }
}
