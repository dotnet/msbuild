// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantImplicitNamespaceImportsDisabled : SdkTest
    {
        public GivenThatWeWantImplicitNamespaceImportsDisabled(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_with_implicit_namespace_imports_disabled()
        {
            var asset = _testAssetsManager
                .CopyTestAsset("InferredTypeVariableName")
                .WithSource();

            var buildCommand = new BuildCommand(asset);

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("BC30455");

            buildCommand
                .Execute("/p:DisableImplicitNamespaceImports=true")
                .Should()
                .Pass();
        }
    }
}
