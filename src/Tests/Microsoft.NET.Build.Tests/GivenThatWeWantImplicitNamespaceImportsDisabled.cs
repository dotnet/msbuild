// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
