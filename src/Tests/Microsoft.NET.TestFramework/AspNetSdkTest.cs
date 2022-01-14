// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework
{
    [Trait("AspNetCore", "Integration")]
    public abstract class AspNetSdkTest : SdkTest
    {
        public readonly string DefaultTfm;

        protected AspNetSdkTest(ITestOutputHelper log) : base(log)
        {
            var assembly = Assembly.GetCallingAssembly();
            var testAssemblyMetadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            DefaultTfm = testAssemblyMetadata.SingleOrDefault(a => a.Key == "AspNetTestTfm").Value;
        }

        public TestAsset CreateAspNetSdkTestAsset(
            string testAsset,
            [CallerMemberName] string callerName = "",
            string subdirectory = "",
            string overrideTfm = null,
            string identifier = null) 
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, callingMethod: callerName, testAssetSubdirectory: subdirectory, identifier: identifier)
                .WithSource()
                .WithProjectChanges(project => 
                {
                    var ns = project.Root.Name.Namespace;
                    var targetFramework = project.Descendants()
                       .Single(e => e.Name.LocalName == "TargetFramework");
                    if (targetFramework.Value == "$(AspNetTestTfm)")
                    {
                        targetFramework.Value = overrideTfm ?? DefaultTfm;
                    }
                });
            return projectDirectory;
        }
    }
}
