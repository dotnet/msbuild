// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class MvcBuildIntegrationTest21 : MvcBuildIntegrationTestLegacy
    {
        public MvcBuildIntegrationTest21(ITestOutputHelper log) : base(log) { }

        public override string TestProjectName => "SimpleMvc21";
        public override string TargetFramework => "netcoreapp2.1";
    }
}
