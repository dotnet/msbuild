// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public abstract class BlazorWasmSdkTest : SdkTest
    {
        private static readonly IEnumerable<System.Reflection.AssemblyMetadataAttribute> TestAssemblyMetadata = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>();
        public readonly string DefaultTfm = TestAssemblyMetadata.SingleOrDefault(a => a.Key == "DefaultBlazorTestTfm").Value;

        protected BlazorWasmSdkTest(ITestOutputHelper log) : base(log) {}

        public TestAsset CreateBlazorWasmSdkTestAsset(
            string testAsset,
            [CallerMemberName] string callerName = "",
            string subdirectory = "",
            string overrideTfm = null) 
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, callingMethod: callerName, testAssetSubdirectory: subdirectory)
                .WithSource()
                .WithProjectChanges(project => 
                {
                    var ns = project.Root.Name.Namespace;
                    var targetFramework = project.Descendants()
                       .Single(e => e.Name.LocalName == "TargetFramework");
                    if (targetFramework.Value == "$(DefaultBlazorTestTfm)")
                    {
                        targetFramework.Value = overrideTfm ?? DefaultTfm;
                    }
                });
            return projectDirectory;
        }
    }
}
